using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Visitors;

public sealed class ComplexityVisitor(IDictionary<string, VariableItem> variables) : CSharpSyntaxVisitor<int>
{
	public override int DefaultVisit(SyntaxNode node)
	{
		return int.MaxValue / 2;
	}

	public override int VisitArgument(ArgumentSyntax node)
	{
		return Visit(node.Expression);
	}

	public override int VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		return 0;
	}

	public override int VisitPredefinedType(PredefinedTypeSyntax node)
	{
		return 0;
	}

	public override int VisitIdentifierName(IdentifierNameSyntax node)
	{
		return 0;
	}

	public override int VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		return 1 + Visit(node.Expression);
	}

	public override int VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		return Visit(node.Expression);
	}

	public override int VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		return 1 + Visit(node.Operand);
	}

	public override int VisitCastExpression(CastExpressionSyntax node)
	{
		return 1 + Visit(node.Expression);
	}

	public override int VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		return 1 + Visit(node.Left) + Visit(node.Right);
	}

	public override int VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		return 3 + Visit(node.Expression)
		         + node.ArgumentList.Arguments.Sum(a => Visit(a.Expression) + 1);
	}

	public override int VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		return 2 + Visit(node.Expression)
		         + node.ArgumentList.Arguments.Sum(a => Visit(a.Expression) + 1);
	}

	public override int VisitRangeExpression(RangeExpressionSyntax node)
	{
		return 2 + Visit(node.LeftOperand) + Visit(node.RightOperand);
	}

	public override int VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		return 3 + Visit(node.Condition) + Visit(node.WhenTrue) + Visit(node.WhenFalse);
	}

	public override int VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		return 5 + Visit(node.Body);
	}

	public override int VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
	{
		return 5 + Visit(node.Body);
	}

	public override int VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
	{
		return 5 + Visit(node.Body);
	}
}