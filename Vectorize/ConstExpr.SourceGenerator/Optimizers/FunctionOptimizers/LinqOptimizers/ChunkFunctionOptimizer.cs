using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Chunk method.
/// Optimizes patterns such as:
/// - collection.Chunk(size).Count() => (collection.Count() + size - 1) / size (ceiling division)
/// - collection.Chunk(n).First() => collection.Take(n).ToArray()
/// - collection.Chunk(n).Last() => collection.TakeLast(n).ToArray()
/// - collection.Chunk(1) => collection.Select(x => new[] { x })
/// </summary>
public class ChunkFunctionOptimizer() : BaseLinqFunctionOptimizer("Chunk", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var chunkSize = parameters[0];

		// Optimization: Chunk(1) => Select(x => new[] { x })
		if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
		{
			var parameter = Parameter(Identifier("x"));
			var lambdaBody = ImplicitArrayCreationExpression(
				InitializerExpression(SyntaxKind.ArrayInitializerExpression,
					SingletonSeparatedList<ExpressionSyntax>(
						IdentifierName("x"))));

			var lambda = SimpleLambdaExpression(parameter, lambdaBody);
			
			result = CreateInvocation(source, nameof(Enumerable.Select), lambda);
			return true;
		}

		result = null;
		return false;
	}
}
