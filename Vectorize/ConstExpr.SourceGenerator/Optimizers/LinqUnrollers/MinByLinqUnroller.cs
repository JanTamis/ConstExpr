using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MinByLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string BestKeyName = "bestKey";
	private const string KeyName = "key";
	private const string FirstName = "first";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1 || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var isArrayLike = IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType);

		if (!isArrayLike)
		{
			// var e = collection.GetEnumerator();
			statements.Add(CreateLocalDeclaration("e", CreateMethodInvocation(IdentifierName("collection"), "GetEnumerator")));
		}

		if (method.HasElementDroppingStep)
		{
			// A filter precedes this step, so element 0 of the source is not necessarily the element to
			// seed from. Track the first surviving element with `first`; the empty-sequence throw moves
			// to UnrollUnderLoop. No `bestKey` local — the loop body recomputes `selector(result)` so
			// that neither LICM nor CSE can hoist a per-iteration key out of the loop (both would, on
			// the enumerator path, read `e.Current` before the first MoveNext).
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

			var firstElement = ElementAccessExpression(IdentifierName("collection"), CreateLiteral(0));

			// var result = collection[0];
			statements.Add(CreateLocalDeclaration(ResultName, firstElement));

			// var bestKey = selector(collection[0]);
			statements.Add(CreateLocalDeclaration(BestKeyName,
				ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, firstElement)!));
		}
		else
		{
			// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(
				LogicalNotExpression(CreateMethodInvocation(IdentifierName("e"), "MoveNext")),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

			var firstCurrent = MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"));

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
		{
			return;
		}

		// For the enumerator path the element is e.Current, not the foreach loop variable.
		var element = !IsInvokedOnArray(method.CollectionType) && !IsInvokedOnCollection(method.CollectionType)
			? MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		var selector = method.Visit(lambda) as LambdaExpressionSyntax ?? lambda;

		if (method.HasElementDroppingStep)
		{
			// if (first) { result = element; first = false; }
			// else if (selector(element) < selector(result)) { result = element; }
			statements.Add(IfStatement(
				IdentifierName(FirstName),
				Block(
					CreateAssignment(ResultName, element),
					CreateAssignment(FirstName, CreateLiteral(false)!)),
				ElseClause(IfStatement(
					LessThanExpression(
						ReplaceLambda(selector, element)!,
						ReplaceLambda(selector, IdentifierName(ResultName))!),
					CreateAssignment(ResultName, element)))));

			return;
		}

		// var key = selector(item);
		statements.Add(CreateLocalDeclaration(KeyName, ReplaceLambda(selector, element)!));

		// if (key < bestKey) { result = item; bestKey = key; }
		statements.Add(IfStatement(
			LessThanExpression(IdentifierName(KeyName), IdentifierName(BestKeyName)),
			Block(
				CreateAssignment(ResultName, element),
				CreateAssignment(BestKeyName, IdentifierName(KeyName)))));
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
		if (IsInvokedOnArray(collectionType)
		    || IsInvokedOnCollection(collectionType))
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
		if (IsInvokedOnArray(method.CollectionType)
		    || IsInvokedOnCollection(method.CollectionType))
		{
			return ElementAccessExpression(IdentifierName(collectionName), IdentifierName("i"));
		}

		return MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"));
	}
}