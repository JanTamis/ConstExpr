using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SkipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Skip), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

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