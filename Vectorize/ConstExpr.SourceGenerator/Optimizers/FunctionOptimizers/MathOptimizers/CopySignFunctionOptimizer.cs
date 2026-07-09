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

public class CopySignFunctionOptimizer() : BaseMathFunctionOptimizer("CopySign", n => n is 2), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var y = context.VisitedParameters[1];

		// 1) Unsigned integer: sign has no effect
		if (paramType.IsUnsignedInteger())
		{
			result = x;
			return true;
		}

		// 2) y is a known numeric literal: fold the sign statically
		if (TryGetNumericLiteral(y, out var signVal)
		    && HasMethod(paramType, "Abs", 1))
		{
			if (signVal >= 0.0)
			{
				// CopySign(x, 0) → +|x|  and  CopySign(x, pos) → |x|
				result = CreateInvocation(paramType, "Abs", x);
				return true;
			}

			// CopySign(x, neg) → -|x|
			var absCall = CreateInvocation(paramType, "Abs", x);
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, absCall);
			return true;
		}

		if (TryGenerateCustomImplementation(context, paramType, out var method))
		{
			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: forward to target numeric helper type
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	public bool TryGenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCopySignMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastCopySignMethodDouble(context.FastMathFlags),
			_ => GenerateFastCopySignMethodInteger(context, context.FastMathFlags)
		});

		if (result is not null)
		{
			context.AdditionalSyntax.TryAdd(result, false);
			return true;
		}

		return false;
	}

	private static string GenerateFastCopySignMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast CopySign implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation and preserves the sign bit of the second operand.</remarks>")
			.WriteLine("/// <param name=\"x\">The magnitude value.</param>")
			.WriteLine("/// <param name=\"y\">The sign source value.</param>")
			.WriteLine("/// <returns>A float with the magnitude of x and the sign of y.</returns>")
			.WriteLine("private static float FastCopySignFloat(float x, float y)")
			.StartBlock()
			.WriteLine("var xBits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var yBits = BitConverter.SingleToInt32Bits(y);")
			.WriteLine("return BitConverter.Int32BitsToSingle((xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000)));")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast CopySign implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation and preserves the sign bit of the second operand.</remarks>")
			.WriteLine("/// <param name=\"x\">The magnitude value.</param>")
			.WriteLine("/// <param name=\"y\">The sign source value.</param>")
			.WriteLine("/// <returns>A double with the magnitude of x and the sign of y.</returns>")
			.WriteLine("private static double FastCopySignDouble(double x, double y)")
			.StartBlock()
			.WriteLine("var xBits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var yBits = BitConverter.DoubleToInt64Bits(y);")
			.WriteLine("return BitConverter.Int64BitsToDouble((xBits & long.MaxValue) | (yBits & long.MinValue));")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodInteger(FunctionOptimizerContext context, FastMathFlags flags)
	{
		var invocation = AbsFunctionOptimizer.GenerateFastAbsMethodInteger(context);

		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast CopySign implementation for integers.</summary>")
			.WriteLine("/// <remarks>Returns x with the sign of y using branchless integer operations.</remarks>")
			.WriteLine("/// <param name=\"x\">The magnitude value.</param>")
			.WriteLine("/// <param name=\"y\">The sign source value.</param>")
			.WriteLine("/// <returns>An integer with the magnitude of x and the sign of y.</returns>")
			.WriteLine("private static T FastCopySign<T>(T x, T y) where T : IBinaryInteger<T>")
			.StartBlock()
			.WriteLine($"var absValue = {invocation}(x);")
			.WriteWhitespace()
			.WriteLine("return T.IsPositive(y) ? absValue : -absValue;")
			.EndBlock();

		return builder.ToString();
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
}