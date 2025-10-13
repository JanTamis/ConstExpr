using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SinPiFunctionOptimizer() : BaseFunctionOptimizer("SinPi", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSinPiMethodFloat()
				: GenerateFastSinPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSinPi", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastSinPiMethodFloat()
	{
		return """
			private static float FastSinPi(float x)
			{
				// Fast sine(π*x) approximation using optimized minimax polynomial
				// SinPi(x) = Sin(π*x)
				
				// Range reduction: bring x to [-1, 1]
				x = x - Single.Floor(x / 2.0f) * 2.0f;
				if (x > 1.0f) x -= 2.0f;
				if (x < -1.0f) x += 2.0f;
				
				// Store original sign and work with absolute value
				var originalSign = x;
				x = Single.Abs(x);
				
				// For better accuracy, split into two ranges
				if (x <= 0.5f)
				{
					// For x in [0, 0.5], use polynomial directly
					var px = x * Single.Pi;
					var px2 = px * px;
					// Taylor series coefficients for sin(x)
					var ret = -0.00019840874f;
					ret = Single.FusedMultiplyAdd(ret, px2, 0.0083333310f);
					ret = Single.FusedMultiplyAdd(ret, px2, -0.16666667f);
					ret = Single.FusedMultiplyAdd(ret, px2, 1.0f);
					ret = ret * px;
					// Apply original sign using CopySign
					return Single.CopySign(ret, originalSign);
				}
				
				// For x in (0.5, 1], use sin(π*x) = sin(π*(1-x))
				var px = (1.0f - x) * Single.Pi;
				var px2 = px * px;
				var ret = -0.00019840874f;
				ret = Single.FusedMultiplyAdd(ret, px2, 0.0083333310f);
				ret = Single.FusedMultiplyAdd(ret, px2, -0.16666667f);
				ret = Single.FusedMultiplyAdd(ret, px2, 1.0f);
				ret = ret * px;
				// Apply original sign using CopySign
				return Single.CopySign(ret, originalSign);
			}
			""";
	}

	private static string GenerateFastSinPiMethodDouble()
	{
		return """
			private static double FastSinPi(double x)
			{
				// Fast sine(π*x) approximation for double precision
				// SinPi(x) = Sin(π*x)
				
				// Range reduction: bring x to [-1, 1]
				x = x - Double.Floor(x / 2.0) * 2.0;
				if (x > 1.0) x -= 2.0;
				if (x < -1.0) x += 2.0;
				
				// Store original sign and work with absolute value
				var originalSign = x;
				x = Double.Abs(x);
				
				// For better accuracy, split into two ranges
				if (x <= 0.5)
				{
					// For x in [0, 0.5], use polynomial directly
					var px = x * Double.Pi;
					var px2 = px * px;
					// Higher precision Taylor series coefficients for sin(x)
					var ret = 2.7557313707070068e-6;
					ret = Double.FusedMultiplyAdd(ret, px2, -0.00019841269841201856);
					ret = Double.FusedMultiplyAdd(ret, px2, 0.0083333333333331650);
					ret = Double.FusedMultiplyAdd(ret, px2, -0.16666666666666666);
					ret = Double.FusedMultiplyAdd(ret, px2, 1.0);
					ret = ret * px;
					// Apply original sign using CopySign
					return Double.CopySign(ret, originalSign);
				}
				
				// For x in (0.5, 1], use sin(π*x) = sin(π*(1-x))
				var px = (1.0 - x) * Double.Pi;
				var px2 = px * px;
				var ret = 2.7557313707070068e-6;
				ret = Double.FusedMultiplyAdd(ret, px2, -0.00019841269841201856);
				ret = Double.FusedMultiplyAdd(ret, px2, 0.0083333333333331650);
				ret = Double.FusedMultiplyAdd(ret, px2, -0.16666666666666666);
				ret = Double.FusedMultiplyAdd(ret, px2, 1.0);
				ret = ret * px;
				// Apply original sign using CopySign
				return Double.CopySign(ret, originalSign);
			}
			""";
	}
}
