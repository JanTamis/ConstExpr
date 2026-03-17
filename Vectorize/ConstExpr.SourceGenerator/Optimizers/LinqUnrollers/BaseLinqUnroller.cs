using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public abstract class BaseLinqUnroller
{
	public abstract void UnrollAboveLoop(UnrolledLinqMethod method, IMethodSymbol methodSymbol, List<StatementSyntax> statements);
	
	public abstract void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName);

	public abstract void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements);

	protected static ExpressionSyntax InvertSyntax(ExpressionSyntax node)
	{
		// invert binary expressions with logical operators
		if (node is BinaryExpressionSyntax binary)
		{
			return binary.Kind() switch
			{
				SyntaxKind.LogicalAndExpression => BinaryExpression(SyntaxKind.LogicalOrExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.LogicalOrExpression => BinaryExpression(SyntaxKind.LogicalAndExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.EqualsExpression => BinaryExpression(SyntaxKind.NotEqualsExpression, binary.Left, binary.Right),
				SyntaxKind.NotEqualsExpression => BinaryExpression(SyntaxKind.EqualsExpression, binary.Left, binary.Right),
				SyntaxKind.GreaterThanExpression => BinaryExpression(SyntaxKind.LessThanOrEqualExpression, binary.Left, binary.Right),
				SyntaxKind.GreaterThanOrEqualExpression => BinaryExpression(SyntaxKind.LessThanExpression, binary.Left, binary.Right),
				SyntaxKind.LessThanExpression => BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, binary.Left, binary.Right),
				SyntaxKind.LessThanOrEqualExpression => BinaryExpression(SyntaxKind.GreaterThanExpression, binary.Left, binary.Right),
				_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node)
			};
		}

		return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node);
	}

	protected static ExpressionSyntax? ReplaceLambda(LambdaExpressionSyntax lambda, ExpressionSyntax replacement)
	{
		var lambdaParam = GetLambdaParameter(lambda!);

		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
			ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body,
			_ => throw new InvalidOperationException("Unsupported lambda expression type")
		};

		return ReplaceIdentifier(body, lambdaParam, replacement) as ExpressionSyntax;
	}

	protected static bool TryGetLambda(ExpressionSyntax? parameter, [NotNullWhen(true)] out LambdaExpressionSyntax? lambda)
	{
		if (parameter is LambdaExpressionSyntax lambdaExpression)
		{
			lambda = lambdaExpression;
			return true;
		}

		lambda = null;
		return false;
	}

	private static string GetLambdaParameter(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: > 0 } parenthesizedLambda
				=> parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => throw new InvalidOperationException("Unsupported lambda expression type")
		};
	}

	private static SyntaxNode ReplaceIdentifier(CSharpSyntaxNode body, string oldIdentifier, ExpressionSyntax replacement)
	{
		var wrappedReplacement = replacement is BinaryExpressionSyntax or ConditionalExpressionSyntax
			? ParenthesizedExpression(replacement)
			: replacement;

		return new IdentifierReplacer(oldIdentifier, wrappedReplacement).Visit(body)!;
	}
}

file sealed class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return node.Identifier.Text == identifier ? replacement : base.VisitIdentifierName(node);
	}
}