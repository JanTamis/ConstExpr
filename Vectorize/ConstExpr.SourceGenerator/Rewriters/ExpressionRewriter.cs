using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace ConstExpr.SourceGenerator.Rewriters;

public class ExpressionRewriter(SemanticModel semanticModel, MetadataLoader loader, Action<SyntaxNode?, Exception> exceptionHandler, IDictionary<string, VariableItem> variables, CancellationToken token) : CSharpSyntaxVisitor<Expression?>
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
			.SelectMany<Expression?, Expression?>(s => s is BlockExpression block ? block.Expressions : [s])
			.Where(w => w != null);

		return Expression.Block(statements);
	}

	public override Expression? VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out IArrayTypeSymbol? arrayType))
		{
			return Expression.NewArrayInit(loader.GetType(arrayType.ElementType), node.Initializer?.Expressions.Select(Visit) ?? []);
		}
		
		return null;
	}

	public override Expression? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		return Visit(node.Expression);
	}

	public override Expression? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		return Visit(node.Expression);
	}
}