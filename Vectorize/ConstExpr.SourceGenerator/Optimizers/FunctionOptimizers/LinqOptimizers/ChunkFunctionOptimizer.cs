using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Chunk context.Method.
/// Optimizes patterns such as:
/// - collection.Chunk(size).Count() => (collection.Count() + size - 1) / size (ceiling division)
/// - collection.Chunk(n).First() => collection.Take(n).ToArray()
/// - collection.Chunk(n).Last() => collection.TakeLast(n).ToArray()
/// - collection.Chunk(1) => collection.Select(x => new[] { x })
/// </summary>
public class ChunkFunctionOptimizer() : BaseLinqFunctionOptimizer("Chunk", 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		var chunkSize = context.VisitedParameters[0];

		// Optimization: Chunk(1) => Select(x => new[] { x })
		if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
		{
			var parameter = Parameter(Identifier("x"));
			var lambdaBody = CreateImplicitArray(IdentifierName("x"));
			var lambda = SimpleLambdaExpression(parameter, lambdaBody);
			
			result = CreateInvocation(source, nameof(Enumerable.Select), lambda);
			return true;
		}

		result = null;
		return false;
	}
}
