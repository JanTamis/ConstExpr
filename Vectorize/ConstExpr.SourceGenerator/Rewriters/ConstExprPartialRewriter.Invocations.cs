using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using ExpressionVisitor = ConstExpr.SourceGenerator.Visitors.ExpressionVisitor;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Invocation and member access visitor methods for the ConstExprPartialRewriter.
/// Handles method invocations, element access, and member access expressions.
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
			return VisitInvocationExpressionFallback(node);
		}

		var arguments = node.ArgumentList.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToList();

		var constantArguments = ExtractConstantArguments(arguments, node.ArgumentList.Arguments);

		// Try to execute with all constant arguments
		if (constantArguments.Count == targetMethod.Parameters.Length)
		{
			var result = TryExecuteWithConstantArguments(node, targetMethod, constantArguments);

			if (result is not null)
			{
				return result;
			}
		}

		// Try string optimizers
		if (targetMethod.ContainingType.SpecialType == SpecialType.System_String
		    && node.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			var tempNode = node.WithExpression(Visit(node.Expression) as ExpressionSyntax ?? node.Expression);
			var optimized = TryOptimizeStringMethod(semanticModel, targetMethod, tempNode, memberAccess, arguments, node.ArgumentList.Arguments.Select(s => s.Expression));

			if (optimized is not null)
			{
				return Visit(optimized);
			}
		}
		// Try math optimizers
		else if (attribute.MathOptimizations.HasFlag(FastMathFlags.NoNaN))
		{
			var tempNode = node.WithExpression(Visit(node.Expression) as ExpressionSyntax ?? node.Expression);
			var optimized = TryOptimizeMathMethod(semanticModel, targetMethod, tempNode, arguments, node.ArgumentList.Arguments.Select(s => s.Expression));

			if (optimized is not null)
			{
				return Visit(optimized);
			}
		}

		if (TryOptimizeSimdMethod(semanticModel, targetMethod, node, arguments, node.ArgumentList.Arguments.Select(s => s.Expression)) is { } optimizedSimd)
		{
			return Visit(optimizedSimd);
		}

		if (TryOptimizeRegexMethod(semanticModel, targetMethod, node, arguments, node.ArgumentList.Arguments.Select(s => s.Expression)) is { } optimizedRegex)
		{
			return optimizedRegex;
		}

		// Try LINQ optimizers (for inner calls, or when unrolling was skipped).
		// The optimized result is annotated with symbol info so it can be unrolled
		// when it re-enters the rewriter through Visit.
		if (attribute.LinqOptimisationMode != LinqOptimisationMode.None)
		{
			if (TryOptimizeLinqMethod(semanticModel, targetMethod, node, arguments, node.ArgumentList.Arguments.Select(s => s.Expression)) is { } optimizedLinq)
			{
				if (attribute.LinqOptimisationMode == LinqOptimisationMode.Unroll
				    && LinqUnroller.TryUnrollLinqChain(optimizedLinq, Visit, semanticModel, additionalMethods, symbolStore, out var unrolled))
				{
					return unrolled;
				}

				return optimizedLinq;
			}

			if (attribute.LinqOptimisationMode == LinqOptimisationMode.Unroll
			    && LinqUnroller.TryUnrollLinqChain(node, Visit, semanticModel, additionalMethods, symbolStore, out var unrolledNode))
			{
				return unrolledNode;
			}
		}

		node = node.WithExpression(Visit(node.Expression) as ExpressionSyntax ?? node.Expression);

		// Handle char overload conversion
		if (ConvertToCharOverloadIfNeeded(targetMethod, arguments, out var newArguments, out var charMethod))
		{
			targetMethod = charMethod;
			arguments = newArguments;
		}

		// Handle static methods and local functions
		return targetMethod.IsStatic || targetMethod.MethodKind == MethodKind.LocalFunction
			? HandleStaticMethodInvocation(node, targetMethod, arguments)
			: HandleInstanceMethodInvocation(node, targetMethod, arguments);

	}

	/// <summary>
	/// Tries to handle nameof(...) expressions.
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
			result = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(name));
			return true;
		}

		result = base.VisitInvocationExpression(node);
		return true;
	}

	/// <summary>
	/// Extracts constant arguments from visited arguments.
	/// </summary>
	private List<object> ExtractConstantArguments(List<SyntaxNode> arguments, SeparatedSyntaxList<ArgumentSyntax> originalArguments)
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
	/// Tries to execute a method with constant arguments.
	/// </summary>
	private SyntaxNode? TryExecuteWithConstantArguments(InvocationExpressionSyntax node, IMethodSymbol targetMethod, List<object> constantArguments)
	{
		try
		{
			if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName }
			    && !targetMethod.ContainingType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
			{
				return TryExecuteInstanceMethod(targetMethod, instanceName, constantArguments)
					.WithTypeSymbolAnnotation(targetMethod.ReturnType, symbolStore);
			}

			return TryExecuteViaOperationVisitor(targetMethod, constantArguments)
				.WithTypeSymbolAnnotation(targetMethod.ReturnType, symbolStore);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Tries to execute an instance method.
	/// </summary>
	private SyntaxNode? TryExecuteInstanceMethod(IMethodSymbol targetMethod, ExpressionSyntax instanceName, List<object> constantArguments)
	{
		var hasLiteral = TryGetLiteralValue(instanceName, out var instance)
		                 || TryGetLiteralValue(Visit(instanceName), out instance);

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
		    && loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value)
		    && TryCreateLiteral(value, out var literal))
		{
			if (targetMethod.ReturnsVoid)
			{
				return null;
			}

			return literal;
		}

		return null;
	}

	/// <summary>
	/// Tries to execute a method via the operation visitor.
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
				visitor.VisitBlock(localFunction.Body, vars);
				break;
			case IMethodBodyOperation { BlockBody: not null } methodBody:
				visitor.VisitBlock(methodBody.BlockBody, vars);
				break;
		}

		if (TryCreateLiteral(vars[ConstExprOperationVisitor.RETURNVARIABLENAME], out var result))
		{
			return result;
		}

		return null;
	}

	/// <summary>
	/// Tries to optimize a string method.
	/// </summary>
	private SyntaxNode? TryOptimizeStringMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, MemberAccessExpressionSyntax memberAccess, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
	{
		var instance = Visit(memberAccess.Expression);

		var optimizers = _stringOptimizers.Value
			.Select(s => Activator.CreateInstance(s, instance) as BaseStringFunctionOptimizer)
			.Where(o => string.Equals(o?.Name, targetMethod.Name, StringComparison.Ordinal));

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
			return node.WithExpression(memberAccess.WithExpression(ParseTypeName(targetMethod.ContainingType.Name)))
				.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(visitedArguments.OfType<ExpressionSyntax>().Select(Argument))));
		}

		return null;
	}

	/// <summary>
	/// Tries to optimize a math method.
	/// </summary>
	private SyntaxNode? TryOptimizeMathMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
	{
		var type = targetMethod.Parameters
			.Select(s => s.Type)
			.FirstOrDefault();

		if (targetMethod.ContainingType.ToString() is not ("System.Math" or "System.MathF")
		    && (!targetMethod.ContainingType.EqualsType(type)
		        || !type.IsNumericType()))
		{
			return null;
		}


		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		return _mathOptimizers.Value
			.WhereSelect<BaseMathFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

	}

	/// <summary>
	/// Tries to optimize a linq method.
	/// </summary>
	private SyntaxNode? TryOptimizeLinqMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
	{
		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _linqOptimizers.Value
			.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
			            && o.IsValidParameterCount(targetMethod.Parameters.Length))
			.WhereSelect<BaseLinqFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

		return result;
	}

	private SyntaxNode? TryOptimizeSimdMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
	{
		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _simdOptimizers.Value
			.WhereSelect<BaseSimdFunctionOptimizer, SyntaxNode>((optimizer, out optimized) => optimizer.TryOptimize(context, out optimized))
			.FirstOrDefault();

		return result;
	}

	/// <summary>
	/// Tries to optimize a Regex method (e.g. Regex.IsMatch) by converting the constant pattern to inline C# code.
	/// </summary>
	private SyntaxNode? TryOptimizeRegexMethod(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
	{
		if (targetMethod.ContainingType?.ToString() != "System.Text.RegularExpressions.Regex")
		{
			return null;
		}

		var context = GetFunctionOptimizerContext(model, targetMethod, node, visitedArguments, originalArguments);

		var result = _regexOptimizers.Value
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

	private FunctionOptimizerContext GetFunctionOptimizerContext(SemanticModel model, IMethodSymbol targetMethod, InvocationExpressionSyntax node, IEnumerable<SyntaxNode> visitedArguments, IEnumerable<SyntaxNode> originalArguments)
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

		return new FunctionOptimizerContext(model, 
			loader, 
			targetMethod, 
			node, 
			visitedArguments.OfType<ExpressionSyntax>().ToArray(), 
			originalArguments.OfType<ExpressionSyntax>().ToArray(), 
			x => Visit(x) as ExpressionSyntax,
			x => Visit(x) as StatementSyntax, 
			getLambda, 
			optimizeBinaryExpression, 
			additionalMethods, 
			variables, 
			usings, 
			attribute.MathOptimizations,
			symbolStore);
	}

	/// <summary>
	/// Converts arguments to char if there's a char overload available.
	/// </summary>
	private bool ConvertToCharOverloadIfNeeded(IMethodSymbol targetMethod, List<SyntaxNode> arguments, [NotNullWhen(true)] out List<SyntaxNode>? newArguments, [NotNullWhen(true)] out IMethodSymbol? charMethod)
	{
		if (attribute.MathOptimizations == FastMathFlags.Strict
		    || !TryGetCharOverload(targetMethod, arguments, out charMethod))
		{
			charMethod = null;
			newArguments = null;
			return false;
		}

		newArguments = arguments
			.Select(s =>
			{
				if (TryGetLiteralValue(s, out var value) && value is string { Length: 1 } charValue)
				{
					return LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(charValue[0]));
				}

				return s;
			})
			.ToList();

		return true;
	}

	/// <summary>
	/// Handles static method invocation.
	/// </summary>
	private SyntaxNode? HandleStaticMethodInvocation(InvocationExpressionSyntax node, IMethodSymbol targetMethod, List<SyntaxNode> arguments)
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
				.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))))
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
			.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))))
			.WithMethodSymbolAnnotation(targetMethod, symbolStore);
	}

	/// <summary>
	/// Gets the inlined syntax for a method.
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
							.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

						visitingMethods?.Add(targetMethod);
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, symbolStore, token, visitingMethods);
						var body = visitor.Visit(method.Body) as BlockSyntax;
						visitingMethods?.Remove(targetMethod);

						return method.WithBody(body).WithModifiers(mods);
					}
					case LocalFunctionStatementSyntax localFunc:
					{
						var parameters = localFunc.ParameterList.Parameters
							.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

						visitingMethods?.Add(targetMethod);
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, symbolStore, token, visitingMethods);
						var body = visitor.Visit(localFunc.Body) as BlockSyntax;
						visitingMethods?.Remove(targetMethod);

						return localFunc.WithBody(body).WithModifiers(mods);
					}
					default:
						return null;
				}
			})
			.FirstOrDefault(f => f is not null);
	}

	/// <summary>
	/// Handles instance method invocation.
	/// </summary>
	private SyntaxNode? HandleInstanceMethodInvocation(InvocationExpressionSyntax node, IMethodSymbol targetMethod, List<SyntaxNode> arguments)
	{
		// try check if method is empty
		if (IsEmptyMethod(targetMethod))
		{
			return null;
		}

		usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());

		// Mark variable as altered since instance method may mutate it
		if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax identifierName }
		    && variables.TryGetValue(identifierName.Identifier.Text, out var variable))
		{
			variable.IsAltered = true;
		}

		var expression = Visit(node.Expression) as ExpressionSyntax ?? node.Expression;

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
				.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))))
			.WithMethodSymbolAnnotation(targetMethod, symbolStore);
	}

	/// <summary>
	/// Tries to invoke a delegate.
	/// </summary>
	private SyntaxNode? TryInvokeDelegate(InvocationExpressionSyntax node, ExpressionSyntax expression, List<SyntaxNode?> arguments)
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
	/// Fallback for invocation expressions when symbol is not found.
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

		return base.VisitElementAccessExpression(node);
	}

	/// <summary>
	/// Tries to evaluate element access at compile time.
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
			IPropertyReferenceOperation { Property.IsIndexer: true } propOp when instanceValue is not null
			                                                                     && constantArguments.Length == propOp.Arguments.Length
			                                                                     && loader.TryExecuteMethod(propOp.Property.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
			                                                                     && TryCreateLiteral(value, out var literal) => literal,
			_ => null
		};

	}

	/// <summary>
	/// Tries to evaluate array access at compile time.
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
		}
		catch { }

		return null;
	}

	/// <summary>
	/// Tries to evaluate a Range access at compile time.
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
	/// Tries to evaluate an Index access at compile time.
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

		var expression = Visit(node.Expression);
		var hasLiteral = TryGetLiteralValue(node.Expression, out var instanceValue);

		if (!hasLiteral)
		{
			hasLiteral = TryGetLiteralValue(expression, out instanceValue);
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
	/// Tries to evaluate member access at compile time.
	/// </summary>
	private SyntaxNode? TryEvaluateMemberAccess(ISymbol symbol, object? instanceValue)
	{
		switch (symbol)
		{
			case IFieldSymbol fieldSymbol:
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

			case IPropertySymbol { Parameters.Length: 0 } propertySymbol:
				if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [ ], out value)
				    && TryCreateLiteral(value, out literal))
				{
					return literal;
				}
				break;
		}

		return null;
	}

	public override SyntaxNode? VisitArgument(ArgumentSyntax node)
	{
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