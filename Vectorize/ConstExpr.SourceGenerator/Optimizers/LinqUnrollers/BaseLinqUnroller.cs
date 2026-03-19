using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public abstract class BaseLinqUnroller
{
	public virtual void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements) { }

	public virtual void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName) { }

	public virtual void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements) { }

	public virtual void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		resultStatements.Add(ForEachStatement(IdentifierName("var"), "item", IdentifierName(collectionName), Block(statements)));
	}

	public virtual ExpressionSyntax GetCollectionElement(UnrolledLinqMethod method, string collectionName)
	{
		return IdentifierName("item");
	}

	protected static ExpressionSyntax InvertSyntax(ExpressionSyntax node)
	{
		switch (node)
		{
			// invert binary expressions with logical operators
			case BinaryExpressionSyntax binary:
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
					SyntaxKind.IsExpression => IsPatternExpression(binary.Left, UnaryPattern(Token(SyntaxKind.NotKeyword), TypePattern((TypeSyntax) binary.Right))),
					_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node)
				};
			}
			case LiteralExpressionSyntax literal:
			{
				return literal.Kind() switch
				{
					SyntaxKind.FalseLiteralExpression => LiteralExpression(SyntaxKind.TrueLiteralExpression),
					SyntaxKind.TrueLiteralExpression => LiteralExpression(SyntaxKind.FalseLiteralExpression),
					_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node)
				};
			}
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

	/// <summary>
	/// Checks if the invocation is made on an array type.
	/// </summary>
	protected static bool IsInvokedOnArray(ITypeSymbol type)
	{
		return type is IArrayTypeSymbol;
	}

	/// <summary>
	/// Checks if the invocation is made on a List&lt;T&gt; type.
	/// </summary>
	protected static bool IsInvokedOnCollection(ITypeSymbol type)
	{
		return type.AllInterfaces.Any(a => a.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IList_T);
	}

	protected ForStatementSyntax CreateForLoop(string collectionName, string indexName, string lengthName, BlockSyntax body, ExpressionSyntax initialElement)
	{
		return ForStatement(body)
			.WithDeclaration(VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(VariableDeclarator(indexName).WithInitializer(EqualsValueClause(initialElement))))
			)
			.WithCondition(BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName(indexName), MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(collectionName), IdentifierName(lengthName))))
			.WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(indexName))));
	}
}

file sealed class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return node.Identifier.Text == identifier ? replacement : base.VisitIdentifierName(node);
	}
}