using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RootNFunctionOptimizer() : BaseMathFunctionOptimizer("RootN", n => n is 2), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var n = context.VisitedParameters[1];

		// Try to get the n value if it's a literal
		if (TryGetIntegerLiteral(n, out var nValue))
		{
			switch (nValue)
			{
				// RootN(x, 1) => x
				case 1:
				{
					result = x;
					return true;
				}
				// RootN(x, 2) => Sqrt(x)
				case 2 when HasMethod(paramType, "Sqrt", 1):
				{
					result = CreateInvocation(paramType, "Sqrt", x);
					return true;
				}
				// RootN(x, 3) => Cbrt(x)
				case 3 when HasMethod(paramType, "Cbrt", 1):
				{
					result = CreateInvocation(paramType, "Cbrt", x);
					return true;
				}
				// RootN(x, -1) => Reciprocal(x) or 1/x
				case -1 when HasMethod(paramType, "Reciprocal", 1):
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}
				case -1:
				{
					var div = DivideExpression(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0)), x);

					result = ParenthesizedExpression(div);
					return true;
				}
				// For negative n: RootN(x, -n) => Reciprocal(RootN(x, n)) if available and fast-math, otherwise 1 / RootN(x, n)
				case < 0:
				{
					var positiveN = LiteralExpression(SyntaxKind.NumericLiteralExpression,
						Literal(-nValue));

					var rootInvocation = CreateInvocation(paramType, "RootN", x, positiveN);

					if (HasMethod(paramType, "Reciprocal", 1))
					{
						result = CreateInvocation(paramType, "Reciprocal", rootInvocation);
						return true;
					}

					var div = DivideExpression(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0)),
						rootInvocation);

					result = ParenthesizedExpression(div);
					return true;
				}
			}
		}

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastRootNMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastRootNMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static bool TryGetIntegerLiteral(ExpressionSyntax expr, out int value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: int i }:
			{
				value = i;
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: int i2 } }:
			{
				value = -i2;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	private static string GenerateFastRootNMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast n-th root implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses logarithmic reduction and a polynomial exp2 approximation. Supports negative exponents and odd roots of negative values.</remarks>")
			.WriteLine("/// <param name=\"x\">The radicand.</param>")
			.WriteLine("/// <param name=\"n\">The root degree.</param>")
			.WriteLine("/// <returns>The approximate real n-th root of x.</returns>")
			.WriteLine("private static float FastRootN(float x, int n)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (n == 0) return float.NaN;")
			.WriteLine("if (n == 1) return x;")
			.WriteLine("if (x == 0.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine("if (n < 0) return 1.0f / FastRootN(x, -n);")
			.WriteWhitespace()
			.WriteLine("var ax = x < 0.0f ? -x : x;")
			.WriteWhitespace()
			.WriteLine("var lBits = BitConverter.SingleToInt32Bits(ax);")
			.WriteLine("var lE    = (lBits >> 23) - 127;")
			.WriteLine("var lM    = BitConverter.Int32BitsToSingle((lBits & 0x007FFFFF) | 0x3F800000);")
			.WriteLine("var lnm   = Single.FusedMultiplyAdd(-0.056570851f, lM,  0.447178975f);")
			.WriteLine("lnm       = Single.FusedMultiplyAdd(lnm,           lM, -1.469956800f);")
			.WriteLine("lnm       = Single.FusedMultiplyAdd(lnm,           lM,  2.821202636f);")
			.WriteLine("lnm       = Single.FusedMultiplyAdd(lnm,           lM, -1.741793927f);")
			.WriteLine("var lnAx  = lE * 0.6931471805599453f + lnm;")
			.WriteWhitespace()
			.WriteLine("var t = lnAx / n;")
			.WriteWhitespace()
			.WriteLine("var kf  = t * 1.4426950408889634f;")
			.WriteLine("var k   = (int)Single.Round(kf);")
			.WriteLine("var r   = kf - k;")
			.WriteLine("var p   = Single.FusedMultiplyAdd(0.055504108664821580f, r, 0.240226506959100690f);")
			.WriteLine("p       = Single.FusedMultiplyAdd(p,                     r, 0.693147180559945309f);")
			.WriteLine("var ans = Single.FusedMultiplyAdd(p, r, 1.0f) * BitConverter.Int32BitsToSingle((k + 127) << 23);")
			.WriteWhitespace()
			.WriteLine("return (x < 0.0f && (n & 1) != 0) ? -ans : ans;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastRootNMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast n-th root implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses logarithmic reduction and a polynomial exp2 approximation. Supports negative exponents and odd roots of negative values.</remarks>")
			.WriteLine("/// <param name=\"x\">The radicand.</param>")
			.WriteLine("/// <param name=\"n\">The root degree.</param>")
			.WriteLine("/// <returns>The approximate real n-th root of x.</returns>")
			.WriteLine("private static double FastRootN(double x, int n)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (n == 0) return double.NaN;")
			.WriteLine("if (n == 1) return x;")
			.WriteLine("if (x == 0.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("if (n < 0) return 1.0 / FastRootN(x, -n);")
			.WriteWhitespace()
			.WriteLine("var ax = x < 0.0 ? -x : x;")
			.WriteWhitespace()
			.WriteLine("var lBits = BitConverter.DoubleToInt64Bits(ax);")
			.WriteLine("var lE    = (int)((lBits >> 52) - 1023L);")
			.WriteLine("var lM    = BitConverter.Int64BitsToDouble((lBits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteLine($"var lnm   = {multiplyAdd(-0.056570851, "lM", 0.447178975)};")
			.WriteLine($"lnm       = {multiplyAdd("lnm", "lM", -1.469956800)};")
			.WriteLine($"lnm       = {multiplyAdd("lnm", "lM", 2.821202636)};")
			.WriteLine($"lnm       = {multiplyAdd("lnm", "lM", -1.741793927)};")
			.WriteLine("var lnAx  = lE * 0.6931471805599453094172321214581766 + lnm;")
			.WriteWhitespace()
			.WriteLine("var t = lnAx / n;")
			.WriteWhitespace()
			.WriteLine("var kf  = t * 1.4426950408889634073599246810018921;")
			.WriteLine("var k   = (long)Double.Round(kf);")
			.WriteLine("var r   = kf - k;")
			.WriteLine($"var p   = {multiplyAdd(9.618129107628477232e-3, "r", 5.550410866482157995e-2)};")
			.WriteLine($"p       = {multiplyAdd("p", "r", 2.402265069591006909e-1)};")
			.WriteLine($"p       = {multiplyAdd("p", "r", 6.931471805599453094e-1)};")
			.WriteLine($"var ans = {multiplyAdd("p", "r", 1.0)} * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));")
			.WriteWhitespace()
			.WriteLine("return (x < 0.0 && (n & 1) != 0) ? -ans : ans;");

		builder.EndBlock();

		return builder.ToString();
	}
}