using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.BuildIn;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using ExpressionVisitor = ConstExpr.SourceGenerator.Visitors.ExpressionVisitor;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Invocation and member access visitor methods for the ConstExprPartialRewriter.
///   Handles method invocations, element access, and member access expressions.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// Handle nameof(...) directly
		if (TryHandleNameof(node, out var nameofResult))
		{
			return nameofResult;
		}

		if (!semanticModel.TryGetSymbol(node, symbolStore, out IMethodSymbol? targetMethod))
		{
			if (node.Expression is MemberAccessExpressionSyntax fallbackMemberAccess)
			{
				MarkReceiverAsAlteredIfNeeded(null, fallbackMemberAccess.Expression, fallbackMemberAccess.Name.Identifier.Text);
			}

			return VisitInvocationExpressionFallback(node);
		}

		var originalArguments = node.ArgumentList.Arguments;
		var argumentExpressions = new ExpressionSyntax[originalArguments.Count];

		for (var i = 0; i < originalArguments.Count; i++)
		{
			var originalExpression = originalArguments[i].Expression;

			// A ref/out argument must remain an lvalue - Visit() can fold a tracked variable straight
			// to a literal (e.g. `-2`, itself a PrefixUnaryExpressionSyntax negation, not even a plain
			// LiteralExpressionSyntax - so a "did it become a literal" check misses it too), which is
			// not valid C# in an out/ref slot and silently breaks the by-ref write the call depends on.
			// Always keep the original lvalue for these; constant-argument extraction below still reads
			// the variable's current value straight off that same identifier.
			argumentExpressions[i] = originalArguments[i].RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword, SyntaxKind.OutKeyword)
				? originalExpression
				: Visit(originalExpression) as ExpressionSyntax ?? originalExpression;
		}

		var constantArguments = ExtractConstantArguments(argumentExpressions, originalArguments);

		// IMethodSymbol.Parameters always reflects the full declaration, including trailing
		// optional parameters the call site omitted (e.g. AggregateBy's keyComparer = null) —
		// fill those in with their declared default so the length still lines up below.
		if (constantArguments.Count == originalArguments.Count)
		{
			for (var i = constantArguments.Count; i < targetMethod.Parameters.Length; i++)
			{
				var parameter = targetMethod.Parameters[i];

				if (!parameter.HasExplicitDefaultValue)
				{
					break;
				}

				constantArguments.Add(parameter.ExplicitDefaultValue!);
			}
		}

		// Try to execute with all constant arguments
		var executed = false;

		if (constantArguments.Count == targetMethod.Parameters.Length)
		{
			var result = TryExecuteWithConstantArguments(node, targetMethod, constantArguments, out executed);

			if (result is not null)
			{
				return result;
			}
		}

		// Only mark the receiver altered (wiping its tracked value) once we know reflective
		// execution above did NOT already run: a receiver with a known value (e.g. a List<T>
		// fuzzed by RunRandomTests) that reaches TryExecuteInstanceMethod gets its mutating call
		// (Add, Remove, ...) actually invoked via reflection on the SAME tracked object - the
		// mutation is already captured, and every later read of the receiver should keep folding
		// against it. Checking this before the attempt (as this used to) meant hasLiteral inside
		// TryExecuteInstanceMethod was always false, since the wipe had already happened -
		// nothing could ever be reflectively executed for a receiver this pass considers likely-
		// mutating in the first place.
		if (!executed && node.Expression is MemberAccessExpressionSyntax receiverMemberAccess)
		{
			MarkReceiverAsAlteredIfNeeded(targetMethod, receiverMemberAccess.Expression, receiverMemberAccess.Name.Identifier.Text);
		}

		// Try string optimizers
		if (targetMethod.ContainingType.SpecialType == SpecialType.System_String
		    && node.Expression is MemberAccessExpressionSyntax stringMemberAccess)
		{
			var tempNode = node.WithExpression(VisitMemoized(node.Expression) as ExpressionSyntax ?? node.Expression);
			var optimized = TryOptimizeStringMethod(semanticModel, targetMethod, tempNode, stringMemberAccess, argumentExpressions, originalArguments);

			if (optimized is not null)
			{
				return Visit(optimized);
			}
		}
		// Try math optimizers. Each optimizer opts in through its own RequiredFlags (default
		// FastMathFlags.NoNaN, so trig/log/approximation rewrites stay gated on it) — a handful are
		// exact for every input and mark themselves FastMathFlags.Strict so they run unconditionally.
		else
		{
			var tempNode = node.WithExpression(VisitMemoized(node.Expression) as ExpressionSyntax ?? node.Expression);
			var optimized = TryOptimizeMathMethod(semanticModel, targetMethod, tempNode, argumentExpressions, originalArguments);

			if (optimized is not null)
			{
				return Visit(optimized);
			}
		}

		if (TryOptimizeSimdMethod(semanticModel, targetMethod, node, argumentExpressions, originalArguments) is { } optimizedSimd)
		{
			return Visit(optimizedSimd);
		}

		if (TryOptimizeRegexMethod(semanticModel, targetMethod, node, argumentExpressions, originalArguments) is { } optimizedRegex)
		{
			return optimizedRegex;
		}

		// Try LINQ optimizers (for inner calls, or when unrolling was skipped).
		// The optimized result is annotated with symbol info so it can be unrolled
		// when it re-enters the rewriter through Visit.
		if (attribute.LinqOptimization != LinqOptimizationMode.None)
		{
			if (TryOptimizeLinqMethod(semanticModel, targetMethod, node, argumentExpressions, originalArguments) is { } optimizedLinq)
			{
				if (attribute.LinqOptimization == LinqOptimizationMode.Unroll
				    && LinqUnroller.TryUnrollLinqChain(optimizedLinq, Visit, semanticModel, additionalMethods, symbolStore, out var unrolled, variables, OptimizeUnrolledHelperBody))
				{
					return unrolled;
				}

				return optimizedLinq;
			}

			if (attribute.LinqOptimization == LinqOptimizationMode.Unroll
			    && LinqUnroller.TryUnrollLinqChain(node, Visit, semanticModel, additionalMethods, symbolStore, out var unrolledNode, variables, OptimizeUnrolledHelperBody))
			{
				return unrolledNode;
			}
		}

		// A mutating call's receiver (`values.Add(...)`) must keep its own identity rather than being
		// visited normally: visiting would inline a fully-known receiver to its current literal value
		// (e.g. `values` -> `[1]`), producing `[1].Add(1)` - a throwaway collection literal that gets
		// mutated and immediately discarded instead of the real, caller-aliased object. Reads have no
		// such concern (a snapshot read and a live read agree on a known value), so only mutating
		// calls on a plain identifier receiver skip the visit. Must happen here, before `node` is
		// rebound below - HandleInstanceMethodInvocation's own copy of this guard only protects
		// against a second, redundant visit once this one has already preserved the receiver.
		var expression = node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax } mutatingReceiverAccessOuter
		                 && IsLikelyMutatingMethod(targetMethod, mutatingReceiverAccessOuter.Name.Identifier.Text)
			? mutatingReceiverAccessOuter
			: VisitMemoized(node.Expression) as ExpressionSyntax ?? node.Expression;

		if (expression is LambdaExpressionSyntax lambdaExpression)
		{
			return Visit(TryEvaluateLambdaVariableWithArguments(lambdaExpression, argumentExpressions, targetMethod));
		}

		node = node.WithExpression(expression);

		// Handle char overload conversion
		if (ConvertToCharOverloadIfNeeded(targetMethod, argumentExpressions, out var newArguments, out var charMethod))
		{
			targetMethod = charMethod;
			argumentExpressions = newArguments;
		}

		// Handle static methods and local functions
		return targetMethod.IsStatic || targetMethod.MethodKind == MethodKind.LocalFunction
			? HandleStaticMethodInvocation(node, targetMethod, argumentExpressions)
			: HandleInstanceMethodInvocation(node, targetMethod, argumentExpressions);

	}

	private void MarkReceiverAsAlteredIfNeeded(IMethodSymbol? targetMethod, ExpressionSyntax receiver, string methodName)
	{
		if (targetMethod?.IsStatic is true || receiver is not IdentifierNameSyntax identifier)
		{
			return;
		}

		if (!variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			return;
		}

		if (variable.Type is null || !variable.Type.IsReferenceType || variable.Type.SpecialType == SpecialType.System_String)
		{
			return;
		}

		var isLikelyMutating = IsLikelyMutatingMethod(targetMethod, methodName);

		if (!isLikelyMutating)
		{
			return;
		}

		variable.IsAltered = true;
		variable.HasValue = false;
		variable.Value = null;
	}

	private static bool IsLikelyMutatingMethod(IMethodSymbol? targetMethod, string methodName)
	{
		if (targetMethod is not null)
		{
			// `IsStatic` alone doesn't catch this: Roslyn reports a REDUCED extension-method symbol
			// (the one bound from instance-call syntax like `x.Append(10)`) as IsStatic == false, so
			// without this an extension method sharing a name with the builder/collection-mutation
			// list below (e.g. LINQ's non-mutating Enumerable.Append vs. StringBuilder.Append) is
			// wrongly flagged as mutating its receiver. IsExtensionMethod is true for both the
			// reduced and unreduced form, so it doesn't have that gap.
			if (targetMethod.IsStatic || targetMethod.IsExtensionMethod)
			{
				return false;
			}

			if (targetMethod.ReturnsVoid || targetMethod.Parameters.Any(static p => p.RefKind is not RefKind.None))
			{
				return true;
			}
		}

		return methodName is "Add" or "AddRange" or "Insert" or "InsertRange" or "Remove" or "RemoveAt" or "RemoveAll"
			or "RemoveRange" or "Clear" or "Sort" or "Enqueue" or "Dequeue" or "Push" or "Pop"
			or "TryAdd" or "TryTake" or "UnionWith" or "IntersectWith" or "ExceptWith" or "SymmetricExceptWith"
			// Builder/writer mutation methods — their side effect is the point, return value is the builder itself
			or "Append" or "AppendLine" or "AppendFormat" or "AppendJoin" or "AppendLiteral"
			or "Write" or "WriteLine" or "Flush";
	}

	/// <summary>
	///   Tries to handle nameof(...) expressions.
	/// </summary>
	private bool TryHandleNameof(InvocationExpressionSyntax node, out SyntaxNode? result)
	{
		result = null;

		if (node is not { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }, ArgumentList.Arguments.Count: 1 })
		{
			return false;
		}

		var arg = node.ArgumentList.Arguments[0].Expression;
		string? name = null;

		if (semanticModel.TryGetSymbol(arg, symbolStore, out ISymbol? sym))
		{
			name = sym.Name;
		}
		else
		{
			name = arg switch
			{
				IdentifierNameSyntax id => id.Identifier.Text,
				MemberAccessExpressionSyntax { Name: IdentifierNameSyntax last } => last.Identifier.Text,
				QualifiedNameSyntax qn => qn.Right.Identifier.Text,
				GenericNameSyntax gen => gen.Identifier.Text,
				_ => name
			};
		}

		if (name is not null)
		{
			result = CreateLiteral(name);
			return true;
		}

		result = base.VisitInvocationExpression(node);
		return true;
	}

	/// <summary>
	///   Extracts constant arguments from visited arguments.
	/// </summary>
	private List<object> ExtractConstantArguments(IReadOnlyList<ExpressionSyntax> arguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var constantArguments = new List<object>(arguments.Count);

		try
		{
			for (var i = 0; i < arguments.Count; i++)
			{
				if (TryGetLiteralValue(arguments[i], out var value) || TryGetLiteralValue(originalArguments[i], out value))
				{
					constantArguments.Add(value);
				}
			}
		}
		catch (Exception e)
		{
			exceptionHandler(arguments[0], e);
		}

		return constantArguments;
	}

	/// <summary>
	///   Tries to execute a method with constant arguments. <paramref name="executed" /> is true when
	///   the method actually ran via reflection - including a void-returning call that folds to
	///   nothing (e.g. <c>List&lt;T&gt;.Add</c>) - as opposed to this method returning
	///   <see langword="null" /> because nothing could be attempted at all. Callers use this to know
	///   whether a mutating call already updated its receiver's tracked value in place, so they don't
	///   also have to conservatively wipe that tracking afterward.
	/// </summary>
	private SyntaxNode? TryExecuteWithConstantArguments(InvocationExpressionSyntax node, IMethodSymbol targetMethod, List<object> constantArguments, out bool executed)
	{
		executed = false;

		try
		{
			// Check if the invocation target is a local lambda variable (e.g. var func = (int x) => x + 1; func(6))
			if (node.Expression is IdentifierNameSyntax { Identifier.Text: var lambdaVarName }
			    && variables.TryGetValue(lambdaVarName, out var lambdaVar)
			    && lambdaVar is { CanBeInlined: true, HasValue: true, Value: LambdaExpressionSyntax lambdaSyntax })
			{
				// Both this and the operation-visitor branch below run real code reflectively, and either
				// can mutate an object a VariableItem still points at (an array handed to a local function
				// mutates the caller's tracked value in place) - which the visit fingerprint cannot see.
				// Announced up front rather than on success: a call that mutates and then throws has still
				// mutated. Narrow paths - a bare-identifier callee or a non-member-access call with every
				// argument constant - so this costs the memo almost nothing.
				MarkStateMutated();

				var lambdaResult = TryEvaluateLambdaVariableWithArguments(lambdaSyntax, constantArguments, targetMethod);

				if (lambdaResult is not null)
				{
					lambdaVar.IsAccessed = false;
					return lambdaResult.WithTypeSymbolAnnotation(targetMethod.ReturnType, symbolStore);
				}
			}

			if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName }
			    && !targetMethod.ContainingType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
			{
				return TryExecuteInstanceMethod(targetMethod, instanceName, constantArguments, node.ArgumentList.Arguments, out executed)
					.WithTypeSymbolAnnotation(targetMethod.ReturnType, symbolStore);
			}

			MarkStateMutated();

			return TryExecuteViaOperationVisitor(targetMethod, constantArguments)
				.WithTypeSymbolAnnotation(targetMethod.ReturnType, symbolStore);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	///   Tries to execute an instance method. See <see cref="TryExecuteWithConstantArguments" /> for
	///   what <paramref name="executed" /> signals.
	/// </summary>
	private SyntaxNode? TryExecuteInstanceMethod(IMethodSymbol targetMethod, ExpressionSyntax instanceName, List<object> constantArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments, out bool executed)
	{
		executed = false;

		// Memoized: the visited node itself is thrown away here - all that is read off it is whether the
		// receiver folds to a literal - but the same receiver gets visited again further down this same
		// method call (the LINQ optimizers, then the invocation's own expression). On the LINQ corpus this
		// speculative walk alone was ~44% of rewriter time, of which under 4% was the reflective execution
		// it exists for.
		var hasLiteral = TryGetLiteralValue(instanceName, out var instance)
		                 || TryGetLiteralValue(VisitMemoized(instanceName), out instance);

		if (hasLiteral && loader.TryGetType(targetMethod.ContainingType, out var type))
		{
			try
			{
				instance = Convert.ChangeType(instance, type);
			}
			catch (InvalidCastException) { }
		}

		if ((targetMethod.IsStatic || hasLiteral
			    && (instanceName is not IdentifierNameSyntax identifier || CanBePruned(identifier.Identifier.Text)))
		    && loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value, out var executedParameters))
		{
			executed = true;

			// A reflectively executed call can mutate the tracked instance in place (values.Add(...)),
			// which leaves VariableItem.Value pointing at the same object and is therefore invisible to
			// the fingerprint - say so explicitly so no memoized visit outlives the mutation.
			MarkStateMutated();

			WriteBackRefOutParameters(originalArguments, executedParameters);

			if (targetMethod.ReturnsVoid)
			{
				return null;
			}

			return TryCreateLiteral(value, out var literal) ? literal : null;
		}

		return null;
	}

	/// <summary>
	///   After a reflectively-executed call, copies each ref/out argument's post-invoke value (which
	///   `MethodBase.Invoke` already wrote into <paramref name="executedParameters" /> as part of the
	///   call - see the `TryExecuteMethod` overload with an `out IReadOnlyList{object?}?` parameter)
	///   back into the tracked <see cref="VariableItem" /> it was passed as, e.g. so `Int32.TryParse(s, out value)`
	///   actually updates `value` for the rest of the fold instead of leaving it at its stale pre-call value.
	///   Scoped to a bare identifier naming a tracked variable - other lvalue shapes (arr[i], obj.Field)
	///   aren't tracked per-write here and are left as an unfolded runtime call.
	/// </summary>
	private void WriteBackRefOutParameters(SeparatedSyntaxList<ArgumentSyntax> originalArguments, IReadOnlyList<object?>? executedParameters)
	{
		if (executedParameters is null)
		{
			return;
		}

		for (var i = 0; i < originalArguments.Count && i < executedParameters.Count; i++)
		{
			if (originalArguments[i].RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword, SyntaxKind.OutKeyword)
			    && originalArguments[i].Expression is IdentifierNameSyntax { Identifier.Text: var name }
			    && variables.TryGetValue(name, out var variable))
			{
				variable.Value = executedParameters[i];
				variable.HasValue = true;
			}
		}
	}

	/// <summary>
	///   Tries to execute a method via the operation visitor.
	/// </summary>
	private SyntaxNode? TryExecuteViaOperationVisitor(IMethodSymbol targetMethod, List<object> constantArguments)
	{
		if (!TryGetOperation<IOperation>(semanticModel, targetMethod, out var methodOperation))
		{
			return null;
		}

		var parameters = methodOperation.Syntax switch
		{
			LocalFunctionStatementSyntax localFunc => localFunc.ParameterList,
			MethodDeclarationSyntax methodDecl => methodDecl.ParameterList,
			_ => null
		};

		var vars = new Dictionary<string, object?>();

		for (var i = 0; i < (parameters?.Parameters.Count ?? 0); i++)
		{
			var parameterName = parameters!.Parameters[i].Identifier.Text;
			vars.Add(parameterName, constantArguments[i]);
		}

		var visitor = new ConstExprOperationVisitor(semanticModel, loader, (_, _) => { }, token);

		switch (methodOperation)
		{
			case ILocalFunctionOperation { Body: not null } localFunction:
			{
				visitor.VisitBlock(localFunction.Body, vars);
				break;
			}
			case IMethodBodyOperation { BlockBody: not null } methodBody:
			{
				visitor.VisitBlock(methodBody.BlockBody, vars);
				break;
			}
		}

		if (vars.TryGetValue(ConstExprOperationVisitor.RETURNVARIABLENAME, out var retVal)
		    && retVal is not null
		    && TryCreateLiteral(retVal, out var result))
		{
			return result;
		}

		return null;
	}

	/// <summary>
	///   Tries to optimize a string method.
	/// </summary>
	private SyntaxNode? TryOptimizeStringMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, MemberAccessExpressionSyntax memberAccess, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var instance = Visit(memberAccess.Expression);

		var optimizers = OptimizerRegistry.CreateStringOptimizers(targetMethod.Name, instance);

		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		foreach (var stringOptimizer in optimizers)
		{
			if (stringOptimizer!.TryOptimize(context, out var optimized))
			{
				return optimized;
			}
		}

		if (targetMethod.IsStatic)
		{
			// Keep the receiver exactly as written (e.g. `System.String` vs `String`) — only the
			// folded arguments change.
			return node.WithArgumentList(node.ArgumentList.WithArguments(ToArgumentList(visitedArguments, originalArguments)));
		}

		return null;
	}

	/// <summary>
	///   Tries to optimize a math method.
	/// </summary>
	private SyntaxNode? TryOptimizeMathMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var type = targetMethod.Parameters
			.Select(s => s.Type)
			.FirstOrDefault();

		if (targetMethod.ContainingType.ToString() is not ("System.Math" or "System.MathF" or "System.Numerics.BitOperations")
		    && (!targetMethod.ContainingType.EqualsType(type)
		        || !type.IsNumericType()))
		{
			return null;
		}

		var enabled = _mathOptimizers.Where(IsMathOptimizerEnabled).ToList();

		// In strict mode only a handful of exact optimizers are enabled; skip building the context
		// (a per-call allocation) when the current math mode leaves nothing that could fire.
		if (enabled.Count == 0)
		{
			return null;
		}

		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		return enabled
			.WhereSelect<BaseMathFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();
	}

	/// <summary>
	///   A math optimizer runs when its <see cref="BaseMathFunctionOptimizer.RequiredFlags" /> lists
	///   <see cref="FastMathFlags.Strict" /> (always) or any flag the attribute has set. Mirrors the
	///   <see cref="Optimizers.BinaryOptimizers.Strategies.IBinaryStrategy.RequiredFlags" /> convention.
	/// </summary>
	private bool IsMathOptimizerEnabled(BaseMathFunctionOptimizer optimizer)
	{
		var required = optimizer.RequiredFlags;

		return required.Contains(FastMathFlags.Strict)
		       || required.Any(f => attribute.MathOptimizations.HasFlag(f));
	}

	/// <summary>
	///   Tries to optimize a linq method.
	/// </summary>
	private SyntaxNode? TryOptimizeLinqMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _linqOptimizers
			.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
			            && o.IsValidParameterCount(targetMethod.Parameters.Length))
			.WhereSelect<BaseLinqFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

		return result;
	}

	private SyntaxNode? TryOptimizeSimdMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _simdOptimizers
			.WhereSelect<BaseSimdFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

		return result;
	}

	/// <summary>
	///   Tries to optimize a Regex method (e.g. Regex.IsMatch) by converting the constant pattern to inline C# code.
	/// </summary>
	private SyntaxNode? TryOptimizeRegexMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		if (targetMethod.ContainingType?.ToString() != "System.Text.RegularExpressions.Regex")
		{
			return null;
		}

		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _regexOptimizers
			.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
			            && o.IsValidParameterCount(targetMethod.Parameters.Length))
			.WhereSelect<BaseRegexFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

		if (result is not null)
		{
			return result;
		}

		return node;
	}

	private FunctionOptimizerContext GetFunctionOptimizerContext(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IReadOnlyList<ExpressionSyntax> visitedArguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
	{
		var getLambda = new Func<LambdaExpressionSyntax, LambdaExpression?>(lambda =>
		{
			try
			{
				if (!semanticModel.TryGetOperation<IAnonymousFunctionOperation>(lambda, out var operation))
				{
					return null;
				}

				// Create parameters for the lambda
				var lambdaParams = operation.Symbol.Parameters
					.Select(p => Expression.Parameter(loader.GetType(p.Type), p.Name))
					.ToArray();

				// Create a new visitor with the lambda parameters included
				var allParams = variables.Select(s => Expression.Parameter(loader.GetType(s.Value.Type), s.Key)).Concat(lambdaParams);
				var lambdaVisitor = new ExpressionVisitor(semanticModel, loader, allParams);

				// Visit the body with the new visitor
				var body = lambdaVisitor.VisitBlock(operation.Body, new VariableItemDictionary(variables));

				// Create the lambda expression
				return Expression.Lambda(body, lambdaParams);
			}
			catch (Exception)
			{
				return null;
			}
		});

		var optimizeBinaryExpression = new Func<BinaryExpressionSyntax, ITypeSymbol, ITypeSymbol, ITypeSymbol, ExpressionSyntax>((binary, leftType, rightType, type) =>
		{
			if (binary.Left is LiteralExpressionSyntax leftLiteral
			    && binary.Right is LiteralExpressionSyntax rightLiteral
			    && TryCreateLiteral(ObjectExtensions.ExecuteBinaryOperation(binary.Kind(), leftLiteral.Token.Value, rightLiteral.Token.Value), out var leftValue))
			{
				return leftValue;
			}

			var expressions = GetBinaryExpressions(node).ToList();

			if (TryOptimizeNode(binary.Kind().ToBinaryOperatorKind(), expressions, type, binary.Left, leftType, binary.Right, rightType, node.Parent, out var optimizedNode)
			    && optimizedNode is ExpressionSyntax optimizedExpr)
			{
				return optimizedExpr;
			}

			return binary;
		});

		var originalParameterExpressions = new ExpressionSyntax[originalArguments.Count];

		for (var i = 0; i < originalArguments.Count; i++)
		{
			originalParameterExpressions[i] = originalArguments[i].Expression;
		}

		return new FunctionOptimizerContext(model,
			loader,
			targetMethod,
			node,
			visitedArguments.ToArray(),
			originalParameterExpressions,
			// Memoized: the LINQ optimizers reach for context.Visit from 98 call sites, and the two on the
			// hot path (TryExecutePredicates and UpdateInvocation) both walk the very same receiver.
			x => VisitMemoized(x) as ExpressionSyntax,
			x => VisitMemoized(x) as StatementSyntax,
			getLambda,
			optimizeBinaryExpression,
			additionalMethods,
			variables,
			usings,
			attribute.MathOptimizations,
			attribute.Optimizations,
			symbolStore);
	}

	/// <summary>
	///   Converts arguments to char if there's a char overload available.
	/// </summary>
	private bool ConvertToCharOverloadIfNeeded(IMethodSymbol targetMethod, IReadOnlyList<ExpressionSyntax> arguments, [NotNullWhen(true)] out ExpressionSyntax[]? newArguments, [NotNullWhen(true)] out IMethodSymbol? charMethod)
	{
		if (attribute.MathOptimizations == FastMathFlags.Strict
		    || !TryGetCharOverload(targetMethod, arguments, out charMethod))
		{
			charMethod = null;
			newArguments = null;
			return false;
		}

		newArguments = new ExpressionSyntax[arguments.Count];

		for (var i = 0; i < arguments.Count; i++)
		{
			var argument = arguments[i];

			if (TryGetLiteralValue(argument, out var value) && value is string { Length: 1 } charValue)
			{
				newArguments[i] = LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(charValue[0]));
				continue;
			}

			newArguments[i] = argument;
		}

		return true;
	}

	/// <summary>
	///   Handles static method invocation.
	/// </summary>
	private SyntaxNode? HandleStaticMethodInvocation(InvocationExpressionSyntax node, IMethodSymbol targetMethod, IReadOnlyList<ExpressionSyntax> arguments)
	{
		// Check if method is empty (applies to local functions too)
		if (IsEmptyMethod(targetMethod))
		{
			return null;
		}

		// Check if we're already visiting this method to prevent infinite recursion
		if (visitingMethods?.Contains(targetMethod) is true)
		{
			usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());

			return node
				.WithArgumentList(node.ArgumentList.WithArguments(ToArgumentList(arguments, node.ArgumentList.Arguments)))
				.WithMethodSymbolAnnotation(targetMethod, symbolStore);
		}

		// Inline local functions that are called exactly once
		if (targetMethod.MethodKind == MethodKind.LocalFunction
		    && CountLocalFunctionInvocations(targetMethod) == 1)
		{
			var inlined = TryInlineLocalFunction(targetMethod, arguments);

			if (inlined is not null)
			{
				return inlined;
			}
		}

		// When at least one argument is a compile-time constant, specialize a private helper
		// with that value folded in (e.g. a loop over a now-known string can unroll) instead of
		// the fully generic, argument-blind body every call site of this method would share.
		if (targetMethod.MethodKind != MethodKind.LocalFunction
		    && arguments.Any(a => TryGetLiteralValue(a, out _))
		    && GetSpecializedMethodSyntax(targetMethod, arguments, out var specializedArguments) is { } specialized)
		{
			if (!additionalMethods.ContainsKey(specialized))
			{
				additionalMethods.Add(specialized, true);
			}

			return WithInvocationTargetName(node, specialized.Identifier.Text)
				.WithArgumentList(node.ArgumentList.WithArguments(ToArgumentList(specializedArguments, node.ArgumentList.Arguments)))
				.WithMethodSymbolAnnotation(targetMethod, symbolStore);
		}

		var syntax = GetInlinedMethodSyntax(targetMethod);

		if (syntax is not null && !additionalMethods.ContainsKey(syntax))
		{
			additionalMethods.Add(syntax, true);
		}
		else if (syntax is null)
		{
			usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());
		}

		return node
			.WithArgumentList(node.ArgumentList.WithArguments(ToArgumentList(arguments, node.ArgumentList.Arguments)))
			.WithMethodSymbolAnnotation(targetMethod, symbolStore);
	}

	/// <summary>
	///   Counts the number of invocations of a local function within its declaring block.
	/// </summary>
	private int CountLocalFunctionInvocations(IMethodSymbol targetMethod)
	{
		var functionSyntax = targetMethod.DeclaringSyntaxReferences
			.Select(r => r.GetSyntax(token))
			.OfType<LocalFunctionStatementSyntax>()
			.FirstOrDefault();

		if (functionSyntax?.Parent is not BlockSyntax parentBlock)
		{
			return -1;
		}

		return parentBlock
			.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation =>
				semanticModel.TryGetSymbol(invocation, symbolStore, out IMethodSymbol? m)
				&& SymbolEqualityComparer.Default.Equals(m, targetMethod));
	}

	/// <summary>
	///   Tries to inline a local function at the call site by substituting arguments into the function body.
	/// </summary>
	private SyntaxNode? TryInlineLocalFunction(IMethodSymbol targetMethod, IReadOnlyList<ExpressionSyntax> arguments)
	{
		try
		{
			var functionSyntax = targetMethod.DeclaringSyntaxReferences
				.Select(r => r.GetSyntax(token))
				.OfType<LocalFunctionStatementSyntax>()
				.FirstOrDefault();

			if (functionSyntax is null)
			{
				return null;
			}

			var parameters = functionSyntax.ParameterList.Parameters;

			if (parameters.Count != arguments.Count)
			{
				return null;
			}

			// Build a sub-variable dictionary with arguments bound to parameter names
			var subParams = new Dictionary<string, VariableItem>(variables, StringComparer.Ordinal);

			for (var i = 0; i < parameters.Count; i++)
			{
				var paramName = parameters[i].Identifier.Text;
				var paramType = semanticModel.GetTypeInfo(parameters[i].Type!).Type
				                ?? semanticModel.Compilation.ObjectType;
				var argExpr = arguments[i];

				if (TryGetLiteralValue(argExpr, out var literalValue))
				{
					subParams[paramName] = new VariableItem(paramType, true, literalValue, true)
					{
						CanBeInlined = true
					};
				}
				else
				{
					subParams[paramName] = new VariableItem(paramType, false, argExpr, true)
					{
						CanBeInlined = true
					};
				}
			}

			visitingMethods?.Add(targetMethod);

			var subRewriter = new ConstExprPartialRewriter(
				semanticModel, loader, (_, _) => { }, subParams,
				additionalMethods, usings, attribute, symbolStore, token, visitingMethods);

			SyntaxNode? result = null;

			// Expression-bodied: e.g. int Add(int a, int b) => a + b;
			if (functionSyntax.ExpressionBody is { Expression: { } bodyExpr })
			{
				result = subRewriter.Visit(bodyExpr) as ExpressionSyntax;
			}
			else if (functionSyntax.Body is not null)
			{
				// Block-bodied: visit the block and look for a single return statement
				var visitedBlock = subRewriter.Visit(functionSyntax.Body) as BlockSyntax;
				var pruned = DeadCodePruner.Prune(visitedBlock!, subParams, semanticModel) as BlockSyntax;

				if (pruned?.Statements is [ ReturnStatementSyntax { Expression: { } returnExpr } ])
				{
					result = returnExpr;
				}
			}

			visitingMethods?.Remove(targetMethod);

			return result;
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	///   Runs the same <see cref="OptimizationPipeline" /> over a helper body that the calling method
	///   itself gets, so a method emitted next to its caller (<c>FindMax</c> alongside <c>Range</c>)
	///   is optimized rather than copied verbatim from source. Without it the helper also keeps the
	///   duplicate subexpressions inlining introduces — e.g. a ternary the source computed once via a
	///   local, now inlined twice.
	/// </summary>
	private BlockSyntax? OptimizeInlinedBody(BlockSyntax? body, IDictionary<string, VariableItem> parameters, ParameterListSyntax parameterList, SyntaxToken methodName)
	{
		if (body is null)
		{
			return body;
		}

		// Same preprocessing the other two call sites (the generator, the test harness) give the
		// pipeline: pruning dead code and simplifying exception guards first, so an inlined/unrolled
		// helper body enters OptimizationPipeline in the same state a top-level method body would.
		var pruned = DeadCodePruner.Prune(body, parameters, semanticModel) as BlockSyntax ?? body;
		var simplified = ExceptionGuardSimplifier.Simplify(pruned) as BlockSyntax ?? pruned;

		return OptimizationPipeline.Apply(simplified, parameterList, methodName, attribute, parameters, semanticModel, symbolStore, additionalMethods, usings) as BlockSyntax ?? simplified;
	}

	/// <summary>
	///   The same treatment for a helper the LINQ unroller synthesized, which reaches
	///   <see cref="OptimizeInlinedBody" /> through <see cref="LinqUnroller.TryUnrollLinqChain" />'s
	///   optimize callback rather than from an inlined source method.
	///   <para>
	///     Passed an empty variable map rather than the enclosing method's: the pipeline reads it to
	///     recover the type of a <c>var</c> local the semantic model can no longer resolve, and the
	///     names in there belong to the caller, not to this body — a <c>result</c> in both would hand
	///     bounds-check elimination the wrong type. Everything the passes legitimately need about this
	///     helper is in <paramref name="parameterList" />, whose declared types are still intact.
	///   </para>
	/// </summary>
	private BlockSyntax OptimizeUnrolledHelperBody(BlockSyntax body, ParameterListSyntax parameterList, SyntaxToken methodName)
	{
		return OptimizeInlinedBody(body, new Dictionary<string, VariableItem>(), parameterList, methodName) ?? body;
	}

	/// <summary>
	///   Tries to build a specialized private helper for a static method call where at least one
	///   argument is a compile-time constant. Unlike <see cref="GetInlinedMethodSyntax" /> (one
	///   generic, argument-blind body shared by every call site), this binds the known
	///   argument(s) to their literal values before re-visiting the body, so folds/unrolls that
	///   depend on them (e.g. a loop over a now-known-length array) actually happen. The result is
	///   named after a hash of its own body, so distinct specializations — and repeated identical
	///   ones — get distinct/shared names automatically via the existing <see cref="additionalMethods" />
	///   structural-equality cache.
	/// </summary>
	private MethodDeclarationSyntax? GetSpecializedMethodSyntax(IMethodSymbol targetMethod, IReadOnlyList<ExpressionSyntax> arguments, out IReadOnlyList<ExpressionSyntax> keptArguments)
	{
		keptArguments = arguments;

		try
		{
			var methodSyntax = targetMethod.DeclaringSyntaxReferences
				.Select(r => r.GetSyntax(token))
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault();

			if (methodSyntax?.Body is null || methodSyntax.ParameterList.Parameters.Count != arguments.Count)
			{
				return null;
			}

			var parameters = new Dictionary<string, VariableItem>();
			var isLiteralArgument = new bool[arguments.Count];

			for (var i = 0; i < arguments.Count; i++)
			{
				var paramSyntax = methodSyntax.ParameterList.Parameters[i];
				var paramName = paramSyntax.Identifier.Text;
				var paramType = semanticModel.GetTypeInfo(paramSyntax.Type!).Type ?? semanticModel.Compilation.ObjectType;

				isLiteralArgument[i] = TryGetLiteralValue(arguments[i], out var literalValue);

				parameters[paramName] = isLiteralArgument[i]
					? new VariableItem(paramType, true, literalValue, true) { CanBeInlined = true }
					: new VariableItem(paramType, false, arguments[i], true);
			}

			// Mirrors GenerateExpression's top-level pass: mark single-use locals (e.g. a loop-body
			// temp) as inlinable so the rewriter substitutes them at their one read site instead of
			// leaving a declaration behind — without this, unrolling the loop below can duplicate a
			// same-named local across iterations with only the first copy keeping its `var`.
			var inlineAnalyzer = new InlineVariableAnalyzer(semanticModel, symbolStore);

			foreach (var candidate in inlineAnalyzer.FindInlineCandidates(methodSyntax.Body))
			{
				var name = candidate.Symbol.Name;

				if (parameters.TryGetValue(name, out var existing))
				{
					existing.CanBeInlined = true;
				}
				else
				{
					parameters[name] = new VariableItem(candidate.Symbol.Type, false, null, candidate.Symbol is { NullableAnnotation: NullableAnnotation.Annotated, Type.IsValueType: false }) { CanBeInlined = true };
				}
			}

			visitingMethods?.Add(targetMethod);

			var subRewriter = new ConstExprPartialRewriter(
				semanticModel, loader, (_, _) => { }, parameters,
				additionalMethods, usings, attribute, symbolStore, token, visitingMethods);

			var body = OptimizeInlinedBody(subRewriter.Visit(methodSyntax.Body) as BlockSyntax, parameters, methodSyntax.ParameterList, methodSyntax.Identifier);

			visitingMethods?.Remove(targetMethod);

			if (body is null)
			{
				return null;
			}

			var mods = TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));
			var originalParameters = methodSyntax.ParameterList.Parameters;
			var result = methodSyntax
				.WithIdentifier(Identifier($"{targetMethod.Name}_{body.GetDeterministicHashString()}"))
				.WithBody(body)
				.WithModifiers(mods);

			// A literal argument that got folded away everywhere in the body no longer needs a
			// parameter slot for it — drop it (and the matching argument at the call site) instead of
			// keeping an unused parameter around just because the original method declared one.
			// Non-literal arguments are never dropped even if they read as unused here: the argument
			// expression may carry a side effect that still has to run at the call site.
			var referencedNames = new HashSet<string>(
				body.DescendantNodes().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text),
				StringComparer.Ordinal);

			var keptIndices = Enumerable.Range(0, originalParameters.Count)
				.Where(i => !isLiteralArgument[i] || referencedNames.Contains(originalParameters[i].Identifier.Text))
				.ToArray();

			if (keptIndices.Length != originalParameters.Count)
			{
				result = result.WithParameterList(ParameterList(SeparatedList(keptIndices.Select(i => originalParameters[i]))));
				keptArguments = keptIndices.Select(i => arguments[i]).ToArray();
			}

			return result;
		}
		catch (Exception)
		{
			visitingMethods?.Remove(targetMethod);
			return null;
		}
	}

	/// <summary>
	///   Gets the inlined syntax for a method.
	/// </summary>
	private SyntaxNode? GetInlinedMethodSyntax(IMethodSymbol targetMethod)
	{
		return targetMethod.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax(token))
			.Select<SyntaxNode, SyntaxNode?>(s =>
			{
				var mods = TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));

				switch (s)
				{
					case MethodDeclarationSyntax method:
					{
						var parameters = method.ParameterList.Parameters
							.ToDictionary(d => d.Identifier.Text, d =>
							{
								var type = semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType;

								return new VariableItem(type, false, null, type is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false });
							});

						visitingMethods?.Add(targetMethod);
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, symbolStore, token, visitingMethods);
						var body = visitor.Visit(method.Body) as BlockSyntax;
						visitingMethods?.Remove(targetMethod);

						return method.WithBody(OptimizeInlinedBody(body, parameters, method.ParameterList, method.Identifier)).WithModifiers(mods);
					}
					case LocalFunctionStatementSyntax localFunc:
					{
						var parameters = localFunc.ParameterList.Parameters
							.ToDictionary(d => d.Identifier.Text, d =>
							{
								var type = semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType;

								return new VariableItem(type, false, null, type is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false });
							});

						visitingMethods?.Add(targetMethod);
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, symbolStore, token, visitingMethods);
						var body = visitor.Visit(localFunc.Body) as BlockSyntax;
						visitingMethods?.Remove(targetMethod);

						return localFunc.WithBody(OptimizeInlinedBody(body, parameters, localFunc.ParameterList, localFunc.Identifier)).WithModifiers(mods);
					}
					default:
					{
						return null;
					}
				}
			})
			.FirstOrDefault(f => f is not null);
	}

	/// <summary>
	///   Handles instance method invocation.
	/// </summary>
	private SyntaxNode? HandleInstanceMethodInvocation(InvocationExpressionSyntax node, IMethodSymbol targetMethod, IReadOnlyList<ExpressionSyntax> arguments)
	{
		// try check if method is empty
		if (IsEmptyMethod(targetMethod))
		{
			return null;
		}

		usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());


		// A mutating call's receiver (`values.Add(...)`) must keep its own identity: visiting it
		// normally would inline a fully-known receiver to its current literal value (e.g. `values` ->
		// `[1]`), producing `[1].Add(1)` - a throwaway collection literal that gets mutated and
		// immediately discarded instead of the real, caller-aliased object. Reads have no such
		// concern (a snapshot read and a live read agree on a known value), so only mutating calls on
		// a plain identifier receiver skip the visit.
		var expression = node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax } mutatingReceiverAccess
		                 && IsLikelyMutatingMethod(targetMethod, mutatingReceiverAccess.Name.Identifier.Text)
			? mutatingReceiverAccess
			: Visit(node.Expression) as ExpressionSyntax ?? node.Expression;

		// Handle collection conversion methods
		if (expression is MemberAccessExpressionSyntax { Expression: CollectionExpressionSyntax collection }
		    && targetMethod.IsMethod(typeof(Enumerable), "ToArray", "ToList"))
		{
			return collection;
		}

		// Try to invoke delegate
		var delegateResult = TryInvokeDelegate(node, expression, arguments);

		if (delegateResult is not null)
		{
			return delegateResult;
		}

		return node
			.WithExpression(expression)
			.WithArgumentList(node.ArgumentList
				.WithArguments(ToArgumentList(arguments, node.ArgumentList.Arguments)))
			.WithMethodSymbolAnnotation(targetMethod, symbolStore);
	}

	/// <summary>
	///   Repoints an invocation's callee to a specialized helper name, keeping the receiver
	///   qualification (if any) intact.
	/// </summary>
	private static InvocationExpressionSyntax WithInvocationTargetName(InvocationExpressionSyntax node, string name)
	{
		return node.Expression switch
		{
			IdentifierNameSyntax identifier => node.WithExpression(IdentifierName(name).WithTriviaFrom(identifier)),
			MemberAccessExpressionSyntax memberAccess => node.WithExpression(memberAccess.WithName(IdentifierName(name).WithTriviaFrom(memberAccess.Name))),
			_ => node
		};
	}

	/// <summary>
	///   Rebuilds an argument list from rewritten expressions. When <paramref name="originalArguments" />
	///   lines up 1:1 with <paramref name="arguments" />, each argument's <c>ref</c>/<c>out</c>/<c>in</c>
	///   keyword and name-colon are carried across positionally — otherwise a bare <c>Argument(expr)</c>
	///   is minted, exactly as before. Without this, a call like <c>int.TryParse(s, out value)</c> would
	///   silently lose its <c>out</c>, changing what gets written and making the emitted call invalid C#.
	/// </summary>
	private static SeparatedSyntaxList<ArgumentSyntax> ToArgumentList(IReadOnlyList<ExpressionSyntax> arguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments = default)
	{
		var mappedArguments = new ArgumentSyntax[arguments.Count];
		var preserveRefKind = originalArguments.Count == arguments.Count;

		for (var i = 0; i < arguments.Count; i++)
		{
			mappedArguments[i] = preserveRefKind
				? Argument(originalArguments[i].NameColon, originalArguments[i].RefKindKeyword, arguments[i])
				: Argument(arguments[i]);
		}

		return SeparatedList(mappedArguments);
	}

	/// <summary>
	///   Tries to invoke a delegate.
	/// </summary>
	private SyntaxNode? TryInvokeDelegate(InvocationExpressionSyntax node, ExpressionSyntax expression, IReadOnlyList<ExpressionSyntax> arguments)
	{
		if (!TryGetLiteralValue(expression, out var action) || action is not Delegate @delegate)
		{
			return null;
		}

		var constantArguments = arguments
			.WhereSelect<SyntaxNode?, object?>(TryGetLiteralValue)
			.ToList();

		if (constantArguments.Count != arguments.Count)
		{
			return null;
		}

		var args = constantArguments.ToArray();
		var result = @delegate.DynamicInvoke(args);

		if (@delegate.Method.ReturnType == typeof(void))
		{
			return null;
		}

		if (TryCreateLiteral(result, out var literal))
		{
			if (node.Expression is IdentifierNameSyntax id
			    && variables.TryGetValue(id.Identifier.Text, out var v))
			{
				v.IsAccessed = false;
			}

			return literal;
		}

		return null;
	}

	/// <summary>
	///   Fallback for invocation expressions when symbol is not found.
	/// </summary>
	private SyntaxNode? VisitInvocationExpressionFallback(InvocationExpressionSyntax node)
	{
		return node
			.WithExpression(Visit(node.Expression) as ExpressionSyntax ?? node.Expression)
			.WithArgumentList(VisitArgumentList(node.ArgumentList) as ArgumentListSyntax ?? node.ArgumentList);
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var instance = Visit(node.Expression);

		var arguments = node.ArgumentList.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToList();

		var constantArguments = arguments
			.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
			.ToArray();

		// Per-element folding: when the array has some runtime-written (unknown) elements the whole-array
		// value is no longer foldable (TryGetLiteralValue bails on HasUnknownElements), but elements that
		// were never overwritten with a runtime value still are.
		if (node.Expression is IdentifierNameSyntax { Identifier.Text: var arrName }
		    && variables.TryGetValue(arrName, out var arrVar)
		    && arrVar is { HasValue: true, IsAltered: false } && arrVar.HasUnknownElements
		    && arrVar.Value is Array elemArr
		    && constantArguments is [ int onlyIndex ])
		{
			if (!arrVar.UnknownIndices!.Contains(onlyIndex)
			    && onlyIndex >= 0 && onlyIndex < elemArr.Length
			    && TryCreateLiteral(elemArr.GetValue(onlyIndex), out var elemLiteral))
			{
				return elemLiteral;
			}

			// Runtime (unknown) element or out of range → keep it as a runtime read.
			return node
				.WithExpression(instance as ExpressionSyntax ?? node.Expression)
				.WithArgumentList(node.ArgumentList
					.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
		}

		if (TryGetLiteralValue(node.Expression, out var instanceValue)
		    || TryGetLiteralValue(instance, out instanceValue))
		{
			var result = TryEvaluateElementAccess(node, instanceValue, constantArguments);

			if (result is not null)
			{
				return result;
			}

			if (semanticModel.TryGetSymbol(node, symbolStore, out IPropertySymbol? propertySymbol)
			    && constantArguments.Length == propertySymbol.Parameters.Length)
			{
				if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
				    && TryCreateLiteral(value, out var literal))
				{
					return literal;
				}

				return node
					.WithExpression(instance as ExpressionSyntax ?? node.Expression)
					.WithArgumentList(node.ArgumentList
						.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
			}
		}

		// When the instance resolves to a collection expression (e.g. numbers → [1,2,3,4,5,6])
		// but we cannot fully evaluate the access (non-constant index), fall back to the
		// original expression. A bare collection expression has no target type in element-access
		// position → CS9176. Using the original identifier (e.g. `numbers`) is both valid and
		// avoids an unnecessary heap allocation from `new[]{...}[index]`.
		var effectiveInstance = instance is CollectionExpressionSyntax
			? node.Expression
			: instance as ExpressionSyntax ?? node.Expression;

		return node
			.WithExpression(effectiveInstance)
			.WithArgumentList(node.ArgumentList
				.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
	}

	/// <summary>
	///   Tries to evaluate element access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateElementAccess(ElementAccessExpressionSyntax node, object? instanceValue, object?[] constantArguments)
	{
		if (!TryGetOperation(semanticModel, node, out IOperation? operation))
		{
			return null;
		}

		var type = instanceValue?.GetType();

		return operation switch
		{
			IArrayElementReferenceOperation arrayOp => TryEvaluateArrayAccess(instanceValue as Array, constantArguments, arrayOp.Indices.Length, type),
			// A Span<T>/ReadOnlySpan<T> indexer is an IPropertyReferenceOperation, not an
			// IArrayElementReferenceOperation - but when it was assigned from an already-tracked array
			// (`Span<int> buf = array;`), TryGetLiteralValue resolved instanceValue to that same boxed
			// Array (Span<T> aliases it, it's never copied), so reading through it directly is exactly
			// as sound as reading the array itself. This also sidesteps the reflection path below,
			// which cannot work here anyway: Span<T> is a ref struct, so its GetMethod can't be
			// reflection-invoked with a boxed receiver in the first place.
			IPropertyReferenceOperation { Property.IsIndexer: true } spanProp when instanceValue is Array shadowArray
			                                                                       && constantArguments.Length == spanProp.Arguments.Length
				=> TryEvaluateArrayAccess(shadowArray, constantArguments, spanProp.Arguments.Length, type),
			IPropertyReferenceOperation { Property.IsIndexer: true } propOp when instanceValue is not null
			                                                                     && constantArguments.Length == propOp.Arguments.Length
			                                                                     && loader.TryExecuteMethod(propOp.Property.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
			                                                                     && TryCreateLiteral(value, out var literal) => literal,
			_ => null
		};

	}

	/// <summary>
	///   Tries to evaluate array access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateArrayAccess(Array? arr, object?[] constantArguments, int indicesLength, Type? type)
	{
		if (arr is null || constantArguments.Length != indicesLength)
		{
			return null;
		}

		try
		{
			if (constantArguments.Length == 1)
			{
				var arg = constantArguments[0];

				// Handle System.Range
				if (arg is not null && (arg.GetType().FullName == "System.Range" || arg.GetType().Name == "Range"))
				{
					return TryEvaluateRangeAccess(arr, arg, type);
				}

				// Handle System.Index
				if (arg is not null && (arg.GetType().FullName == "System.Index" || arg.GetType().Name == "Index"))
				{
					return TryEvaluateIndexAccess(arr, arg);
				}
			}

			// Handle integer indices
			if (constantArguments.All(a => a is int))
			{
				var value = arr.GetValue(constantArguments.OfType<int>().ToArray());

				if (TryCreateLiteral(value, out var literal))
				{
					return literal;
				}
			}
			else if (constantArguments.All(a => a is long))
			{
				var value = arr.GetValue(constantArguments.OfType<long>().ToArray());

				if (TryCreateLiteral(value, out var literal))
				{
					return literal;
				}
			}
			// Array indexers also accept a char via its implicit conversion to int (e.g. counts['a']).
			else if (constantArguments is [ char ch ])
			{
				var value = arr.GetValue(ch);

				if (TryCreateLiteral(value, out var literal))
				{
					return literal;
				}
			}
		}
		catch { }

		return null;
	}

	/// <summary>
	///   Tries to evaluate a Range access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateRangeAccess(Array arr, object arg, Type? type)
	{
		var getOffsetAndLength = arg.GetType().GetMethod("GetOffsetAndLength", [ typeof(int) ]);
		var tuple = getOffsetAndLength?.Invoke(arg, [ arr.Length ]);

		if (tuple is null)
		{
			return null;
		}

		var tType = tuple.GetType();
		var item1 = tType.GetField("Item1")?.GetValue(tuple);
		var item2 = tType.GetField("Item2")?.GetValue(tuple);

		if (item1 is int offset && item2 is int length)
		{
			var slice = Array.CreateInstance(type?.GetElementType() ?? typeof(object), length);
			Array.Copy(arr, offset, slice, 0, length);

			if (TryCreateLiteral(slice, out var result))
			{
				return result;
			}
		}

		return null;
	}

	/// <summary>
	///   Tries to evaluate an Index access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateIndexAccess(Array arr, object arg)
	{
		var getOffset = arg.GetType().GetMethod("GetOffset", [ typeof(int) ]);
		var offset = getOffset?.Invoke(arg, [ arr.Length ]);

		if (offset is int idx)
		{
			var value = arr.GetValue(idx);

			if (TryCreateLiteral(value, out var literal))
			{
				return literal;
			}
		}

		return null;
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		semanticModel.TryGetTypeSymbol(node, symbolStore, out var typeSymbol);

		// `Regex.Match(input, pattern).Value` / `Regex.Matches(input, pattern).Count` etc. - when every
		// argument to Match/Matches is a known constant, run the real regex directly (the source generator
		// is itself a normal .NET assembly with access to System.Text.RegularExpressions - no need to go
		// through the interpreter's reflection layer) and fold straight to the accessed property's literal
		// value. MatchFunctionOptimizer/MatchesFunctionOptimizer only cache a compiled Regex as a static
		// field and re-emit the call - they never attempt this even when the input is also fully known, so
		// without this check `Regex.Match("1234", @"^\d+$").Value` stays an unfolded runtime call forever.
		if (TryFoldRegexPropertyAccess(node, out var regexLiteral))
		{
			return regexLiteral;
		}

		var expression = Visit(node.Expression);

		if (expression is ThrowExpressionSyntax)
		{
			return expression;
		}

		// `new Lookup_xxx().Count` (ToLookupFunctionOptimizer's compile-time-generated ILookup struct) -
		// node's real symbol resolves fine (it's the original tree's genuine ILookup<TKey,TElement>.Count
		// property), but the instance is a struct this same generator pass is still emitting, so neither
		// the semantic model nor reflection can ever construct/execute against it below. The optimizer
		// stashes the known group count directly on the object-creation node; recover it here first.
		if (node.Name.Identifier.Text == "Count"
		    && expression is ObjectCreationExpressionSyntax
		    && expression.TryGetLookupCountAnnotation(out var lookupCount))
		{
			return CreateLiteral(lookupCount);
		}

		// Distribute the member access into both branches of an otherwise-unresolvable ternary
		// receiver, e.g. `(cond ? a : b).Length` -> `cond ? a.Length : b.Length`. If a ternary
		// reaches here at all its condition must be unknown (a resolvable one would already have
		// collapsed in VisitConditionalExpression), so this never duplicates evaluation of `cond`.
		// A literal branch (like `""`) is folded via plain reflection on its runtime value rather
		// than through the semantic model: this member-access node was just synthesized (its
		// receiver didn't exist in the source), so it has no symbol of its own for the model to
		// resolve, but we already have the concrete value to read the property off of directly.
		if (expression is ExpressionSyntax expressionForUnwrap && UnwrapParens(expressionForUnwrap) is ConditionalExpressionSyntax conditional)
		{
			ExpressionSyntax BuildBranch(ExpressionSyntax branch)
			{
				if (TryGetLiteralValue(branch, out var branchValue)
				    && branchValue?.GetType().GetProperty(node.Name.Identifier.Text) is { } propertyInfo
				    && TryCreateLiteral(propertyInfo.GetValue(branchValue), out var branchLiteral))
				{
					return branchLiteral;
				}

				return MemberAccessExpression(node.Kind(), ParenthesizeIfNeeded(branch), node.Name);
			}

			return Visit(ConditionalExpression(conditional.Condition, BuildBranch(conditional.WhenTrue), BuildBranch(conditional.WhenFalse)));
		}

		var hasLiteral = TryGetLiteralValue(node.Expression, out var instanceValue) || TryGetLiteralValue(expression, out instanceValue);

		// `SomeStruct.Factory(consts).Property` - a static factory method returning a value that is not
		// itself literal-representable (e.g. `DateOnly.FromDayNumber(738946)`), whose result then has a
		// property read. The invocation folded to nothing in `Visit(node.Expression)` above because
		// TryCreateLiteral can't render a `DateOnly`, and TryGetLiteralValue has no invocation case, so
		// `instanceValue` is still null here. Re-run the static call reflectively (constant args only, so
		// it's side-effect-free on tracked state - no MarkStateMutated needed) to recover the concrete
		// instance the member access below reads off of.
		if (!hasLiteral
		    && node.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } staticFactoryInvocation
		    && semanticModel.TryGetSymbol(staticFactoryInvocation, symbolStore, out IMethodSymbol? factoryMethod)
		    && factoryMethod is { IsStatic: true, ReturnsVoid: false })
		{
			var factoryArguments = new List<object>(staticFactoryInvocation.ArgumentList.Arguments.Count);

			foreach (var factoryArgument in staticFactoryInvocation.ArgumentList.Arguments)
			{
				if (TryGetLiteralValue(Visit(factoryArgument.Expression), out var factoryArgumentValue)
				    || TryGetLiteralValue(factoryArgument.Expression, out factoryArgumentValue))
				{
					factoryArguments.Add(factoryArgumentValue);
				}
			}

			if (factoryArguments.Count == factoryMethod.Parameters.Length
			    && loader.TryExecuteMethod(factoryMethod, null, new VariableItemDictionary(variables), factoryArguments, out var factoryResult)
			    && factoryResult is not null)
			{
				instanceValue = factoryResult;
				hasLiteral = true;
			}
		}

		if (semanticModel.TryGetSymbol(node, symbolStore, out ISymbol? symbol))
		{
			var result = TryEvaluateMemberAccess(symbol, instanceValue);

			if (result is not null)
			{
				return result;
			}

			// check if symbol is IList.Count
			if (symbol is IPropertySymbol propertySymbol)
			{
				var isListCount = propertySymbol.Name == "Count"
				                  && propertySymbol.ContainingType.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T or SpecialType.System_Collections_Generic_IList_T);

				var isArrayLength = propertySymbol is { Name: "Length", ContainingType.SpecialType: SpecialType.System_Array };

				if ((isListCount || isArrayLength)
				    && expression is CollectionExpressionSyntax collectionExpression)
				{
					return CreateLiteral(collectionExpression.Elements.Count);
				}

				// Array.Length is immutable regardless of element mutations, so bypass IsAltered
				if (isArrayLength
				    && node.Expression is IdentifierNameSyntax arrayIdentifier
				    && variables.TryGetValue(arrayIdentifier.Identifier.Text, out var arrayVar)
				    && arrayVar.Value is Array storedArray)
				{
					return CreateLiteral(storedArray.Length);
				}

				if (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.Text: { } methodName }, Expression: { } resultExpression } })
				{
					if (isListCount)
					{
						switch (methodName)
						{
							// check if expression is Enumerable.ToList()
							case "ToList" when semanticModel.TryGetTypeSymbol(node.Expression, symbolStore, out var collectionType)
							                   && collectionType is INamedTypeSymbol namedTypeSymbol:
							{
								// return Enumerable.Count() instead
								var newInvocation = InvocationExpression(
										MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											resultExpression,
											IdentifierName("Count")))
									.WithArgumentList(ArgumentList());

								var optimized = TryOptimizeLinqMethod(semanticModel, semanticModel.Compilation
									.GetTypeByMetadataName("System.Linq.Enumerable")!
									.GetMembers("Count")
									.OfType<IMethodSymbol>()
									.Select(s => s.Construct(namedTypeSymbol.TypeArguments[0]))
									.First(m => m.Parameters.Length == 1), newInvocation, [ ], [ ]);

								return optimized ?? newInvocation;
							}
							case "ToHashSet" when semanticModel.TryGetTypeSymbol(node.Expression, symbolStore, out var collectionType)
							                      && collectionType is INamedTypeSymbol namedTypeSymbol:
							{
								// return Enumerable.Distinct().Count() instead
								var distinctInvocation = InvocationExpression(
										MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											resultExpression,
											IdentifierName("Distinct")))
									.WithArgumentList(ArgumentList());

								var optimizedDistinct = TryOptimizeLinqMethod(semanticModel, semanticModel.Compilation
									.GetTypeByMetadataName("System.Linq.Enumerable")!
									.GetMembers("Count")
									.OfType<IMethodSymbol>()
									.Select(s => s.Construct(namedTypeSymbol.TypeArguments[0]))
									.First(m => m.Parameters.Length == 1), distinctInvocation, [ ], [ ]);

								var countInvocation = InvocationExpression(
										MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											optimizedDistinct as ExpressionSyntax ?? distinctInvocation,
											IdentifierName("Count")))
									.WithArgumentList(ArgumentList());

								var optimized = TryOptimizeLinqMethod(semanticModel, semanticModel.Compilation
									.GetTypeByMetadataName("System.Linq.Enumerable")!
									.GetMembers("Count")
									.OfType<IMethodSymbol>()
									.Select(s => s.Construct(namedTypeSymbol.TypeArguments[0]))
									.First(m => m.Parameters.Length == 1), countInvocation, [ ], [ ]);

								return optimized ?? countInvocation;
							}
						}
					}

					if (isArrayLength
					    && methodName == nameof(Enumerable.ToArray)
					    && semanticModel.TryGetTypeSymbol(node.Expression, symbolStore, out typeSymbol)
					    && typeSymbol is IArrayTypeSymbol arrayType)
					{
						// return Enumerable.Count() instead
						var newInvocation = InvocationExpression(
								MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									resultExpression,
									IdentifierName("Count")))
							.WithArgumentList(ArgumentList());

						var optimized = TryOptimizeLinqMethod(semanticModel, semanticModel.Compilation
							.GetTypeByMetadataName("System.Linq.Enumerable")!
							.GetMembers("Count")
							.OfType<IMethodSymbol>()
							.Select(s => s.Construct(arrayType.ElementType))
							.First(m => m.Parameters.Length == 1), newInvocation, [ ], [ ]);

						return optimized ?? newInvocation;
					}
				}
			}
		}

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	/// <summary>
	///   Folds `Regex.Match(input, pattern[, options]).SomeProperty` / `Regex.Matches(input, pattern[, options]).SomeProperty`
	///   to a literal when every Match/Matches argument is a known constant, by actually running the regex
	///   (the source generator itself is a normal .NET assembly - no interpreter reflection needed) and
	///   reading the accessed property off the real result object. Only folds when the property's value is
	///   itself literal-representable (e.g. Value/Success/Index/Length/Count) - a property like Groups isn't,
	///   and TryCreateLiteral failing is exactly the signal to leave the member access for normal processing.
	/// </summary>
	private bool TryFoldRegexPropertyAccess(MemberAccessExpressionSyntax node, out ExpressionSyntax? literal)
	{
		literal = null;

		if (node.Expression is not InvocationExpressionSyntax regexInvocation
		    || !semanticModel.TryGetSymbol(regexInvocation, symbolStore, out IMethodSymbol? regexMethod)
		    || regexMethod.ContainingType.ToDisplayString() != "System.Text.RegularExpressions.Regex"
		    || regexMethod.Name is not (nameof(Regex.Match) or nameof(Regex.Matches)))
		{
			return false;
		}

		var arguments = regexMethod.IsStatic
			? regexInvocation.ArgumentList.Arguments
			: default;

		if (arguments.Count is not (2 or 3)
		    || !TryGetLiteralValue(arguments[0].Expression, out var inputValue) || inputValue is not string input
		    || !TryGetLiteralValue(arguments[1].Expression, out var patternValue) || patternValue is not string pattern)
		{
			return false;
		}

		var options = RegexOptions.None;

		if (arguments.Count == 3)
		{
			if (!TryGetLiteralValue(arguments[2].Expression, out var optionsValue) || optionsValue is not RegexOptions o)
			{
				return false;
			}

			options = o;
		}

		try
		{
			var regex = new Regex(pattern, options);
			object? result = regexMethod.Name == nameof(Regex.Match)
				? regex.Match(input)
				: regex.Matches(input);

			var propertyValue = result?.GetType().GetProperty(node.Name.Identifier.Text)?.GetValue(result);

			return TryCreateLiteral(propertyValue, out literal);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	///   Tries to evaluate member access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateMemberAccess(ISymbol symbol, object? instanceValue)
	{
		switch (symbol)
		{
			case IFieldSymbol fieldSymbol:
			{
				if (fieldSymbol.ContainingType.EnumUnderlyingType is not null)
				{
					return null;
				}

				if (loader.TryGetFieldValue(fieldSymbol, instanceValue, out var value)
				    && TryCreateLiteral(value, out var literal))
				{
					return literal;
				}
				break;
			}

			case IPropertySymbol { Parameters.Length: 0 } propertySymbol:
			{
				if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [ ], out var value)
				    && TryCreateLiteral(value, out var literal))
				{
					return literal;
				}
				break;
			}
		}

		return null;
	}

	public override SyntaxNode? VisitArgument(ArgumentSyntax node)
	{
		// A ref/out argument must remain an lvalue - Visit() can fold a tracked variable straight to
		// a literal (e.g. `-2`, itself a PrefixUnaryExpressionSyntax negation, not even a plain
		// LiteralExpressionSyntax - a "did it become a literal" check misses it too), which is not
		// valid C# in an out/ref slot and silently breaks the by-ref write the call depends on.
		// Always keep the original lvalue for these instead of visiting at all.
		if (node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword, SyntaxKind.OutKeyword))
		{
			return node;
		}

		var expression = Visit(node.Expression);

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
	{
		return node.WithArguments(VisitList(node.Arguments));
	}

	public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
	{
		usings.Add(node.Left.ToString());
		return node.Right;
	}
}