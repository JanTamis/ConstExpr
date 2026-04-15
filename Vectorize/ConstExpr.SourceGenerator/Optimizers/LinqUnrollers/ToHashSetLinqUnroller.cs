using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.ToHashSet()</c> as a terminal step.
/// Builds a <c>HashSet&lt;T&gt;</c> during the loop and returns it after the loop completes.
/// </summary>
public class ToHashSetLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var result = new HashSet<T>();
		statements.Add(CreateLocalDeclaration(ResultName,
			ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// result.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(ResultName), "Add", elementName)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}

