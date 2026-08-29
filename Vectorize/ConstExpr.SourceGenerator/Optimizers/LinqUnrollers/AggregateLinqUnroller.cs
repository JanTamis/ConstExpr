using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class AggregateLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string FirstName = "first";

	/// <summary>
	///   Seedless <c>Aggregate((acc, v) =&gt; ...)</c> behind a filter: the seed is the first element
	///   that survives the chain, not <c>collection[0]</c>. The 2/3-arg seeded overloads always fold
	///   every element through <c>func</c> starting from the explicit seed, so they never take this path.
	/// </summary>
	private static bool UseFirstFlag(UnrolledLinqMethod method)
	{
		return method.Parameters.Length == 1 && method.HasElementDroppingStep;
	}

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length == 1)
		{
			var isArrayLike = IsInvokedOnArray(method.CollectionType) || IsInvokedOnCollection(method.CollectionType);

			if (!isArrayLike)
			{
				// using var e = collection.GetEnumerator();
				statements.Add(CreateLocalDeclaration("e", CreateMethodInvocation(IdentifierName("collection"), "GetEnumerator")));
			}

			if (UseFirstFlag(method))
			{
				// var result = default(T); var first = true;  (result overwritten on the first survivor)
				statements.Add(CreateDefaultSeed(ResultName, method.MethodSymbol.ReturnType.AsTypeSyntax()));
				statements.Add(CreateLocalDeclaration(FirstName, CreateLiteral(true)!));
				return;
			}

			if (isArrayLike)
			{
				// var result = collection[0];
				var elementAccess = ElementAccessExpression(IdentifierName("collection"), CreateLiteral(0));

				statements.Add(CreateLocalDeclaration(ResultName, elementAccess));
			}
			else
			{
				// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
				statements.Add(IfStatement(LogicalNotExpression(CreateMethodInvocation(IdentifierName("e"), "MoveNext")),
					CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));

				// var result = e.Current;
				statements.Add(CreateLocalDeclaration(ResultName, MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"))));
			}

			return;
		}

		statements.Add(CreateLocalDeclaration(ResultName, method.Parameters[0]));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// 1-param overload: Aggregate((acc, el) => ...) — lambda is Parameters[0]
		// 2/3-param overloads: Aggregate(seed, (acc, el) => ...) — lambda is Parameters[1]
		var lambdaIndex = method.Parameters.Length == 1 ? 0 : 1;

		if (lambdaIndex >= method.Parameters.Length
		    || !TryGetLambda(method.Parameters[lambdaIndex], out var lambda))
		{
			return;
		}

		// For the 1-param enumerator path (non-array, non-IList), the current element is e.Current
		var element = method.Parameters.Length == 1
		              && !IsInvokedOnArray(method.CollectionType)
		              && !IsInvokedOnCollection(method.CollectionType)
			? MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		// Replace the first lambda param (accumulator) with "result"
		var bodyWithResult = ReplaceLambda(lambda, IdentifierName(ResultName));

		if (bodyWithResult == null)
		{
			return;
		}

		// Replace the second lambda param (element) with the element expression
		var finalBody = bodyWithResult;

		if (lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
		{
			var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
			var identifiers = bodyWithResult
				.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Where(n => n.Identifier.Text == secondParam)
				.ToList();

			finalBody = bodyWithResult.ReplaceNodes(identifiers, (_, _) => element);
		}

		if (UseFirstFlag(method))
		{
			// if (first) { result = element; first = false; }
			// else { result = func(result, element); }
			var foldAssign = method.Visit(CreateAssignment(ResultName, finalBody)) as StatementSyntax
			                 ?? CreateAssignment(ResultName, finalBody);

			statements.Add(IfStatement(
				IdentifierName(FirstName),
				Block(
					CreateAssignment(ResultName, element),
					CreateAssignment(FirstName, CreateLiteral(false)!)),
				ElseClause(Block(foldAssign))));

			return;
		}

		statements.Add(method.Visit(CreateAssignment(ResultName, finalBody)) as ExpressionStatementSyntax);
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (UseFirstFlag(method))
		{
			// if (first) throw new InvalidOperationException("Sequence contains no elements");
			statements.Add(IfStatement(IdentifierName(FirstName),
				CreateThrowExpression<InvalidOperationException>("Sequence contains no elements")));
		}

		if (method.Parameters.Length == 3
		    && TryGetLambda(method.Parameters[2], out var lambda))
		{
			// result = func(result, element);
			statements.Add(ReturnStatement(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(ResultName))!));
		}
		else
		{
			statements.Add(ReturnStatement(IdentifierName(ResultName)));
		}
	}

	public override void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length == 1)
		{
			if (IsInvokedOnArray(collectionType)
			    || IsInvokedOnCollection(collectionType))
			{
				var countProperty = IsInvokedOnArray(collectionType) ? "Length" : "Count";

				var start = UseFirstFlag(method) ? CreateLiteral(0) : CreateLiteral(1);
				resultStatements.Add(CreateForLoop(collectionName, "i", countProperty, Block(statements), start));
			}
			else
			{
				resultStatements.Add(WhileStatement(
					CreateMethodInvocation(IdentifierName("e"), "MoveNext"),
					Block(statements)));
			}
		}
		else
		{
			base.CreateLoop(method, collectionType, statements, collectionName, resultStatements);
		}
	}

	public override ExpressionSyntax GetCollectionElement(UnrolledLinqMethod method, string collectionName)
	{
		if (method.Parameters.Length == 1)
		{
			if (IsInvokedOnArray(method.CollectionType)
			    || IsInvokedOnCollection(method.CollectionType))
			{
				return ElementAccessExpression(IdentifierName(collectionName), IdentifierName("i"));
			}

			return MemberAccessExpression(IdentifierName("e"), IdentifierName("Current"));
		}

		return base.GetCollectionElement(method, collectionName);
	}
}