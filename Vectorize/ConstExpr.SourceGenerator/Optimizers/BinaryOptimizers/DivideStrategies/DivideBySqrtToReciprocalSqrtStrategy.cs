using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Strategy for reciprocal square root: <c>a / Sqrt(b)</c> => <c>a * ReciprocalSqrtEstimate(b)</c>
///   (and <c>1 / Sqrt(b)</c> => <c>ReciprocalSqrtEstimate(b)</c>), trading a sqrt + divide for a
///   single estimate instruction plus a multiply. Verified codegen on ARM64 (.NET 10, Apple M4 Pro):
///   <c>fsqrt</c> + <c>fdiv</c> becomes <c>frsqrte</c> + <c>fmul</c> for both <c>float</c> and <c>double</c>.
///   On x86 only <c>float</c> has a hardware <c>RSQRTSS</c>; <c>double</c> may fall back to
///   <c>1.0 / Sqrt(x)</c>, making the rewrite break even there rather than win.
///   Requires ReciprocalMath as the estimate carries only ~8-12 bits of mantissa precision.
/// </summary>
public class DivideBySqrtToReciprocalSqrtStrategy : FloatNumberBinaryStrategy<ExpressionSyntax, InvocationExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.ReciprocalMath ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, InvocationExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out _)
		    || context.Right.Syntax is not
		    {
			    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Sqrt" },
			    ArgumentList.Arguments: [ { Expression: var radicand } ]
		    }
		    || !context.Type.HasMember<IMethodSymbol>(
			    "ReciprocalSqrtEstimate",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type))))
		{
			return false;
		}

		// Sqrt(x * x) folds to the exact Abs(x) in SqrtFunctionOptimizer — don't trade that for an estimate.
		if (RemoveParentheses(radicand) is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } mul
		    && SyntaxNodeComparer.Get().Equals(mul.Left, mul.Right))
		{
			return false;
		}

		// ponytail: no 1 / Sqrt(b) special case — the multiply-by-one strategy already drops the `1 *`.
		optimized = MultiplyExpression(
			context.Left.Syntax,
			InvocationExpression(
				MemberAccessExpression(context.Type.AsTypeSyntax(), IdentifierName("ReciprocalSqrtEstimate")),
				ArgumentList(radicand)));

		return true;
	}
}