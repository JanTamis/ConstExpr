using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SelectFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Select), 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLambda(invocation.ArgumentList.Arguments[0], out var lambda)
		    || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			result = null;
			return false;
		}

		if (IsIdentityLambda(lambda))
		{
			result = memberAccess.Expression;
			return true;
		}
		
		// check if memberAccess.Expression is another Select call
		if (memberAccess.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: nameof(Enumerable.Select) } innerMemberAccess, ArgumentList.Arguments.Count: 1 } innerInvocation 
		    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0], out var innerLambda))
		{
			// Combine the two lambdas: source.Select(inner).Select(outer) => source.Select(combined)
			var combinedLambda = CombineLambdas(lambda, innerLambda);
			
			// Create a new Select call with the combined lambda
			result = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					innerMemberAccess.Expression,
					SyntaxFactory.IdentifierName(nameof(Enumerable.Select))
				),
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(combinedLambda))
				)
			);
			return true;
		}
		
		result = null;
		return false;
	}
	
	private LambdaExpressionSyntax CombineLambdas(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);
		
		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);
		
		// Replace the outer lambda's parameter with the inner lambda's body
		var combinedBody = ReplaceIdentifier(outerBody, outerParam, innerBody);
		
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