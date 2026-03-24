using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Intersect(second)</c> as an intermediate step.
/// Builds a <c>HashSet&lt;T&gt;</c> from <c>second</c>, then yields only elements
/// that can be removed from the set (ensuring distinct results).
/// </summary>
public class IntersectLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "intersectSet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1)
			return;

		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var intersectSet = new HashSet<T>(second);
		// — or with a custom comparer: new HashSet<T>(second, comparer)
		statements.Add(CreateLocalDeclaration(SetName, ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), method.Parameters)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// if (!intersectSet.Remove(item)) continue;
		// Remove returns true only on the first occurrence, matching LINQ's distinct semantics
		statements.Add(IfStatement(LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Remove", elementName)),
			ContinueStatement()));
	}
}


