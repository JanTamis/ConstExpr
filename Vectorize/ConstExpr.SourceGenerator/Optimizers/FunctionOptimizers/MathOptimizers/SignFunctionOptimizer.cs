using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SignFunctionOptimizer() : BaseMathFunctionOptimizer("Sign", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSignMethodFloat(),
			SpecialType.System_Double => GenerateFastSignMethodDouble(),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Sign call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSignMethodFloat()
	{
		return """
			private static int FastSign(float x)
			{
				if (x == 0.0f)
					return 0;

				var bits = BitConverter.SingleToInt32Bits(x);
				return 1 | (bits >> 31);
			}
			""";
	}

	private static string GenerateFastSignMethodDouble()
	{
		return """
			private static int FastSign(double x)
			{
				if (x == 0.0)
					return 0;

				var bits = BitConverter.DoubleToInt64Bits(x);
				return 1 | (int)(bits >> 63);
			}
			""";
	}
}