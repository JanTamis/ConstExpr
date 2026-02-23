using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Union context.Method.
/// Optimizes patterns such as:
/// - collection.Union(Enumerable.Empty&lt;T&gt;()) => collection.Distinct() (union with empty)
/// - Enumerable.Empty&lt;T&gt;().Union(collection) => collection.Distinct()
/// - collection.Union(collection) => collection.Distinct() (same source)
/// </summary>
public class UnionFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Union), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = context.VisitedParameters[0];

		source = context.Visit(source) ?? source;

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		if (TryGetValues(source, out var sourceValues)
		    && TryGetValues(secondSource, out var secondSourceValues)
		    && TryCastToType(context.Loader, sourceValues, context.Method.TypeArguments[0], out var sourceCast)
		    && TryCastToType(context.Loader, secondSourceValues, context.Method.TypeArguments[0], out var secondSourceCast)
		    && context.Loader.TryGetMethodByMethod(context.Method, out var methodInfo)
		    && SyntaxHelpers.TryGetLiteral(methodInfo.Invoke(null, [ sourceCast, secondSourceCast ]), out var literal))
		{
			result = literal;
			return true;
		}

		// Optimize collection.Union(Enumerable.Empty<T>()) => collection.Distinct()
		if (IsEmptyEnumerable(secondSource))
		{
			if (TryGetSyntaxes(source, out var syntaxes))
			{
				var items = syntaxes.Distinct(SyntaxNodeComparer<ExpressionSyntax>.Instance)
					.Select(SyntaxFactory.ExpressionElement);
				
				result = SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList<CollectionElementSyntax>(items));
				return true;
			}
			
			result = CreateSimpleInvocation(source, nameof(Enumerable.Distinct));
			return true;
		}

		// Optimize Enumerable.Empty<T>().Union(collection) => collection.Distinct()
		if (IsEmptyEnumerable(source))
		{
			if (TryGetSyntaxes(secondSource, out var syntaxes))
			{
				var items = syntaxes.Distinct(SyntaxNodeComparer<ExpressionSyntax>.Instance)
					.Select(SyntaxFactory.ExpressionElement);

				result = SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList<CollectionElementSyntax>(items));
				return true;
			}

			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(secondSource, nameof(Enumerable.Distinct)));
			return true;
		}

		// Optimize collection.Union(collection) => collection.Distinct() (same reference)
		if (AreSyntacticallyEquivalent(source, secondSource))
		{
			if (TryGetSyntaxes(source, out var values))
			{
				var tempValues = values.Distinct(SyntaxNodeComparer<ExpressionSyntax>.Instance);

				result = SyntaxFactory.CollectionExpression(
					SyntaxFactory.SeparatedList<CollectionElementSyntax>(
						tempValues.Select(SyntaxFactory.ExpressionElement))
				);
				return true;
			}

			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct)));
			return true;
		}

		result = null;
		return false;
	}
}