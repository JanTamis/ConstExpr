using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.IntersectBy(second, keySelector)</c> as an intermediate step.
/// Builds a <c>HashSet&lt;TKey&gt;</c> from <c>second</c>, then yields only source elements
/// whose projected key can be removed from the set (ensuring distinct-by-key results).
/// </summary>
public class IntersectByLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "intersectBySet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 2)
		{
			return;
		}

		// TypeArguments: [TSource, TKey] — we need TKey for the HashSet
		var keyType = method.MethodSymbol.TypeArguments[^1];
		var typeName = method.Model.Compilation.GetMinimalString(keyType);

		// var intersectBySet = new HashSet<TKey>(second);
		statements.Add(CreateLocalDeclaration(SetName, ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), method.Parameters)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 2
		    || !TryGetLambda(method.Parameters[1], out var lambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName);

		if (keyExpr is null)
		{
			return;
		}

		// if (!intersectBySet.Remove(keySelector(item))) continue;
		statements.Add(IfStatement(LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Remove", keyExpr)),
			ContinueStatement()));
	}
}