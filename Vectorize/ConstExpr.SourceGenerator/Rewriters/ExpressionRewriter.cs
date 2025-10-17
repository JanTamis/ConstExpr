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

namespace ConstExpr.SourceGenerator.Rewriters;

public class ExpressionRewriter(SemanticModel semanticModel, MetadataLoader loader, Action<SyntaxNode?, Exception> exceptionHandler, IDictionary<string, VariableItem> variables, IDictionary<string, ParameterExpression> parameters, CancellationToken token) : CSharpSyntaxVisitor<Expression?>
{
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
}