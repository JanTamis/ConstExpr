using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Except(second)</c> as an intermediate step.
/// Uses a <c>HashSet&lt;T&gt;</c> seeded from <c>second</c>, then filters via <c>Add</c>
/// (which also handles distinct from source, matching LINQ semantics).
/// </summary>
public class ExceptLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "exceptSet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1)
			return;

		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		statements.Add(CreateLocalDeclaration(SetName, ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), method.Parameters)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// if (!exceptSet.Add(item)) continue;
		// Add returns false when the element was already in the set (from second, or duplicate from source)
		statements.Add(IfStatement(LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", elementName)),
			ContinueStatement()));
	}
}
