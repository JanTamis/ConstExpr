using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
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
	private static readonly HashSet<string> OperationsThatDontAffectUnion =
	[
		..MaterializingMethods,
		nameof(Enumerable.Distinct), // union already applies Distinct
	];
	
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var secondSource = context.VisitedParameters[0];
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectUnion, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (TryGetValues(source, out var sourceValues)
		    && TryGetValues(secondSource, out var secondSourceValues)
		    && TryCastToType(context.Loader, sourceValues, context.Method.TypeArguments[0], out var sourceCast)
		    && TryCastToType(context.Loader, secondSourceValues, context.Method.TypeArguments[0], out var secondSourceCast)
		    && context.Loader.TryGetMethodByMethod(context.Method, out var methodInfo)
		    && TryCreateLiteral(methodInfo.Invoke(null, [ sourceCast, secondSourceCast ]), out var literal))
		{
			result = literal;
			return true;
		}

		// Optimize collection.Union(Enumerable.Empty<T>()) => collection.Distinct()
		if (IsEmptyEnumerable(secondSource))
		{
			if (TryGetSyntaxes(source, out var syntaxes))
			{
				var items = syntaxes
					.Distinct(SyntaxNodeComparer.Get<ExpressionSyntax>())
					.Select(ExpressionElement);
				
				result = CollectionExpression(SeparatedList<CollectionElementSyntax>(items));
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
				var items = syntaxes
					.Distinct(SyntaxNodeComparer.Get<ExpressionSyntax>())
					.Select(ExpressionElement);

				result = CollectionExpression(SeparatedList<CollectionElementSyntax>(items));
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
				var tempValues = values.Distinct(SyntaxNodeComparer.Get<ExpressionSyntax>());

				result = CollectionExpression(
					SeparatedList<CollectionElementSyntax>(
						tempValues.Select(ExpressionElement)));
				return true;
			}

			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct)));
			return true;
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