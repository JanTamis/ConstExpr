using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SequenceEqual context.Method.
/// Optimizes patterns such as:
/// - collection.SequenceEqual(collection) => true (same reference)
/// - Enumerable.Empty&lt;T&gt;().SequenceEqual(Enumerable.Empty&lt;T&gt;()) => true
/// </summary>
public class SequenceEqualFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SequenceEqual), 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
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
				
				result = (tempResource as ExpressionSyntax ?? invocation).InvertSyntax();
				return true;
			}

			// Optimize collection.SequenceEqual(Enumerable.Empty<T>()) => !collection.Any()
			if (IsEmptyEnumerable(secondSource))
			{
				var invocation = CreateInvocation(source, nameof(Enumerable.Any));
				var tempResource = TryOptimizeByOptimizer<AnyFunctionOptimizer>(context.WithInvocationAndMethod(invocation, anyMethod), invocation);

				result = (tempResource as ExpressionSyntax ?? invocation).InvertSyntax();
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

