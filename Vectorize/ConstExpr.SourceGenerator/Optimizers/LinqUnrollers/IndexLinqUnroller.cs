using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Index()</c> (.NET 9+) as an intermediate step.
/// Adds a counter that tracks the current element index.
/// The element becomes a <c>(int Index, T Item)</c> value tuple.
/// </summary>
public class IndexLinqUnroller : BaseLinqUnroller
{
	private const string CounterName = "indexCounter";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var indexCounter = 0;
		statements.Add(CreateLocalDeclaration(CounterName, CreateLiteral(0)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// var indexedItem = (indexCounter, item);
		statements.Add(CreateLocalDeclaration("indexedItem",
			TupleExpression(SeparatedList([
				Argument(IdentifierName(CounterName)),
				Argument(elementName)
			]))));

		// indexCounter++;
		statements.Add(ExpressionStatement(PostIncrementExpression(IdentifierName(CounterName))));

		elementName = IdentifierName("indexedItem");
	}
}

