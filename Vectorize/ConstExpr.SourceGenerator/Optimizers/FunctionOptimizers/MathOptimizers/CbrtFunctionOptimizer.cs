using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CbrtFunctionOptimizer() : BaseMathFunctionOptimizer("Cbrt", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastCbrtMethodFloat()
				: GenerateFastCbrtMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastCbrt", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

		private static string GenerateFastCbrtMethodFloat()
	{
		return """
			private static float FastCbrt(float x)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				if (x == 0.0f)
					return 0.0f;
				
				var absX = Single.Abs(x);
				
				// Initial approximation using bit manipulation (~7 significant bits)
				var i = BitConverter.SingleToInt32Bits(absX);
				i = 0x2a517d47 + i / 3;
				var y = BitConverter.Int32BitsToSingle(i);
				
				// Single Halley iteration: y = y * (y³ + 2a) / (2y³ + a)
				// Cubic convergence: 7 bits → ~21 bits in one step (vs two Newton steps for ~20 bits).
				// One division instead of two — benchmarked at ~2× faster than the 2×Newton approach.
				var y2 = y * y;
				var y3 = y2 * y;
				var twoA = absX + absX;
				y = y * Single.FusedMultiplyAdd(1.0f, y3, twoA) / Single.FusedMultiplyAdd(2.0f, y3, absX);
				
				return Single.CopySign(y, x);
			}
			""";
	}

		private static string GenerateFastCbrtMethodDouble()
	{
		return """
			private static double FastCbrt(double x)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				if (x == 0.0)
					return 0.0;
				
				var absX = Double.Abs(x);
				
				// Initial approximation using bit manipulation (~8 significant bits)
				var i = BitConverter.DoubleToInt64Bits(absX);
				i = 0x2a9f8b7cef1d0da0L + i / 3;
				var y = BitConverter.Int64BitsToDouble(i);
				
				// 1× Newton: y = (2y + a/y²) / 3  — reaches ~16 bits
				y = (y + y + absX / (y * y)) / 3.0;
				
				// 1× Halley: y = y * (y³ + 2a) / (2y³ + a)
				// Cubic convergence from 16 bits → ~48 bits (vs 2×Newton which only reached ~32 bits).
				// Same two-division cost as the previous 2×Newton implementation.
				var y2 = y * y;
				var y3 = y2 * y;
				var twoA = absX + absX;
				y = y * Double.FusedMultiplyAdd(1.0, y3, twoA) / Double.FusedMultiplyAdd(2.0, y3, absX);
				
				return Double.CopySign(y, x);
			}
			""";
	}
}
