using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToList context.Method.
/// Optimizes patterns such as:
/// - collection.ToList().ToList() => collection.ToList() (redundant ToList)
/// - collection.ToArray().ToList() => collection.ToList()
/// - collection.AsEnumerable().ToList() => collection.ToList()
/// - list.Where(p).ToList() => list.FindAll(p) (direct BCL call, no LINQ pipeline)
/// - list.Select(f).Where(p).ToList() => list.FindAll(x => p(f(x))) (fused selector+predicate)
/// </summary>
public class ToListFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToList), 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Where(p).ToList() → list.FindAll(p)
		// Select(f).Where(p).ToList() → list.FindAll(x => p(f(x)))
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var wherePredicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, MaterializingMethods, out whereSource);

			// Select(f).Where(p).ToList() → list.FindAll(x => p(f(x)))
			if (IsLinqMethodChain(whereSource, nameof(Enumerable.Select), out var selectInvocation)
			    && GetMethodArguments(selectInvocation).FirstOrDefault() is { Expression: { } selectorArg }
			    && TryGetLambda(selectorArg, out var selector)
			    && TryGetLinqSource(selectInvocation, out var selectSource))
			{
				TryGetOptimizedChainExpression(selectSource, MaterializingMethods, out selectSource);

				if (IsListTypeSource(context, selectSource))
				{
					var fusedLambda = CombineLambdas(wherePredicate, selector);
					var visitedFused = context.Visit(fusedLambda) as LambdaExpressionSyntax ?? fusedLambda;
					result = CreateInvocation(
						context.Visit(selectSource) ?? selectSource,
						"FindAll",
						visitedFused);
					return true;
				}
			}

			// Where(p).ToList() → list.FindAll(p)
			if (IsListTypeSource(context, whereSource))
			{
				var visitedPredicate = context.Visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate;
				result = CreateInvocation(
					whereSource,
					"FindAll",
					visitedPredicate);
				return true;
			}
		}

		// Skip all materializing/type-cast operations
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Checks whether <paramref name="expression"/> resolves to a <c>List&lt;T&gt;</c> type.
	/// Uses <c>GetTypeByMetadataName</c> for a reliable comparison that avoids
	/// the potential NullReferenceException in <see cref="IsInvokedOnList"/>.
	/// </summary>
	private static bool IsListTypeSource(FunctionOptimizerContext context, ExpressionSyntax expression)
	{
		var listTypeDef = context.Model.Compilation
			.GetTypeByMetadataName("System.Collections.Generic.List`1");

		if (listTypeDef is null)
		{
			return false;
		}

		ITypeSymbol? type = null;

		// Prefer the tracked variable type for identifiers (reliable across fresh nodes)
		if (expression is IdentifierNameSyntax identifier
		    && context.Variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			type = variable.Type;
		}

		// Fall back to semantic model for non-identifier expressions
		if (type is null)
		{
			context.Model.TryGetTypeSymbol(expression, out type);
		}

		return type is INamedTypeSymbol namedType
		       && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, listTypeDef);
	}
}
