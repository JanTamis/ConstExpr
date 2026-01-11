using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
/// Strategy for modulo even detection: (x % 2) != 1 => T.IsEvenInteger(x)
/// </summary>
public class NotEqualsModuloEvenStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseAndExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericOne()
		    || !context.TryGetValue(context.Left.Syntax.Right, out var modValue)
		    || !modValue.IsNumericTwo()
		    || context.Left.Type?.HasMember<IMethodSymbol>(
			    "IsEvenInteger",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Left.Type))) != true)
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(context.Left.Type!.Name),
					IdentifierName("IsEvenInteger")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax.Left))));

		return true;
	}
}

