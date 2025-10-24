using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for reciprocal optimization: 1 / x => ReciprocalEstimate(x)
/// </summary>
public class DivideOneToReciprocalStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Value.IsNumericOne()
			&& context.Type.HasMember<IMethodSymbol>(
			"ReciprocalEstimate",
			m => m.Parameters.Length == 1
					 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type)));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var host = ParseName(context.Type.Name);
		var reciprocalIdentifier = MemberAccessExpression(
			Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression,
			host,
			IdentifierName("ReciprocalEstimate"));

		return InvocationExpression(
			reciprocalIdentifier,
			ArgumentList(SingletonSeparatedList(Argument(context.Right.Syntax))));
	}
}
