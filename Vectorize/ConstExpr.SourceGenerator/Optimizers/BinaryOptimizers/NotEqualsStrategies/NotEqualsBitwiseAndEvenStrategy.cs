using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
/// Strategy for bitwise AND even detection: (x & 1) != 1 => T.IsEvenInteger(x)
/// </summary>
public class NotEqualsBitwiseAndEvenStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right is { HasValue: true, Value: { } value } && value.IsNumericOne()
					&& context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr
					&& andExpr.Right is LiteralExpressionSyntax { Token.Value: var andValue } && andValue.IsNumericOne()
					&& context.Left.Type?.HasMember<IMethodSymbol>(
					"IsEvenInteger",
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
				IdentifierName("IsEvenInteger")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(andExpr.Left))));
	}
}

