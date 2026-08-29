using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BigMulFunctionOptimizer() : BaseMathFunctionOptimizer("BigMul", n => n is 2)
{
	// `(long)a * (long)b` / `(ulong)a * (ulong)b` is exactly what Math.BigMul(int,int) / (uint,uint)
	// compute — a widening multiply that cannot overflow. Integer-only, no edge cases. Safe in strict mode.
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Math.BigMul(int a, int b) → (long)a * (long)b
		// Math.BigMul(uint a, uint b) → (ulong)a * (ulong)b
		// This inlines the widening multiply, avoiding the Math.BigMul dispatch overhead.
		SyntaxKind targetKeyword;

		switch (paramType.SpecialType)
		{
			case SpecialType.System_Int32:
			{
				targetKeyword = SyntaxKind.LongKeyword;
				break;
			}
			case SpecialType.System_UInt32:
			{
				targetKeyword = SyntaxKind.ULongKeyword;
				break;
			}
			default:
			{
				result = null;
				return false;
			}
		}

		var targetType = PredefinedType(Token(targetKeyword));

		// Cast both operands to the wider type to ensure the widening multiply.
		var castLeft = CastExpression(targetType, ParenthesizedExpression(left));
		var castRight = CastExpression(targetType, ParenthesizedExpression(right));
		var multiply = MultiplyExpression(castLeft, castRight);

		result = context.Visit(multiply) ?? multiply;
		return true;
	}
}