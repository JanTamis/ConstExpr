using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Reverse context.Method.
/// Optimizes patterns such as:
/// - collection.Reverse().Reverse() => collection (double reverse cancels out)
/// </summary>
public class ReverseFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Reverse), 0)
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

    // Optimize Reverse().Reverse() => original collection (double reverse cancels out)
    if (IsLinqMethodChain(source, out var methodName, out var invocation)
        && TryGetLinqSource(invocation, out var invocationSource))
    {
      switch (methodName)
      {
        case nameof(Enumerable.Reverse):
        {
          result = invocationSource;
          return true;
        }
      }
    }

    result = null;
    return false;
  }
}
