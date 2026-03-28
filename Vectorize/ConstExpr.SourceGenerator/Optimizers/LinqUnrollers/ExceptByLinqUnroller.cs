using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.ExceptBy(second, keySelector)</c> as an intermediate step.
/// Builds a <c>HashSet&lt;TKey&gt;</c> from <c>second</c>, then filters source elements
/// whose projected key is already in the set (via <c>Add</c>).
/// </summary>
public class ExceptByLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "exceptBySet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 2)
		{
			return;
		}

		// TypeArguments: [TSource, TKey] — we need TKey for the HashSet
		var keyType = method.MethodSymbol.TypeArguments[^1];
		var typeName = method.Model.Compilation.GetMinimalString(keyType);

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

		// if (!exceptBySet.Add(keySelector(item))) continue;
		statements.Add(IfStatement(LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", keyExpr)),
			ContinueStatement()));
	}
}


