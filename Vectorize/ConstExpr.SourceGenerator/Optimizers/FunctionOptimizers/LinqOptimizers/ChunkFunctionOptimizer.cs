using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
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
			
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Select), lambda);
			return true;
		}

		result = null;
		return false;
	}
}
