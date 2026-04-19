using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.DefaultIfEmpty()</c> or <c>.DefaultIfEmpty(defaultValue)</c> as an intermediate step.
/// Tracks whether any elements were processed in the main loop. If not, processes the default
/// value through subsequent chain steps after the main loop completes.
/// </summary>
public class DefaultIfEmptyLinqUnroller : BaseLinqUnroller
{
	private const string HasElementsName = "hasElements";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var hasElements = false;
		statements.Add(CreateLocalDeclaration(HasElementsName, CreateLiteral(false)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// hasElements = true;
		statements.Add(CreateAssignment(HasElementsName, CreateLiteral(true)!));
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// Default value: method.Parameters[0] if present, otherwise default(T)
		var defaultValue = method.Parameters.Length >= 1
			? method.Parameters[0]
			: method.MethodSymbol.TypeArguments[0].GetDefaultValue();

		var body = Block(partialLoopBody);
		
		body = body.ReplaceIdentifier("item", defaultValue) as BlockSyntax ?? body;

		// if (!hasElements)
		// {
		//     foreach (var item in new[] { defaultValue }) { <partialLoopBody> }
		// }
		resultStatements.Add(IfStatement(
			LogicalNotExpression(IdentifierName(HasElementsName)),
			body));
	}
}

