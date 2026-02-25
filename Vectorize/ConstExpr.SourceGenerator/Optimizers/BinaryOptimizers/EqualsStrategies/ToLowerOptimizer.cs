using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// strategy for string.ToLower() detection: x.ToLower() == "constant" => x.Equals("constant", StringComparison.OrdinalIgnoreCase)
/// </summary>
public class ToLowerOptimizer() : SymmetricStrategy<InvocationExpressionSyntax, LiteralExpressionSyntax>(SyntaxKind.InvocationExpression, SyntaxKind.StringLiteralExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<InvocationExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Left.Syntax.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "ToLower" } memberAccess)
		{
			optimized = null;
			return false;
		}
		
		optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName("String"),
					IdentifierName("Equals")))
			.WithArgumentList(
				ArgumentList(
					SeparatedList<ArgumentSyntax>(
						new SyntaxNodeOrToken[]
						{
							Argument(memberAccess.Expression),
							Token(SyntaxKind.CommaToken),
							Argument(context.Right.Syntax),
							Token(SyntaxKind.CommaToken),
							Argument(MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("StringComparison"),
								IdentifierName("CurrentCultureIgnoreCase")))
						})));
		return true;
	}
}