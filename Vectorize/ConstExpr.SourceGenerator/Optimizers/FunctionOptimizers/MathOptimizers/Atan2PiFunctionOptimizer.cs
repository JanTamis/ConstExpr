using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Atan2PiFunctionOptimizer() : BaseMathFunctionOptimizer("Atan2Pi", n => n is 2), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var y = context.VisitedParameters[0];
		var x = context.VisitedParameters[1];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(y, out var yValue) && TryGetNumericLiteral(x, out var xValue))
		{
			// Atan2Pi(0, x) where x > 0 => 0
			if (IsApproximately(yValue, 0.0) && xValue > 0.0)
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(0, x) where x < 0 => 1 (π/π = 1)
			if (IsApproximately(yValue, 0.0) && xValue < 0.0)
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, 0) where y > 0 => 0.5 (π/2 / π = 0.5)
			if (IsApproximately(xValue, 0.0) && yValue > 0.0)
			{
				result = CreateLiteral(0.5.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, 0) where y < 0 => -0.5
			if (IsApproximately(xValue, 0.0) && yValue < 0.0)
			{
				result = CreateLiteral((-0.5).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, x) where y == x and x > 0 => 0.25 (π/4 / π = 0.25)
			if (IsApproximately(yValue, xValue) && xValue > 0.0)
			{
				result = CreateLiteral(0.25.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtan2PiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastAtan2PiMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
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

	private static string GenerateFastAtan2PiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var maxInvocation = GetMethodInvocation<MaxFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static float FastAtan2Pi(float y, float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(y) || Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine($"var absX = {absInvocation}(x);")
			.WriteLine($"var absY = {absInvocation}(y);")
			.WriteLine($"var maxV = {maxInvocation}(absX, absY);")
			.WriteWhitespace()
			.WriteLine("if (maxV == 0f) return 0f;")
			.WriteWhitespace()
			.WriteLine($"var a = {minInvocation}(absX, absY) / maxV;")
			.WriteLine("var u = a * a;")
			.WriteWhitespace()
			.WriteLine($"var p = {multiplyAdd("u", 0.00663222f, -0.02710107f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 0.05733014f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -0.10510700f)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 0.31826720f)};")
			.WriteLine("p *= a;")
			.WriteWhitespace()
			.WriteLine("p = absY > absX ? 0.5f - p : p;")
			.WriteLine("p = x < 0f ? 1f - p : p;")
			.WriteLine("p = y < 0f ? -p : p;")
			.WriteWhitespace()
			.WriteLine("return p;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtan2PiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var maxInvocation = GetMethodInvocation<MaxFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);
		var sqrtInvocation = GetMethodInvocation<SqrtFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static double FastAtan2Pi(double y, double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(y) || Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine($"var absX = {absInvocation}(x);")
			.WriteLine($"var absY = {absInvocation}(y);")
			.WriteLine($"var maxV = {maxInvocation}(absX, absY);")
			.WriteLine("if (maxV == 0.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine($"var a = {minInvocation}(absX, absY) / maxV;")
			.WriteWhitespace()
			.WriteLine($"var t = a / (1.0 + {sqrtInvocation}(1.0 + a * a));")
			.WriteLine("var u = t * t;")
			.WriteWhitespace()
			.WriteLine($"var p = {multiplyAdd("u", -1.0 / 15.0, 1.0 / 13.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 11.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0 / 9.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 7.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0 / 5.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", -1.0 / 3.0)};")
			.WriteLine($"p = {multiplyAdd("u", "p", 1.0)};")
			.WriteLine("p = 2.0 / Double.Pi * t * p;")
			.WriteWhitespace()
			.WriteLine("p = absY > absX ? 0.5 - p : p;")
			.WriteLine("p = x < 0.0    ? 1.0 - p  : p;")
			.WriteLine("p = y < 0.0    ? -p : p;")
			.WriteLine("return p;");

		builder.EndBlock();

		return builder.ToString();
	}
}