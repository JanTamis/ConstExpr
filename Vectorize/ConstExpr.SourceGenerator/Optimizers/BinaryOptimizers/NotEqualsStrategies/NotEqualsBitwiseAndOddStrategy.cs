using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
/// Strategy for bitwise AND odd detection: (x & 1) != 0 => T.IsOddInteger(x)
/// </summary>
public class NotEqualsBitwiseAndOddStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right is { HasValue: true, Value: { } value } && value.IsNumericZero()
					&& context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr
					&& andExpr.Right is LiteralExpressionSyntax { Token.Value: var andValue } && andValue.IsNumericOne()
					&& context.Left.Type?.HasMember<IMethodSymbol>(
					"IsOddInteger",
					m => m.Parameters.Length == 1
							 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Left.Type))) == true;
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax
		    { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr)
		{
			return null;
		}

		return InvocationExpression(
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				ParseTypeName(context.Left.Type!.Name),
				IdentifierName("IsOddInteger")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(andExpr.Left))));
	}
}

