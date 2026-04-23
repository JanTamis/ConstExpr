using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CoshFunctionOptimizer() : BaseMathFunctionOptimizer("Cosh",n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastCoshMethodFloat()
				: GenerateFastCoshMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastCosh", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastCoshMethodFloat()
	{
		return """
			private static float FastCosh(float x)
			{
				// cosh(x) = (e^x + e^-x) / 2, with cosh(-x) = cosh(x)
				if (Single.IsNaN(x)) return Single.NaN;
				x = Single.Abs(x);
				
				// exp overflows to +Inf for x > ~88.72; return +Inf immediately
				if (x > 88.0f)
					return float.PositiveInfinity;
				
				var ex = Single.Exp(x);
				
				// One Newton-Raphson step on ReciprocalEstimate restores ~24-bit precision
				// (raw estimate is only ~12-bit accurate, which causes ~375× worse error than float epsilon)
				// r' = r * (2 - ex * r)
				var r = Single.ReciprocalEstimate(ex);
				r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);
				
				return (ex + r) * 0.5f;
			}
			""";
	}

	private static string GenerateFastCoshMethodDouble()
	{
		return """
			private static double FastCosh(double x)
			{
				// cosh(x) = (e^x + e^-x) / 2, with cosh(-x) = cosh(x)
				if (Double.IsNaN(x)) return Double.NaN;
				x = Double.Abs(x);
				
				// exp overflows to +Inf for x > ~709.78; return +Inf immediately
				if (x > 709.0)
					return double.PositiveInfinity;
				
				var ex = Double.Exp(x);
				
				// Division gives full double precision for 1/ex.
				// Double.ReciprocalEstimate is only ~14-bit accurate, causing catastrophic
				// precision loss — using FDIV here is both correct and comparable in cost.
				return (ex + 1.0 / ex) * 0.5;
			}
			""";
	}
}
