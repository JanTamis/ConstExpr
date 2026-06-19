using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

public class NotEqualsToLowerStrategy : SymmetricStrategy<InvocationExpressionSyntax, InvocationExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<InvocationExpressionSyntax, InvocationExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Left.Syntax.ArgumentList.Arguments.Count != 1
		    || context.Right.Syntax.ArgumentList.Arguments.Count != 1
		    || context.Left.Syntax.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.Text: "ToLower" } }
		    || context.Right.Syntax.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.Text: "ToLower" } }
		    || !context.Model.Compilation.CreateChar().HasMember<IMethodSymbol>("Equals", m => m.Parameters.Length == 2))
		{
			optimized = null;
			return false;
		}

		optimized = LogicalNotExpression(InvocationExpression(MemberAccessExpression(context.Left.Syntax.ArgumentList.Arguments[0].Expression, IdentifierName("Equals")), ArgumentList([ context.Right.Syntax.ArgumentList.Arguments[0], Argument(MemberAccessExpression(ParseTypeName(nameof(StringComparison)), IdentifierName("CurrentCultureIgnoreCase"))) ])));
		return true;
	}
}