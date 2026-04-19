using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Append context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Append(x) =&gt; collection.Append(x) (skip type cast)
/// - collection.ToList().Append(x) =&gt; collection.Append(x) (skip materialization)
/// - collection.ToArray().Append(x) =&gt; collection.Append(x) (skip materialization)
/// </summary>
public class AppendFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Append), 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Append call
		if (TryGetOptimizedChainExpression(source, MaterializingMethods, out source))
		{
			if (TryExecutePredicates(context, source, out result, out source))
			{
				return true;
			}

			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}
