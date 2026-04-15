using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.ToDictionary(keySelector)</c> or <c>.ToDictionary(keySelector, elementSelector)</c>
/// as a terminal step. Builds a <c>Dictionary&lt;TKey, TValue&gt;</c> during the loop
/// and returns it after the loop completes.
/// </summary>
public class ToDictionaryLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var keyType = method.MethodSymbol.TypeArguments.Length >= 2
			? method.MethodSymbol.TypeArguments[1]
			: method.MethodSymbol.TypeArguments[0];

		var valueType = method.MethodSymbol.TypeArguments.Length >= 3
			? method.MethodSymbol.TypeArguments[2]
			: method.MethodSymbol.TypeArguments[0];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var valueTypeName = method.Model.Compilation.GetMinimalString(valueType);

		// var result = new Dictionary<TKey, TValue>();
		statements.Add(CreateLocalDeclaration(ResultName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, {valueTypeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1
		    || !TryGetLambda(method.Parameters[0], out var keyLambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(keyLambda) as LambdaExpressionSyntax ?? keyLambda, elementName);

		if (keyExpr is null)
		{
			return;
		}

		// Determine value: if element selector, apply it; otherwise use element itself
		ExpressionSyntax valueExpr = elementName;
		if (method.Parameters.Length >= 2 && TryGetLambda(method.Parameters[1], out var valueLambda))
		{
			var selectedValue = ReplaceLambda(method.Visit(valueLambda) as LambdaExpressionSyntax ?? valueLambda, elementName);
			if (selectedValue is not null)
			{
				valueExpr = selectedValue;
			}
		}

		// result.Add(keySelector(item), valueSelector(item));
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(ResultName), "Add", keyExpr, valueExpr)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


