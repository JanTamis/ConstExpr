using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for reciprocal optimization: 1 / x => ReciprocalEstimate(x)
/// </summary>
public class DivideOneToReciprocalStrategy : BaseBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.TryGetLiteral(context.Left.Syntax, out var value)
		    || !value.IsNumericOne()
		    || !context.Type.HasMember<IMethodSymbol>(
			    "ReciprocalEstimate",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type))))
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				ParseName(context.Type.Name),
				IdentifierName("ReciprocalEstimate")),
			ArgumentList(SingletonSeparatedList(Argument(context.Right.Syntax))));

		return true;
	}
}