using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SkipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Skip), n => n is 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int32);

		var amount = context.VisitedParameters[0];
		var isNewSource = false;

		while (IsLinqMethodChain(source, nameof(Enumerable.Skip), out var skipInvocation)
		       && TryGetLinqSource(skipInvocation, out var skipSource))
		{
			amount = OptimizeArithmetic(context, SyntaxKind.AddExpression, amount, skipInvocation.ArgumentList.Arguments[0].Expression, intType);

			TryGetOptimizedChainExpression(skipSource, MaterializingMethods, out source);
			isNewSource = true;
		}

		if (amount is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = source;
			return true;
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source, amount);
			return true;
		}

		result = null;
		return false;
	}
}