using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Prepend context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().Prepend(x) => new[] { x } or simplified form
/// - collection.Append(a).Prepend(b) => can be optimized for specific cases
/// </summary>
public class PrependFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Prepend), 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		if (IsEmptyEnumerable(source))
		{
			result = CreateImplicitArray(context.VisitedParameters[0]);
			return true;
		}

		result = null;
		return false;
	}
}

