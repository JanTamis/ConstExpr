using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Zip(second)</c> or <c>.Zip(second, resultSelector)</c> as an intermediate step.
/// Uses a counter to index into the second collection in parallel with the main loop.
/// Breaks when either collection is exhausted.
/// </summary>
public class ZipLinqUnroller : BaseLinqUnroller
{
	private const string IndexName = "zipIndex";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var zipIndex = 0;
		statements.Add(CreateLocalDeclaration(IndexName, CreateLiteral(0)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1)
		{
			return;
		}

		var second = method.Parameters[0];

		// if (zipIndex >= second.Length / second.Count) break;
		// Use a general Count() call approach or Length/Count property
		var sizeExpr = GetCollectionSizeExpression(method.CollectionType, second.ToString()!)
		               ?? MemberAccessExpression(second, IdentifierName("Count"));

		statements.Add(IfStatement(
			GreaterThanOrEqualExpression(IdentifierName(IndexName), sizeExpr),
			BreakStatement()));

		// var zipSecondElement = second[zipIndex];
		var secondElement = ElementAccessExpression(second, IdentifierName(IndexName));
		statements.Add(CreateLocalDeclaration("zipSecondElement", secondElement));

		// zipIndex++;
		statements.Add(ExpressionStatement(PostIncrementExpression(IdentifierName(IndexName))));

		// If there's a result selector lambda: var zippedItem = selector(item, second[i]);
		if (method.Parameters.Length >= 2 && TryGetLambda(method.Parameters[1], out var lambda))
		{
			// Replace first param with current element, second param with second element
			var bodyWithFirst = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName);

			if (bodyWithFirst is not null
			    && lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
			{
				var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
				var identifiers = bodyWithFirst
					.DescendantNodesAndSelf()
					.OfType<IdentifierNameSyntax>()
					.Where(n => n.Identifier.Text == secondParam)
					.ToList();

				var finalBody = bodyWithFirst.ReplaceNodes(identifiers, (_, _) => IdentifierName("zipSecondElement"));

				statements.Add(CreateLocalDeclaration("zippedItem", finalBody));
				elementName = IdentifierName("zippedItem");
			}
		}
		else
		{
			// No result selector: create a ValueTuple (item, secondElement)
			statements.Add(CreateLocalDeclaration("zippedItem",
				TupleExpression(SeparatedList([
					Argument(elementName),
					Argument(IdentifierName("zipSecondElement"))
				]))));

			elementName = IdentifierName("zippedItem");
		}
	}
}


