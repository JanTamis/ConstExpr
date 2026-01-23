using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class WhereFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Where), 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			result = null;
			return false;
		}

		// Optimize Where(v => true) - remove entirely
		if (IsAlwaysTrueLambda(lambda))
		{
			result = memberAccess.Expression;
			return true;
		}

		// Optimize Where(v => false) - replace with Empty
		if (IsAlwaysFalseLambda(lambda))
		{
			result = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.ParseTypeName("System.Linq.Enumerable"),
					SyntaxFactory.GenericName(
						SyntaxFactory.Identifier("Empty"))
						.WithTypeArgumentList(
							SyntaxFactory.TypeArgumentList(
								SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
									SyntaxFactory.IdentifierName("T"))))));
			return true;
		}

		// Combine consecutive Where calls: source.Where(a).Where(b) => source.Where(a && b)
		if (memberAccess.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: nameof(Enumerable.Where) } innerMemberAccess, ArgumentList.Arguments.Count: 1 } innerInvocation
		    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda))
		{
			// Combine the two predicates with &&
			var combinedLambda = CombinePredicates(lambda, innerLambda);

			result = invocation
				.WithExpression(SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					innerMemberAccess.Expression,
					SyntaxFactory.IdentifierName(nameof(Enumerable.Where))))
				.WithArgumentList(SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(combinedLambda))));

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

	private LambdaExpressionSyntax CombinePredicates(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// If parameters are the same, we can directly combine with &&
		// Otherwise, replace the outer parameter with the inner parameter
		ExpressionSyntax combinedBody;
		if (innerParam == outerParam)
		{
			// Both use the same parameter name: v => v > 1 && v < 5
			combinedBody = SyntaxFactory.BinaryExpression(
				SyntaxKind.LogicalAndExpression,
				SyntaxFactory.ParenthesizedExpression(innerBody),
				SyntaxFactory.ParenthesizedExpression(outerBody));
		}
		else
		{
			// Different parameter names: replace outer parameter with inner parameter
			var renamedOuterBody = ReplaceIdentifier(outerBody, outerParam, SyntaxFactory.IdentifierName(innerParam));
			combinedBody = SyntaxFactory.BinaryExpression(
				SyntaxKind.LogicalAndExpression,
				SyntaxFactory.ParenthesizedExpression(innerBody),
				SyntaxFactory.ParenthesizedExpression(renamedOuterBody));
		}

		// Create a new lambda with the inner parameter and the combined body
		return SyntaxFactory.SimpleLambdaExpression(
			SyntaxFactory.Parameter(SyntaxFactory.Identifier(innerParam)),
			combinedBody
		);
	}

	private static string GetLambdaParameter(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: > 0 } parenthesizedLambda
				=> parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => throw new System.InvalidOperationException("Unsupported lambda expression type")
		};
	}

	private static ExpressionSyntax GetLambdaBody(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax { ExpressionBody: { } body } => body,
			ParenthesizedLambdaExpressionSyntax { ExpressionBody: { } body } => body,
			_ => throw new System.InvalidOperationException("Only expression-bodied lambdas are supported")
		};
	}

	private static ExpressionSyntax ReplaceIdentifier(ExpressionSyntax expression, string oldIdentifier, ExpressionSyntax replacement)
	{
		return (ExpressionSyntax)new IdentifierReplacer(oldIdentifier, replacement).Visit(expression);
	}

	private class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (node.Identifier.Text == identifier)
			{
				return replacement;
			}

			return base.VisitIdentifierName(node);
		}
	}
}
