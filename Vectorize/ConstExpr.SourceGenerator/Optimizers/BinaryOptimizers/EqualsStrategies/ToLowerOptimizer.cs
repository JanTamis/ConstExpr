using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// strategy for string.ToLower() detection: x.ToLower() == "constant" => x.Equals("constant", StringComparison.OrdinalIgnoreCase)
/// </summary>
public class ToLowerOptimizer() : SymmetricStrategy<StringBinaryStrategy, MemberAccessExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.SimpleMemberAccessExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<MemberAccessExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Right.Syntax.Token.Value is not string rightValue
		    || !context.Left.Syntax.Name.Identifier.ValueText.Equals("ToLower"))
		{
			optimized = null;
			return false;
		}
		
		optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					context.Left.Syntax.Expression,
					IdentifierName("Equals")))
			.WithArgumentList(
				ArgumentList(
					SeparatedList<ArgumentSyntax>(
						new SyntaxNodeOrToken[]
						{
							Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(rightValue))),
							Token(SyntaxKind.CommaToken),
							Argument(MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("StringComparison"),
								IdentifierName("CurrentCultureIgnoreCase")))
						})));
		return true;
	}
}