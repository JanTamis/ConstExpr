using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Rewriters;

public class ExpressionRewriter(
	SemanticModel semanticModel, 
	MetadataLoader loader, 
	Action<SyntaxNode?, Exception> exceptionHandler, 
	IDictionary<string, VariableItem> variables, 
	IDictionary<string, ParameterExpression> parameters, 
	CancellationToken token,
	IDictionary<SyntaxNode, bool>? additionalMethods = null,
	ISet<string>? usings = null,
	ConstExprAttribute? attribute = null,
	HashSet<IMethodSymbol>? visitingMethods = null) : CSharpSyntaxVisitor<Expression?>
{
	private readonly IDictionary<SyntaxNode, bool> additionalMethods = additionalMethods ?? new Dictionary<SyntaxNode, bool>();
	private readonly ISet<string> usings = usings ?? new HashSet<string>();
	private readonly ConstExprAttribute attribute = attribute ?? new ConstExprAttribute();
	private readonly HashSet<IMethodSymbol> visitingMethods = visitingMethods ?? new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

	public override Expression? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception ex)
		{
			exceptionHandler(node, ex);
			return null;
		}
	}

	/// <summary>
	/// Try to get literal value from a syntax node, supporting variables and constants
	/// </summary>
	private bool TryGetLiteralValue(SyntaxNode? node, out object? value)
	{
		return TryGetLiteralValue(node, out value, new HashSet<string>());
	}

	private bool TryGetLiteralValue(SyntaxNode? node, out object? value, HashSet<string> visitedVariables)
	{
		switch (node)
		{
			case LiteralExpressionSyntax { Token.Value: var v }:
				value = v;
				return true;
			case IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue:
				// Prevent infinite recursion from circular variable references
				if (!visitedVariables.Add(identifier.Identifier.Text))
				{
					value = null;
					return false;
				}

				if (variable.Value is SyntaxNode sn)
				{
					return TryGetLiteralValue(sn, out value, visitedVariables);
				}

				value = variable.Value;
				return true;
			// unwrap ( ... )
			case ParenthesizedExpressionSyntax paren:
				return TryGetLiteralValue(paren.Expression, out value, visitedVariables);
			default:
				value = null;
				return false;
		}
	}

	/// <summary>
	/// Create a literal expression from a value
	/// </summary>
	private ExpressionSyntax? CreateLiteral(object? value)
	{
		if (TryGetLiteral(value, out var expression))
		{
			return expression;
		}
		return null;
	}

	public override Expression? VisitBlock(BlockSyntax node)
	{
		var statements = node.Statements
			.Select(Visit)
			.SelectMany<Expression?, Expression?>(s => s is BlockExpression block ? block.Expressions : [ s ])
			.Where(w => w != null);

		return Expression.Block(statements);
	}

	public override Expression? VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out IArrayTypeSymbol? arrayType))
		{
			return Expression.NewArrayInit(loader.GetType(arrayType.ElementType), node.Initializer?.Expressions.Select(Visit) ?? [ ]);
		}

		return null;
	}

	public override Expression? VisitAwaitExpression(AwaitExpressionSyntax node)
	{
		// Not supported in expression trees
		return null;
	}

	public override Expression? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var left = Visit(node.Left);
		var right = Visit(node.Right);

		if (left == null || right == null)
		{
			return null;
		}

		if (semanticModel.GetOperation(node) is IBinaryOperation binOp)
		{
			if (binOp.LeftOperand is IConversionOperation { Type: { } lType })
			{
				var targetLeftType = loader.GetType(lType) ?? typeof(object);
				if (left.Type != targetLeftType)
				{
					left = Expression.Convert(left, targetLeftType);
				}
			}
	
			if (binOp.RightOperand is IConversionOperation { Type: { } rType })
			{
				var targetRightType = loader.GetType(rType) ?? typeof(object);
				if (right.Type != targetRightType)
				{
					right = Expression.Convert(right, targetRightType);
				}
			}
		}
	
		return node.Kind() switch
		{
			SyntaxKind.AddExpression => Expression.Add(left, right),
			SyntaxKind.SubtractExpression => Expression.Subtract(left, right),
			SyntaxKind.MultiplyExpression => Expression.Multiply(left, right),
			SyntaxKind.DivideExpression => Expression.Divide(left, right),
			SyntaxKind.ModuloExpression => Expression.Modulo(left, right),
			SyntaxKind.LogicalAndExpression => Expression.AndAlso(left, right),
			SyntaxKind.LogicalOrExpression => Expression.OrElse(left, right),
			SyntaxKind.ExclusiveOrExpression => Expression.ExclusiveOr(left, right),
			SyntaxKind.LeftShiftExpression => Expression.LeftShift(left, right),
			SyntaxKind.RightShiftExpression => Expression.RightShift(left, right),
			SyntaxKind.EqualsExpression => Expression.Equal(left, right),
			SyntaxKind.NotEqualsExpression => Expression.NotEqual(left, right),
			SyntaxKind.LessThanExpression => Expression.LessThan(left, right),
			SyntaxKind.LessThanOrEqualExpression => Expression.LessThanOrEqual(left, right),
			SyntaxKind.GreaterThanExpression => Expression.GreaterThan(left, right),
			SyntaxKind.GreaterThanOrEqualExpression => Expression.GreaterThanOrEqual(left, right),
			_ => null,
		};
	}

	public override Expression? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (semanticModel.TryGetSymbol(node, out ISymbol symbol))
		{
			switch (symbol)
			{
				case IParameterSymbol parameter:
					return parameters[parameter.Name];
				case ILocalSymbol local:
					if (variables.TryGetValue(local.Name, out var variable) && variable.HasValue)
					{
						return Expression.Constant(variable.Value, loader.GetType(local.Type) ?? typeof(object));
					}

					return Expression.Parameter(loader.GetType(local.Type) ?? typeof(object), local.Name);
				case IFieldSymbol field:
				{
					var containingType = loader.GetType(field.ContainingType) ?? typeof(object);

					if (field.IsStatic)
					{
						var fieldInfo = containingType.GetField(field.Name);

						if (fieldInfo != null)
						{
							return Expression.Field(null, fieldInfo);
						}
					}
					else
					{
						var fieldInfo = containingType.GetField(field.Name);

						if (fieldInfo != null)
						{
							var instance = Expression.Parameter(containingType, "instance");

							return Expression.Field(instance, fieldInfo);
						}
					}

					return null;
				}
			}
		}

		return base.VisitIdentifierName(node);
	}

	public override Expression? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		return Visit(node.Expression);
	}

	public override Expression? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		return Visit(node.Expression);
	}

	public override Expression? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		return Expression.Constant(node.Token.Value);
	}

	public override Expression? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// Handle nameof(...) directly for constant folding
		if (node is { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }, ArgumentList.Arguments.Count: 1 })
		{
			var arg = node.ArgumentList.Arguments[0].Expression;
			string? name = null;

			if (semanticModel.TryGetSymbol(arg, out ISymbol? sym))
			{
				name = sym.Name;
			}
			else
			{
				switch (arg)
				{
					case IdentifierNameSyntax id: name = id.Identifier.Text; break;
					case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax last }: name = last.Identifier.Text; break;
					case QualifiedNameSyntax qn: name = qn.Right.Identifier.Text; break;
					case GenericNameSyntax gen: name = gen.Identifier.Text; break;
				}
			}

			if (name is not null)
			{
				return Expression.Constant(name);
			}
		}

		if (semanticModel.TryGetSymbol(node, out IMethodSymbol? targetMethod))
		{
			var arguments = node.ArgumentList.Arguments
				.Select(arg => Visit(arg.Expression))
				.ToList();

			var constantArguments = arguments
				.Select((arg, index) => 
				{
					if (arg is ConstantExpression constExpr)
          {
            return constExpr.Value;
          }

          // Try to get constant value from the original syntax
          var originalArg = node.ArgumentList.Arguments[index].Expression;
					return TryGetLiteralValue(originalArg, out var value) ? value : null;
				})
				.ToArray();

			// If all arguments are constant, try to execute the method at compile time
			if (constantArguments.All(arg => arg != null) && constantArguments.Length == targetMethod.Parameters.Length)
			{
				if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName }
				    && !targetMethod.ContainingType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
				{
					object? instance = null;
					if (instanceName != null)
					{
						TryGetLiteralValue(instanceName, out instance);
					}

					if (loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value))
					{
						if (targetMethod.ReturnsVoid)
						{
							return null;
						}

						return Expression.Constant(value, loader.GetType(targetMethod.ReturnType) ?? typeof(object));
					}
				}
			}

			// Handle static method calls
			if (targetMethod.IsStatic)
			{
				// Check if we're already visiting this method to prevent infinite recursion
				if (visitingMethods?.Contains(targetMethod) is true)
				{
					// Don't inline this method - just keep the invocation
					usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());
					
					// Convert arguments to expressions
					var expArgs = arguments.OfType<Expression>().ToArray();
					if (expArgs.Length == arguments.Count)
					{
						var staticMethodInfo = loader.GetType(targetMethod.ContainingType)?.GetMethod(targetMethod.Name);
						if (staticMethodInfo != null)
						{
							return Expression.Call(staticMethodInfo, expArgs);
						}
					}
				}

				usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());
			}

			// Convert to method call expression if possible
			var containingType = loader.GetType(targetMethod.ContainingType);
			var methodDeclaration = containingType?.GetMethod(targetMethod.Name, 
				targetMethod.Parameters.Select(p => loader.GetType(p.Type) ?? typeof(object)).ToArray());

			if (methodDeclaration != null)
			{
				var expArgs = arguments.OfType<Expression>().ToArray();
				if (expArgs.Length == arguments.Count)
				{
					if (targetMethod.IsStatic)
					{
						return Expression.Call(methodDeclaration, expArgs);
					}
					else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
					{
						var instanceExpr = Visit(memberAccess.Expression);
						if (instanceExpr != null)
						{
							return Expression.Call(instanceExpr, methodDeclaration, expArgs);
						}
					}
				}
			}
		}

		return null;
	}

	public override Expression? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		var expression = Visit(node.Expression);

		if (semanticModel.TryGetSymbol(node, out ISymbol? symbol))
		{
			switch (symbol)
			{
				case IFieldSymbol fieldSymbol:
					var containingType = loader.GetType(fieldSymbol.ContainingType) ?? typeof(object);
					var fieldInfo = containingType.GetField(fieldSymbol.Name);

					if (fieldInfo != null)
					{
						if (fieldSymbol.IsStatic)
						{
							return Expression.Field(null, fieldInfo);
						}
						else if (expression != null)
						{
							return Expression.Field(expression, fieldInfo);
						}
					}
					break;

				case IPropertySymbol propertySymbol:
					if (propertySymbol.Parameters.Length == 0)
					{
						var propContainingType = loader.GetType(propertySymbol.ContainingType) ?? typeof(object);
						var propertyInfo = propContainingType.GetProperty(propertySymbol.Name);

						if (propertyInfo != null)
						{
							if (propertySymbol.IsStatic)
							{
								return Expression.Property(null, propertyInfo);
							}
							else if (expression != null)
							{
								return Expression.Property(expression, propertyInfo);
							}
						}
					}
					break;
			}
		}

		return null;
	}

	public override Expression? VisitCastExpression(CastExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		
		if (expression == null)
    {
      return null;
    }

    if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? targetType))
		{
			var targetRuntimeType = loader.GetType(targetType) ?? typeof(object);
			
			// If it's a constant expression, try to evaluate the cast at compile time
			if (expression is ConstantExpression constExpr)
			{
				try
				{
					var convertedValue = targetType.SpecialType switch
					{
						SpecialType.System_Boolean => Convert.ToBoolean(constExpr.Value),
						SpecialType.System_Byte => Convert.ToByte(constExpr.Value),
						SpecialType.System_Char => Convert.ToChar(constExpr.Value),
						SpecialType.System_DateTime => Convert.ToDateTime(constExpr.Value),
						SpecialType.System_Decimal => Convert.ToDecimal(constExpr.Value),
						SpecialType.System_Double => Convert.ToDouble(constExpr.Value),
						SpecialType.System_Int16 => Convert.ToInt16(constExpr.Value),
						SpecialType.System_Int32 => Convert.ToInt32(constExpr.Value),
						SpecialType.System_Int64 => Convert.ToInt64(constExpr.Value),
						SpecialType.System_SByte => Convert.ToSByte(constExpr.Value),
						SpecialType.System_Single => Convert.ToSingle(constExpr.Value),
						SpecialType.System_String => Convert.ToString(constExpr.Value),
						SpecialType.System_UInt16 => Convert.ToUInt16(constExpr.Value),
						SpecialType.System_UInt32 => Convert.ToUInt32(constExpr.Value),
						SpecialType.System_UInt64 => Convert.ToUInt64(constExpr.Value),
						_ => constExpr.Value
					};

					return Expression.Constant(convertedValue, targetRuntimeType);
				}
				catch
				{
					// Fall through to runtime conversion
				}
			}

			// Create a runtime conversion expression
			return Expression.Convert(expression, targetRuntimeType);
		}

		return null;
	}

	public override Expression? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		var condition = Visit(node.Condition);
		
		// If condition is constant, return the appropriate branch
		if (condition is ConstantExpression { Value: bool b })
		{
			return b ? Visit(node.WhenTrue) : Visit(node.WhenFalse);
		}

		var whenTrue = Visit(node.WhenTrue);
		var whenFalse = Visit(node.WhenFalse);

		if (condition != null && whenTrue != null && whenFalse != null)
		{
			return Expression.Condition(condition, whenTrue, whenFalse);
		}

		return null;
	}

	public override Expression? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		var operand = Visit(node.Operand);

		if (operand == null)
    {
      return null;
    }

    // Handle unary operators
    return node.OperatorToken.Kind() switch
		{
			SyntaxKind.PlusToken => Expression.UnaryPlus(operand),
			SyntaxKind.MinusToken => Expression.Negate(operand),
			SyntaxKind.ExclamationToken => Expression.Not(operand),
			SyntaxKind.TildeToken => Expression.OnesComplement(operand),
			SyntaxKind.PlusPlusToken => Expression.PreIncrementAssign(operand),
			SyntaxKind.MinusMinusToken => Expression.PreDecrementAssign(operand),
			_ => null
		};
	}

	public override Expression? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		var operand = Visit(node.Operand);

		if (operand == null)
    {
      return null;
    }

    return node.OperatorToken.Kind() switch
		{
			SyntaxKind.PlusPlusToken => Expression.PostIncrementAssign(operand),
			SyntaxKind.MinusMinusToken => Expression.PostDecrementAssign(operand),
			_ => null
		};
	}

	public override Expression? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		var left = Visit(node.Left);
		var right = Visit(node.Right);

		if (left == null || right == null)
    {
      return null;
    }

    return node.OperatorToken.Kind() switch
		{
			SyntaxKind.EqualsToken => Expression.Assign(left, right),
			SyntaxKind.PlusEqualsToken => Expression.AddAssign(left, right),
			SyntaxKind.MinusEqualsToken => Expression.SubtractAssign(left, right),
			SyntaxKind.AsteriskEqualsToken => Expression.MultiplyAssign(left, right),
			SyntaxKind.SlashEqualsToken => Expression.DivideAssign(left, right),
			SyntaxKind.PercentEqualsToken => Expression.ModuloAssign(left, right),
			SyntaxKind.AmpersandEqualsToken => Expression.AndAssign(left, right),
			SyntaxKind.BarEqualsToken => Expression.OrAssign(left, right),
			SyntaxKind.CaretEqualsToken => Expression.ExclusiveOrAssign(left, right),
			SyntaxKind.LessThanLessThanEqualsToken => Expression.LeftShiftAssign(left, right),
			SyntaxKind.GreaterThanGreaterThanEqualsToken => Expression.RightShiftAssign(left, right),
			_ => null
		};
	}

	public override Expression? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		var arguments = node.ArgumentList.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToArray();

		if (expression == null || arguments.Any(arg => arg == null))
    {
      return null;
    }

    // Handle array access
    if (arguments.Length == 1)
		{
			return Expression.ArrayIndex(expression, arguments[0]!);
		}

		// Handle multi-dimensional array access
		return Expression.ArrayAccess(expression, arguments);
	}

	public override Expression? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? type))
		{
			var runtimeType = loader.GetType(type);
			if (runtimeType != null)
			{
				var arguments = node.ArgumentList?.Arguments
					.Select(arg => Visit(arg.Expression))
					.ToArray() ?? Array.Empty<Expression>();

				if (arguments.All(arg => arg != null))
				{
					// Try to find matching constructor
					var constructorInfo = runtimeType.GetConstructors()
						.FirstOrDefault(c => c.GetParameters().Length == arguments.Length);

					if (constructorInfo != null)
					{
						return Expression.New(constructorInfo, arguments);
					}
				}
			}

			usings.Add(type.ContainingNamespace.ToDisplayString());
		}

		return null;
	}

	public override Expression? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
	{
		var parts = new List<Expression>();
		var isAllConstant = true;
		var constantParts = new List<string>();

		foreach (var content in node.Contents)
		{
			switch (content)
			{
				case InterpolatedStringTextSyntax text:
					constantParts.Add(text.TextToken.ValueText);
					parts.Add(Expression.Constant(text.TextToken.ValueText));
					break;
				case InterpolationSyntax interp:
					var visited = Visit(interp.Expression);
					if (visited == null)
					{
						isAllConstant = false;
						parts.Add(Expression.Constant(""));
					}
					else if (visited is ConstantExpression constExpr)
					{
						var str = constExpr.Value?.ToString() ?? string.Empty;
						constantParts.Add(str);
						parts.Add(Expression.Constant(str));
					}
					else
					{
						isAllConstant = false;
						// Convert to string
						var toStringMethod = visited.Type.GetMethod("ToString", Type.EmptyTypes);
						if (toStringMethod != null)
						{
							parts.Add(Expression.Call(visited, toStringMethod));
						}
						else
						{
							parts.Add(Expression.Call(visited, typeof(object).GetMethod("ToString")!));
						}
					}
					break;
			}
		}

		// If all parts are constant, return a single constant string
		if (isAllConstant)
		{
			return Expression.Constant(string.Concat(constantParts));
		}

		// Otherwise, create a string concatenation expression
		var concatMethod = typeof(string).GetMethod("Concat", new[] { typeof(object[]) });
		if (concatMethod != null)
		{
			var arrayInit = Expression.NewArrayInit(typeof(object), 
				parts.Select(p => Expression.Convert(p, typeof(object))));
			return Expression.Call(concatMethod, arrayInit);
		}

		return null;
	}

	public override Expression? VisitForEachStatement(ForEachStatementSyntax node)
	{
		// ForEach statements can't be directly converted to expression trees
		// They would need to be converted to while loops or LINQ expressions
		return null;
	}

	public override Expression? VisitForStatement(ForStatementSyntax node)
	{
		// For statements can't be directly converted to expression trees
		// They would need to be converted to while loops
		return null;
	}

	public override Expression? VisitWhileStatement(WhileStatementSyntax node)
	{
		// While statements can't be directly converted to expression trees
		return null;
	}

	public override Expression? VisitIfStatement(IfStatementSyntax node)
	{
		// If statements can't be directly converted to expression trees unless
		// they're part of a conditional expression
		return null;
	}

	public override Expression? VisitReturnStatement(ReturnStatementSyntax node)
	{
		return Visit(node.Expression);
	}

	public override Expression? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		// Variable declarators in expression trees are handled differently
		// This method helps track variable initialization for optimization
		var value = Visit(node.Initializer?.Value);

		if (semanticModel.GetOperation(node) is IVariableDeclaratorOperation operation)
		{
			var name = operation.Symbol.Name;

			if (!variables.TryGetValue(name, out var item))
			{
				item = new VariableItem(operation.Type ?? operation.Symbol.Type, true, value);
				variables.Add(name, item);
			}

			if (TryGetLiteralValue(node.Initializer?.Value, out var result) || (value is ConstantExpression constExpr && (result = constExpr.Value) != null))
			{
				item.Value = result;
				item.IsInitialized = true;
			}
			else
			{
				item.HasValue = false;
				item.IsInitialized = true;
			}
		}

		// For expression trees, we don't typically return variable declarators
		// Instead, they're handled as part of block expressions
		return null;
	}

	public override Expression? VisitTupleExpression(TupleExpressionSyntax node)
	{
		var arguments = node.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToArray();

		// Check if all arguments are valid expressions
		if (arguments.All(arg => arg != null))
		{
			// For now, return null as tuple expressions are complex to handle in expression trees
			// A full implementation would need to create ValueTuple expressions
			return null;
		}

		return null;
	}	
}
