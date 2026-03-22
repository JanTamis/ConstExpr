using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MaxLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType))
		{
			var countProperty = IsInvokedOnArray(method.CollectionType) ? "Length" : "Count";

			// if (collection.Length == 0) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				BinaryExpression(SyntaxKind.EqualsExpression,
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("collection"), IdentifierName(countProperty)),
					CreateLiteral(0)),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			// var value = collection[0]; (or lambda(collection[0]) when selector is present)
			ExpressionSyntax firstElement = ElementAccessExpression(IdentifierName("collection"))
				.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(CreateLiteral(0)))));

			if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var initLambda))
			{
				firstElement = ReplaceLambda(method.Visit(initLambda) as LambdaExpressionSyntax ?? initLambda, firstElement)!;
			}

			statements.Add(CreateLocalDeclaration(ResultName, firstElement));
		}
		else
		{
			// var e = collection.GetEnumerator();
			statements.Add(CreateLocalDeclaration("e", CreateMethodInvocation(IdentifierName("collection"), "GetEnumerator")));

			// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, CreateMethodInvocation(IdentifierName("e"), "MoveNext")),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			// var value = e.Current; (or lambda(e.Current) when selector is present)
			ExpressionSyntax firstCurrent = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"));

			if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var initLambda))
			{
				firstCurrent = ReplaceLambda(method.Visit(initLambda) as LambdaExpressionSyntax ?? initLambda, firstCurrent)!;
			}

			statements.Add(CreateLocalDeclaration(ResultName, firstCurrent));
		}
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// For the enumerator path the element is e.Current, not the foreach loop variable.
		var element = (!IsInvokedOnArray(method.CollectionType) && !IsInvokedOnCollection(method.CollectionType))
			? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		ExpressionSyntax candidate;

		if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var lambda))
		{
			candidate = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, element)!;
		}
		else
		{
			candidate = element;
		}

		// if (candidate > value) { value = candidate; }
		var condition = BinaryExpression(SyntaxKind.GreaterThanExpression, candidate, IdentifierName(ResultName));
		statements.Add(IfStatement(condition, CreateAssignment(ResultName, candidate)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}

	public override void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		if (IsInvokedOnArray(collectionType) || IsInvokedOnCollection(collectionType))
		{
			var countProperty = IsInvokedOnArray(collectionType) ? "Length" : "Count";
			resultStatements.Add(CreateForLoop(collectionName, "i", countProperty, Block(statements), CreateLiteral(1)));
		}
		else
		{
			// while (e.MoveNext()) { ... }
			resultStatements.Add(WhileStatement(
				CreateMethodInvocation(IdentifierName("e"), "MoveNext"),
				Block(statements)));
		}
	}

	public override ExpressionSyntax GetCollectionElement(UnrolledLinqMethod method, string collectionName)
	{
		if (IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType))
		{
			return ElementAccessExpression(IdentifierName(collectionName))
				.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("i")))));
		}

		return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"));
	}
}
