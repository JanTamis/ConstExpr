using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
///   Folds a constant multiplier into an existing <c>MultiplyAddEstimate</c>/<c>FusedMultiplyAdd</c> call:
///   <c>FMA(x, a, b) * k =&gt; FMA(x, a * k, b * k)</c>, e.g.
///   <c>MultiplyAddEstimate(c, -255D, 255D) * 0.6 =&gt; MultiplyAddEstimate(c, -153D, 153D)</c>.
///   Both fma addend/summand and the outer multiplier must be known constants so the new coefficients can
///   be precomputed; requires AssociativeMath since re-associating the multiply changes rounding.
/// </summary>
public class MultiplyFMAConstantFoldingStrategy : SymmetricStrategy<NumericBinaryStrategy, InvocationExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<InvocationExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (RemoveParentheses(context.Left.Syntax) is not InvocationExpressionSyntax invocation
		    || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "MultiplyAddEstimate" or "FusedMultiplyAdd" } member
		    || invocation.ArgumentList.Arguments.Count != 3
		    || !context.TryGetValue(context.Right.Syntax, out var multiplier))
		{
			return false;
		}

		var x = invocation.ArgumentList.Arguments[0];
		var a = invocation.ArgumentList.Arguments[1].Expression;
		var b = invocation.ArgumentList.Arguments[2].Expression;

		if (!context.TryGetValue(a, out var aValue) || !context.TryGetValue(b, out var bValue)
		                                            || !TryCreateLiteral(aValue.Multiply(multiplier), out var newA)
		                                            || !TryCreateLiteral(bValue.Multiply(multiplier), out var newB))
		{
			return false;
		}

		optimized = InvocationExpression(member, ArgumentList(x, Argument(newA), Argument(newB)));

		return true;
	}
}