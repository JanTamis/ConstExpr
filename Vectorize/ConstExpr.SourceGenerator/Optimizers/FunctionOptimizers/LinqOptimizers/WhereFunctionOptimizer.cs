using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class WhereFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Where), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Where(v => true) - remove entirely
		if (IsAlwaysTrueLambda(lambda))
		{
			result = source;
			return true;
		}

		// Optimize Where(v => false) - replace with Empty
		if (IsAlwaysFalseLambda(lambda))
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		// Combine consecutive Where calls: source.Where(a).Where(b) => source.Where(a && b)
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var innerInvocation)
		    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda)
		    && TryGetLinqSource(innerInvocation, out var innerSource))
		{
			// Combine the two predicates with &&
			var combinedLambda = CombinePredicates(lambda, innerLambda);

			// Create a new Where call with the combined lambda
			result = CreateInvocation(innerSource, nameof(Enumerable.Where), combinedLambda);
			return true;
		}

		result = null;
		return false;
	}

	private static bool IsAlwaysTrueLambda(LambdaExpressionSyntax lambda)
	{
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		return body is LiteralExpressionSyntax { Token.Value: true };
	}

	private static bool IsAlwaysFalseLambda(LambdaExpressionSyntax lambda)
	{
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		return body is LiteralExpressionSyntax { Token.Value: false };
	}

	
}
