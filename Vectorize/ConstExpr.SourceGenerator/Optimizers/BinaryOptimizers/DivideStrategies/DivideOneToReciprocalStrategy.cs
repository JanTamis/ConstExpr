using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for reciprocal optimization: 1 / x => ReciprocalEstimate(x)
/// Requires ReciprocalMath flag as reciprocal approximation may differ from IEEE 754.
/// </summary>
public class DivideOneToReciprocalStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.ReciprocalMath ];

	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsNumericOne()
		    || !context.Type.HasMember<IMethodSymbol>(
			    "ReciprocalEstimate",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type))))
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
			MemberAccessExpression(ParseName(context.Type.Name), IdentifierName("ReciprocalEstimate")),
			ArgumentList(SingletonSeparatedList(Argument(context.Right.Syntax))));

		return true;
	}
}