using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Atan2FunctionOptimizer() : BaseMathFunctionOptimizer("Atan2", n => n is 2)
{
	/// <summary>
	///   Attempts to optimize a Math.Atan2 function call by applying literal simplifications and generating a fast
	///   approximation implementation.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <param name="paramType">The type symbol of the parameter (float or double).</param>
	/// <param name="result">The optimized syntax node if successful; otherwise null.</param>
	/// <returns>True if optimization was successful; otherwise false.</returns>
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var y = context.VisitedParameters[0];
		var x = context.VisitedParameters[1];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(y, out var yValue) && TryGetNumericLiteral(x, out var xValue))
		{
			// Atan2(0, x) where x > 0 => 0
			if (IsApproximately(yValue, 0.0) && xValue > 0.0)
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(0, x) where x < 0 => π
			if (IsApproximately(yValue, 0.0) && xValue < 0.0)
			{
				result = CreateLiteral(Math.PI.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y > 0 => π/2
			if (IsApproximately(xValue, 0.0) && yValue > 0.0)
			{
				var piOver2 = Math.PI / 2.0;
				result = CreateLiteral(piOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y < 0 => -π/2
			if (IsApproximately(xValue, 0.0) && yValue < 0.0)
			{
				var negPiOver2 = -Math.PI / 2.0;
				result = CreateLiteral(negPiOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, x) where y == x => π/4 or -3π/4
			if (IsApproximately(yValue, xValue) && xValue > 0.0)
			{
				var piOver4 = Math.PI / 4.0;
				result = CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtan2MethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastAtan2MethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
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
			{
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
			{
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	private static string GenerateFastAtan2MethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of arctangent with two arguments (Atan2) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses octant reduction, a minimax polynomial approximation, and branch-friendly quadrant corrections.</remarks>")
			.WriteLine("/// <param name=\"y\">The y-coordinate.</param>")
			.WriteLine("/// <param name=\"x\">The x-coordinate.</param>")
			.WriteLine("/// <returns>Approximate angle in radians in the range [-π, π].</returns>")
			.WriteLine("private static float FastAtan2(float y, float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(y) || Single.IsNaN(x)) return Single.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("var absX = Single.Abs(x);")
			.WriteLine("var absY = Single.Abs(y);")
			.WriteLine("var maxV = Single.Max(absX, absY);")
			.WriteWhitespace()
			.WriteLine("if (maxV == 0f) return 0f;")
			.WriteWhitespace()
			.WriteLine("var a = Single.Min(absX, absY) / maxV;")
			.WriteLine("var u = a * a;")
			.WriteWhitespace()
			.WriteLine($"var p = {multiplyAdd("u", 0.0208351f, -0.0851330f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 0.1801410f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -0.3302995f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 0.9998660f)};")
			.WriteLine("p *= a;")
			.WriteWhitespace()
			.WriteLine("p = absY > absX ? Single.Pi / 2 - p : p;")
			.WriteLine("p = x < 0f     ? Single.Pi      - p : p;")
			.WriteLine("p = y < 0f     ? -p : p;")
			.WriteWhitespace()
			.WriteLine("return p;")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtan2MethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of arctangent with two arguments (Atan2) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a rational approximation with octant reduction and quadrant corrections.</remarks>")
			.WriteLine("/// <param name=\"y\">The y-coordinate.</param>")
			.WriteLine("/// <param name=\"x\">The x-coordinate.</param>")
			.WriteLine("/// <returns>Approximate angle in radians in the range [-π, π].</returns>")
			.WriteLine("private static double FastAtan2(double y, double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(y) || Double.IsNaN(x)) return Double.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("var absX = Double.Abs(x);")
			.WriteLine("var absY = Double.Abs(y);")
			.WriteLine("var maxV = Double.Max(absX, absY);")
			.WriteWhitespace()
			.WriteLine("if (maxV == 0.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("var a = Double.Min(absX, absY) / maxV;")
			.WriteWhitespace()
			.WriteLine("var t = a / (1.0 + Double.Sqrt(1.0 + a * a));")
			.WriteLine("var u = t * t;")
			.WriteWhitespace()
			.WriteLine($"var p = {multiplyAdd("u", -1.0 / 15.0, 1.0 / 13.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 11.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0 / 9.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 7.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0 / 5.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 3.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0)};")
			.WriteLine("p = 2.0 * t * p; // atan(a) = 2·atan(t)")
			.WriteWhitespace()
			.WriteLine("p = absY > absX ? Double.Pi / 2 - p : p;")
			.WriteLine("p = x < 0.0    ? Double.Pi      - p : p;")
			.WriteLine("p = y < 0.0    ? -p : p;")
			.WriteWhitespace()
			.WriteLine("return p;")
			.EndBlock();

		return builder.ToString();
	}
}