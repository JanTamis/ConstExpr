using System;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

public class NotEqualsToLowerOptimizer : CharBinaryStrategy<InvocationExpressionSyntax, InvocationExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<InvocationExpressionSyntax, InvocationExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Check if both sides are invocations of ToLower()
		if (!base.TryOptimize(context, out optimized)
		    || !IsToLowerInvocation(context.Left.Syntax)
		    || !IsToLowerInvocation(context.Right.Syntax))
		{
			return false;
		}
		
		var leftArgument = context.Left.Syntax.ArgumentList.Arguments.FirstOrDefault()?.Expression;
		var rightArgument = context.Right.Syntax.ArgumentList.Arguments.FirstOrDefault()?.Expression;

		if (leftArgument != null && rightArgument != null)
		{
			// Create !Char.Equals(leftArgument, rightArgument) expression
			optimized = LogicalNotExpression(
				InvocationExpression(
					MemberAccessExpression(IdentifierName("Char"), IdentifierName("Equals")),
					ArgumentList(
						SeparatedList([
							Argument(leftArgument),
							Argument(rightArgument)
						])
					)
				)
			);


			return true;
		}
		
		return false;
	}
	
	private bool IsToLowerInvocation(InvocationExpressionSyntax invocation)
	{
		return invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: nameof(Char.ToLower) };
	}
}