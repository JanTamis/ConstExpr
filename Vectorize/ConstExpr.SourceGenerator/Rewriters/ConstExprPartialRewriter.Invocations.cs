using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

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

		if (!semanticModel.TryGetSymbol(node, out IMethodSymbol? targetMethod))
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
			var optimized = TryOptimizeStringMethod(targetMethod, node, memberAccess, arguments);

			if (optimized is not null)
			{
				return optimized;
			}
		}
		// Try math optimizers
		else if (attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			var optimized = TryOptimizeMathMethod(targetMethod, node, arguments);

			if (optimized is not null)
			{
				return optimized;
			}
		}

		// Try linq optimizers
		if (TryOptimizeLinqMethod(targetMethod, node, arguments) is { } optimizedLinq)
		{
			return Visit(optimizedLinq);
		}

		// Handle char overload conversion
		arguments = ConvertToCharOverloadIfNeeded(targetMethod, arguments);

		// Handle static methods and local functions
		if (targetMethod.IsStatic || targetMethod.MethodKind == MethodKind.LocalFunction)
		{
			return HandleStaticMethodInvocation(node, targetMethod, arguments);
		}

		return HandleInstanceMethodInvocation(node, targetMethod, arguments);
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

		if (semanticModel.TryGetSymbol(arg, out ISymbol? sym))
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

		for (var i = 0; i < arguments.Count; i++)
		{
			if (TryGetLiteralValue(arguments[i], out var value) || TryGetLiteralValue(originalArguments[i], out value))
			{
				constantArguments.Add(value);
			}
		}

		return constantArguments;
	}

	/// <summary>
	/// Tries to execute a method with constant arguments.
	/// </summary>
	private SyntaxNode? TryExecuteWithConstantArguments(InvocationExpressionSyntax node, IMethodSymbol targetMethod, List<object> constantArguments)
	{
		if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName }
		    && !targetMethod.ContainingType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
		{
			return TryExecuteInstanceMethod(node, targetMethod, instanceName, constantArguments);
		}

		return TryExecuteViaOperationVisitor(targetMethod, constantArguments);
	}

	/// <summary>
	/// Tries to execute an instance method.
	/// </summary>
	private SyntaxNode? TryExecuteInstanceMethod(InvocationExpressionSyntax node, IMethodSymbol targetMethod, ExpressionSyntax instanceName, List<object> constantArguments)
	{
		var hasLiteral = TryGetLiteralValue(instanceName, out var instance) || TryGetLiteralValue(Visit(instanceName), out instance);

		if (hasLiteral)
		{
			try
			{
				instance = Convert.ChangeType(instance, loader.GetType(targetMethod.ContainingType));
			}
			catch (InvalidCastException) { }
		}

		if ((targetMethod.IsStatic || hasLiteral
			    && (instanceName is not IdentifierNameSyntax identifier || CanBePruned(identifier.Identifier.Text)))
		    && loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value)
		    && TryGetLiteral(value, out var literal))
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

		var visitor = new Visitors.ConstExprOperationVisitor(semanticModel.Compilation, loader, (_, _) => { }, token);

		switch (methodOperation)
		{
			case ILocalFunctionOperation { Body: not null } localFunction:
				visitor.VisitBlock(localFunction.Body, vars);
				break;
			case IMethodBodyOperation { BlockBody: not null } methodBody:
				visitor.VisitBlock(methodBody.BlockBody, vars);
				break;
		}

		if (TryGetLiteral(vars[Visitors.ConstExprOperationVisitor.RETURNVARIABLENAME], out var result))
		{
			return result;
		}

		return null;
	}

	/// <summary>
	/// Tries to optimize a string method.
	/// </summary>
	private SyntaxNode? TryOptimizeStringMethod(IMethodSymbol targetMethod, InvocationExpressionSyntax node, MemberAccessExpressionSyntax memberAccess, List<SyntaxNode> arguments)
	{
		var instance = Visit(memberAccess.Expression);

		var optimizers = _stringOptimizers.Value
			.Select(s => Activator.CreateInstance(s, instance) as BaseStringFunctionOptimizer)
			.Where(o => string.Equals(o?.Name, targetMethod.Name, StringComparison.Ordinal));

		foreach (var stringOptimizer in optimizers)
		{
			if (stringOptimizer!.TryOptimize(targetMethod, node, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out var optimized))
			{
				return optimized;
			}
		}

		if (targetMethod.IsStatic)
		{
			return node.WithExpression(memberAccess.WithExpression(ParseTypeName(targetMethod.ContainingType.Name)))
				.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
		}

		return null;
	}

	/// <summary>
	/// Tries to optimize a math method.
	/// </summary>
	private SyntaxNode? TryOptimizeMathMethod(IMethodSymbol targetMethod, InvocationExpressionSyntax node, List<SyntaxNode> arguments)
	{
		return _mathOptimizers.Value
			.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
			            && o.ParameterCounts.Contains(targetMethod.Parameters.Length))
			.WhereSelect<BaseMathFunctionOptimizer, SyntaxNode>((w, out optimized) => w.TryOptimize(targetMethod, node, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out optimized))
			.FirstOrDefault();
	}

	/// <summary>
	/// Tries to optimize a linq method.
	/// </summary>
	private SyntaxNode? TryOptimizeLinqMethod(IMethodSymbol targetMethod, InvocationExpressionSyntax node, List<SyntaxNode> arguments)
	{
		return _linqOptimizers.Value
			.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
			            && o.ParameterCounts.Contains(targetMethod.Parameters.Length))
			.WhereSelect<BaseLinqFunctionOptimizer, SyntaxNode>((w, out optimized) => w.TryOptimize(targetMethod, node, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out optimized))
			.FirstOrDefault();
	}

	/// <summary>
	/// Converts arguments to char if there's a char overload available.
	/// </summary>
	private List<SyntaxNode> ConvertToCharOverloadIfNeeded(IMethodSymbol targetMethod, List<SyntaxNode> arguments)
	{
		var hasCharOverload = attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath
		                      && TryGetCharOverload(targetMethod, arguments, out _);

		if (!hasCharOverload)
		{
			return arguments;
		}

		return arguments
			.Select(s =>
			{
				if (TryGetLiteralValue(s, out var value) && value is string { Length: 1 } charValue)
				{
					return LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(charValue[0]));
				}

				return s;
			})
			.ToList();
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

			return node.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
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

		return node.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
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
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, token, visitingMethods);
						var body = visitor.Visit(method.Body) as BlockSyntax;
						visitingMethods?.Remove(targetMethod);

						return method.WithBody(body).WithModifiers(mods);
					}
					case LocalFunctionStatementSyntax localFunc:
					{
						var parameters = localFunc.ParameterList.Parameters
							.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

						visitingMethods?.Add(targetMethod);
						var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, token, visitingMethods);
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
			.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
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

		if (TryGetLiteral(result, out var literal))
		{
			if (node.Expression is IdentifierNameSyntax id && variables.TryGetValue(id.Identifier.Text, out var v))
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

			if (semanticModel.TryGetSymbol(node, out IPropertySymbol? propertySymbol)
			    && constantArguments.Length == propertySymbol.Parameters.Length)
			{
				if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
				    && TryGetLiteral(value, out var literal))
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
			                                                                     && TryGetLiteral(value, out var literal) => literal,
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

				if (TryGetLiteral(value, out var literal))
				{
					return literal;
				}
			}
			else if (constantArguments.All(a => a is long))
			{
				var value = arr.GetValue(constantArguments.OfType<long>().ToArray());

				if (TryGetLiteral(value, out var literal))
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

			if (TryGetLiteral(slice, out var result))
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

			if (TryGetLiteral(value, out var literal))
			{
				return literal;
			}
		}

		return null;
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		var hasLiteral = TryGetLiteralValue(node.Expression, out var instanceValue);

		if (!hasLiteral)
		{
			hasLiteral = TryGetLiteralValue(expression, out instanceValue);
		}

		if (semanticModel.TryGetSymbol(node, out ISymbol? symbol))
		{
			var result = TryEvaluateMemberAccess(symbol, instanceValue);

			if (result is not null)
			{
				return result;
			}
		}

		if (hasLiteral && instanceValue != null && TryGetLiteral(instanceValue, out var instanceLiteral))
		{
			return node.WithExpression(instanceLiteral);
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
				    && TryGetLiteral(value, out var literal))
				{
					return literal;
				}
				break;

			case IPropertySymbol { Parameters.Length: 0 } propertySymbol:
				if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [ ], out value)
				    && TryGetLiteral(value, out literal))
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