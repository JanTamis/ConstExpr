using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MinLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "value";
	private const string FirstName = "first";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var isArrayLike = IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType);

		if (!isArrayLike)
		{
			// var e = collection.GetEnumerator();
			statements.Add(CreateLocalDeclaration("e", CreateMethodInvocation(IdentifierName("collection"), "GetEnumerator")));
		}

		if (method.HasElementDroppingStep)
		{
			// A filter sits between the source and here — seed from the first element that survives the
			// chain (tracked by `first`) and defer the empty-sequence throw to UnrollUnderLoop.
			statements.Add(CreateDefaultSeed(ResultName, method.MethodSymbol.ReturnType.AsTypeSyntax()));
			statements.Add(CreateLocalDeclaration(FirstName, CreateLiteral(true)!));
			return;
		}

		if (isArrayLike)
		{
			var countProperty = IsInvokedOnArray(method.CollectionType) ? "Length" : "Count";

			// if (collection.Length == 0) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				EqualsExpression(
					MemberAccessExpression(IdentifierName("collection"), IdentifierName(countProperty)),
					CreateLiteral(0)),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			// var value = collection[0]; (or lambda(collection[0]) when selector is present)
			ExpressionSyntax firstElement = ElementAccessExpression(IdentifierName("collection"), CreateLiteral(0));

			if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var initLambda))
			{
				firstElement = ReplaceLambda(method.Visit(initLambda) as LambdaExpressionSyntax ?? initLambda, firstElement)!;
			}

			statements.Add(CreateLocalDeclaration(ResultName, firstElement));
		}
		else
		{
			// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				LogicalNotExpression(CreateMethodInvocation(IdentifierName("e"), "MoveNext")),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			// var value = e.Current; (or lambda(e.Current) when selector is present)
			ExpressionSyntax firstCurrent = MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"));

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
		var element = !IsInvokedOnArray(method.CollectionType) && !IsInvokedOnCollection(method.CollectionType)
			? MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		var candidate = method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var lambda)
			? ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, element)!
			: element;

		if (method.HasElementDroppingStep)
		{
			// if (first) { value = candidate; first = false; }
			// else if (candidate < value) { value = candidate; }
			// `candidate` is spelled inline rather than bound to a local: on the enumerator path it is
			// `e.Current`, which LICM would wrongly hoist out of the loop if it were a `var` declaration.
			statements.Add(IfStatement(
				IdentifierName(FirstName),
				Block(
					CreateAssignment(ResultName, candidate),
					CreateAssignment(FirstName, CreateLiteral(false)!)),
				ElseClause(IfStatement(
					LessThanExpression(candidate, IdentifierName(ResultName)),
					CreateAssignment(ResultName, candidate)))));

			return;
		}

		// if (candidate < value) { value = candidate; }
		statements.Add(IfStatement(LessThanExpression(candidate, IdentifierName(ResultName)),
			CreateAssignment(ResultName, candidate)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.HasElementDroppingStep)
		{
			statements.Add(IfStatement(IdentifierName(FirstName),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));
		}

		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}

	public override void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		if (IsInvokedOnArray(collectionType) || IsInvokedOnCollection(collectionType))
		{
			var countProperty = IsInvokedOnArray(collectionType) ? "Length" : "Count";
			var start = method.HasElementDroppingStep ? CreateLiteral(0) : CreateLiteral(1);
			resultStatements.Add(CreateForLoop(collectionName, "i", countProperty, Block(statements), start));
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
			return ElementAccessExpression(IdentifierName(collectionName), IdentifierName("i"));
		}

		return MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"));
	}
}