using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SequenceEqual context.Method.
/// Optimizes patterns such as:
/// - collection.SequenceEqual(collection) => true (same reference)
/// - Enumerable.Empty&lt;T&gt;().SequenceEqual(Enumerable.Empty&lt;T&gt;()) => true
/// </summary>
public class SequenceEqualFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SequenceEqual), 1)
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
		
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);
		var secondSource = context.VisitedParameters[0];
		
		// Optimize collection.SequenceEqual(collection) => true (same reference)
		if (AreSyntacticallyEquivalent(source, secondSource))
		{
			result = LiteralExpression(SyntaxKind.TrueLiteralExpression);
			return true;
		}

		if (TryGetEnumerableMethod(context, nameof(Enumerable.Any), 0, out var anyMethod))
		{
			// Optimize Enumerable.Empty<T>().SequenceEqual(collection) => !collection.Any()
			if (IsEmptyEnumerable(source))
			{
				var invocation = CreateInvocation(secondSource, nameof(Enumerable.Any));
				var tempResource = TryOptimizeByOptimizer<AnyFunctionOptimizer>(context.WithInvocationAndMethod(invocation, anyMethod), invocation);
				
				result = InvertSyntax(tempResource as ExpressionSyntax ?? invocation);
				return true;
			}

			// Optimize collection.SequenceEqual(Enumerable.Empty<T>()) => !collection.Any()
			if (IsEmptyEnumerable(secondSource))
			{
				var invocation = CreateInvocation(source, nameof(Enumerable.Any));
				var tempResource = TryOptimizeByOptimizer<AnyFunctionOptimizer>(context.WithInvocationAndMethod(invocation, anyMethod), invocation);

				result = InvertSyntax(tempResource as ExpressionSyntax ?? invocation);
				return true;
			}
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}
		
		result = null;
		return false;
	}
}

