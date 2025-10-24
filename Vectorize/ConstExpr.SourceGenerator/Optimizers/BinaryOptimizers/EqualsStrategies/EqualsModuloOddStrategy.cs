using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for modulo odd detection: (x % 2) == 1 => T.IsOddInteger(x)
/// </summary>
public class EqualsModuloOddStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Value.IsNumericOne()
				 && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } modExpr
				 && modExpr.Right is LiteralExpressionSyntax { Token.Value: var modValue } && modValue.IsNumericValue(2)
				 && context.Left.Type?.HasMember<IMethodSymbol>(
			"IsOddInteger",
						m => m.Parameters.Length == 1
							&& m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Left.Type))) == true;
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax	{ RawKind: (int)SyntaxKind.ModuloExpression } modExpr)
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
						Argument(modExpr.Left))));
	}
}
