using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   Strategy for modulo even detection: (x % 2) != 1 => T.IsEvenInteger(x)
///   x % 2 != 1 is not sign-invariant (-3 % 2 == -1 in C#, so -3 % 2 != 1 is true even though -3 is odd),
///   so this only holds for unsigned types or signed values proven non-negative via sibling comparisons
///   (see IsPositive, mirroring ModuloByPowerOfTwoStrategy).
/// </summary>
public class NotEqualsModuloEvenStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.ModuloExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericOne()
		    || !context.TryGetValue(context.Left.Syntax.Right, out var modValue)
		    || !modValue.IsNumericTwo()
		    || context.Left.Type?.HasMember<IMethodSymbol>(
			    "IsEvenInteger",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Left.Type))) != true
		    || !(context.Left.Type!.IsUnsignedInteger() || IsPositive(new BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax>
		    {
			    BinaryExpressions = context.BinaryExpressions,
			    Variables = context.Variables
		    }, context.Left.Syntax.Left)))
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
				MemberAccessExpression(ParseTypeName(context.Left.Type!.Name), IdentifierName("IsEvenInteger")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax.Left))));

		return true;
	}
}