using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TanPiFunctionOptimizer() : BaseMathFunctionOptimizer("TanPi",n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// TanPi(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.25) => 1 (tan(?/4) = 1)
			if (IsApproximately(value, 0.25))
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-0.25) => -1 (tan(-?/4) = -1)
			if (IsApproximately(value, -0.25))
			{
				result = CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.5) => undefined (asymptote at ?/2), but mathematically approaches infinity
			// TanPi(1.0) => 0 (tan(?) = 0)
			if (IsApproximately(value, 1.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-1.0) => 0 (tan(-?) = 0)
			if (IsApproximately(value, -1.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanPiMethodFloat()
				: GenerateFastTanPiMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTanPi", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

	private static string GenerateFastTanPiMethodFloat()
	{
		// V3: fold to [0,0.25] via cotangent + absorbed-π [2/2] Padé at xf (no xf·π multiply).
		// tanPi(xf) = xf·(C1 + C3·xf² + C5·xf⁴) / (1 + D2·xf² + D4·xf⁴)
		//   C1 = π,  C3 = −π³/9,  C5 = π⁵/945
		//   D2 = −4π²/9,  D4 = π⁴/63
		// Benchmark (Apple M4 Pro, .NET 10, ARM64):
		//   float.TanPi=2.477 ns | Current=1.266 ns | V2=1.179 ns | V3≈1.155 ns (−53% vs .NET)
		return """
			private static float FastTanPi(float x)
			{
				// Range reduce to [-0.5, 0.5] — TanPi period is 1
				if (Single.IsNaN(x)) return Single.NaN;
				x -= Single.Round(x);
				
				var signX = x;
				x = Single.Abs(x); // [0, 0.5]
				
				// Fold (0.25, 0.5) to [0, 0.25] via cotangent: tanPi(x) = 1/tanPi(0.5-x)
				var swap = x > 0.25f;
				var xf   = swap ? 0.5f - x : x;
				var u2   = xf * xf;
				
				// Absorbed-pi [2/2] Pade: tanPi(xf) = xf*(C1 + C3*xf^2 + C5*xf^4) / (1 + D2*xf^2 + D4*xf^4)
				const float C1 =  3.14159265f;   //  pi
				const float C3 = -3.44514185f;   // -pi^3/9
				const float C5 =  0.32383247f;   //  pi^5/945
				const float D2 = -4.38649084f;   // -4*pi^2/9
				const float D4 =  1.54617606f;   //  pi^4/63
				
				var num = Single.FusedMultiplyAdd(C5, u2, C3);
				num     = Single.FusedMultiplyAdd(num, u2, C1);
				num    *= xf;
				var den = Single.FusedMultiplyAdd(D4, u2, D2);
				den     = Single.FusedMultiplyAdd(den, u2, 1.0f);
				
				var t = num / den;
				if (swap) t = 1.0f / t;
				
				return Single.CopySign(t, signX);
			}
			""";
	}

	private static string GenerateFastTanPiMethodDouble()
	{
		// V3: fold to [0,0.25] via cotangent + absorbed-π [2/3] Padé at xf (no xf·π multiply).
		// tanPi(xf) = xf·(C1 + C3·xf² + C5·xf⁴) / (1 + D2·xf² + D4·xf⁴ + D6·xf⁶)
		//   C1 = π,  C3 = −4π³/33,  C5 = π⁵/495
		//   D2 = −5π²/11,  D4 = 2π⁴/99,  D6 = −π⁶/10395
		// Benchmark (Apple M4 Pro, .NET 10, ARM64):
		//   double.TanPi=3.410 ns | Current=1.513 ns | V2=1.248 ns | V3=1.227 ns (−64% vs .NET)
		return """
			private static double FastTanPi(double x)
			{
				// Range reduce to [-0.5, 0.5] — TanPi period is 1
				if (Double.IsNaN(x)) return Double.NaN;
				x -= Double.Round(x);
				
				var signX = x;
				x = Double.Abs(x); // [0, 0.5]
				
				// Fold (0.25, 0.5) to [0, 0.25] via cotangent: tanPi(x) = 1/tanPi(0.5-x)
				var swap = x > 0.25;
				var xf   = swap ? 0.5 - x : x;
				var u2   = xf * xf;
				
				// Absorbed-pi [2/3] Pade: tanPi(xf) = xf*(C1 + C3*xf^2 + C5*xf^4) / (1 + D2*xf^2 + D4*xf^4 + D6*xf^6)
				const double C1 =  3.14159265358979324;   //  pi
				const double C3 = -3.75833657307876;       // -4*pi^3/33
				const double C5 =  0.61822157532380;       //  pi^5/495
				const double D2 = -4.48618381867698;       // -5*pi^2/11
				const double D4 =  1.96786042492934;       //  2*pi^4/99
				const double D6 = -0.09248641780;          // -pi^6/10395
				
				var num = Double.FusedMultiplyAdd(C5, u2, C3);
				num     = Double.FusedMultiplyAdd(num, u2, C1);
				num    *= xf;
				var den = Double.FusedMultiplyAdd(D6, u2, D4);
				den     = Double.FusedMultiplyAdd(den, u2, D2);
				den     = Double.FusedMultiplyAdd(den, u2, 1.0);
				
				var t = num / den;
				if (swap) t = 1.0 / t;
				
				return Double.CopySign(t, signX);
			}
			""";
	}
}
