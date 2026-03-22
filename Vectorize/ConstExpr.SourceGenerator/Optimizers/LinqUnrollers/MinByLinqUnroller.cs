using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MinByLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string BestKeyName = "bestKey";
	private const string KeyName = "key";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1 || !TryGetLambda(method.Parameters[0], out var lambda))
			return;

		if (IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType))
		{
			var countProperty = IsInvokedOnArray(method.CollectionType) ? "Length" : "Count";

			// if (collection.Length == 0) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				BinaryExpression(SyntaxKind.EqualsExpression,
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("collection"), IdentifierName(countProperty)),
					CreateLiteral(0)),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			var firstElement = ElementAccessExpression(IdentifierName("collection"))
				.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(CreateLiteral(0)))));

			// var result = collection[0];
			statements.Add(CreateLocalDeclaration(ResultName, firstElement));

			// var bestKey = selector(collection[0]);
			statements.Add(CreateLocalDeclaration(BestKeyName,
				ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, firstElement)!));
		}
		else
		{
			// var e = collection.GetEnumerator();
			statements.Add(CreateLocalDeclaration("e", CreateMethodInvocation(IdentifierName("collection"), "GetEnumerator")));

			// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, CreateMethodInvocation(IdentifierName("e"), "MoveNext")),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			var firstCurrent = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"));

			// var result = e.Current;
			statements.Add(CreateLocalDeclaration(ResultName, firstCurrent));

			// var bestKey = selector(e.Current);
			statements.Add(CreateLocalDeclaration(BestKeyName,
				ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, firstCurrent)!));
		}
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1 || !TryGetLambda(method.Parameters[0], out var lambda))
			return;

		// For the enumerator path the element is e.Current, not the foreach loop variable.
		var element = (!IsInvokedOnArray(method.CollectionType) && !IsInvokedOnCollection(method.CollectionType))
			? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		// var key = selector(item);
		statements.Add(CreateLocalDeclaration(KeyName,
			ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, element)!));

		// if (key < bestKey) { result = item; bestKey = key; }
		var condition = BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName(KeyName), IdentifierName(BestKeyName));
		statements.Add(IfStatement(condition, Block(
			CreateAssignment(ResultName, element),
			CreateAssignment(BestKeyName, IdentifierName(KeyName)))));
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
