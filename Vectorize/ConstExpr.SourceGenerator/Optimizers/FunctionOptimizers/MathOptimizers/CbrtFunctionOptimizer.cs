using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CbrtFunctionOptimizer() : BaseMathFunctionOptimizer("Cbrt", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		var arg = context.VisitedParameters[0];

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastCbrtMethodFloat()
				: GenerateFastCbrtMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

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
				if (x == 0.0f)
					return 0.0f;
				
				var absX = Single.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.SingleToInt32Bits(absX);
				i = 0x2a517d47 + i / 3;
				var y = BitConverter.Int32BitsToSingle(i);
				
				// Newton-Raphson iteration: y = (2*y + x/y) / 3
				y = (y + y + absX / (y * y)) / 3.0f;
				y = (y + y + absX / (y * y)) / 3.0f;
				
				return Single.CopySign(y, x);
			}
			""";
	}

	private static string GenerateFastCbrtMethodDouble()
	{
		return """
			private static double FastCbrt(double x)
			{
				if (x == 0.0)
					return 0.0;
				
				var absX = Double.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.DoubleToInt64Bits(absX);
				i = 0x2a9f8b7cef1d0da0L + i / 3;
				var y = BitConverter.Int64BitsToDouble(i);
				
				// Newton-Raphson iteration: y = (2*y + x/y�) / 3
				y = (y + y + absX / (y * y)) / 3.0;
				y = (y + y + absX / (y * y)) / 3.0;
				
				return Double.CopySign(y, x);
			}
			""";
	}
}
