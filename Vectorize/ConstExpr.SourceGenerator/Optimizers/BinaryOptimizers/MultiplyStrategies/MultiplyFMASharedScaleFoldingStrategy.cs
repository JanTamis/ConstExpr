using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
///   Folds a non-constant multiplier into the shared scale of an existing <c>MultiplyAddEstimate</c>/
///   <c>FusedMultiplyAdd</c> call that represents a <c>b * (1 - x)</c> shape (its two coefficients are exact
///   opposites): <c>FMA(x, -b, b) * k =&gt; FMA(t, -x, t)</c> where <c>t = b * k</c>, e.g.
///   <c>MultiplyAddEstimate(c, -255D, 255D) * diff =&gt; MultiplyAddEstimate(255D * diff, -c, 255D * diff)</c>.
///   Unlike <see cref="MultiplyFMAConstantFoldingStrategy" />, <c>k</c> need not be a compile-time constant here -
///   the duplicated <c>t</c> expression is left in place for CSE to hoist into a shared local.
/// </summary>
public class MultiplyFMASharedScaleFoldingStrategy : SymmetricStrategy<NumericBinaryStrategy, InvocationExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<InvocationExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (RemoveParentheses(context.Left.Syntax) is not InvocationExpressionSyntax invocation
		    || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "MultiplyAddEstimate" or "FusedMultiplyAdd" } member
		    || invocation.ArgumentList.Arguments.Count != 3
		    || context.TryGetValue(context.Right.Syntax, out _))
		{
			// A compile-time constant multiplier is handled by MultiplyFMAConstantFoldingStrategy instead,
			// which can fully precompute the new coefficients.
			return false;
		}

		var x = invocation.ArgumentList.Arguments[0].Expression;
		var a = invocation.ArgumentList.Arguments[1].Expression;
		var b = invocation.ArgumentList.Arguments[2].Expression;

		if (!context.TryGetValue(a, out var aValue) || !context.TryGetValue(b, out var bValue) || !Equals(aValue, bValue.Negate()))
		{
			return false;
		}

		var scaled = MultiplyExpression(b, context.Right.Syntax);

		optimized = InvocationExpression(member, ArgumentList(Argument(scaled), Argument(UnaryMinusExpression(x)), Argument(scaled)));

		return true;
	}
}