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

public class CopySignFunctionOptimizer() : BaseMathFunctionOptimizer("CopySign", n => n is 2)
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

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCopySignMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastCopySignMethodDouble(context.FastMathFlags),
			_ => GenerateFastCopySignMethodInteger(context, context.FastMathFlags)
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: forward to target numeric helper type
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastCopySignMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.AddIndent("/// ")
			.WriteLine("<summary>")
			.WriteLine("Bit-manipulation CopySign for float — ~10% faster than MathF.CopySign on ARM64.")
			.WriteLine("Masks the magnitude bits of x and the sign bit of y via BitConverter round-trip.")
			.WriteLine("</summary>")
			.RemoveIndent()
			.WriteLine("private static float CopySignFastFloat(float x, float y)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("var xBits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var yBits = BitConverter.SingleToInt32Bits(y);")
			.WriteLine("return BitConverter.Int32BitsToSingle((xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000)));")
			.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.AddIndent("/// ")
			.WriteLine("<summary>")
			.WriteLine("Bit-manipulation CopySign for double — ~10% faster than Math.CopySign on ARM64.")
			.WriteLine("Masks the magnitude bits of x and the sign bit of y via BitConverter round-trip.")
			.WriteLine("</summary>")
			.RemoveIndent()
			.WriteLine("private static double CopySignFastDouble(double x, double y)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("var xBits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var yBits = BitConverter.DoubleToInt64Bits(y);")
			.WriteLine("return BitConverter.Int64BitsToDouble((xBits & long.MaxValue) | (yBits & long.MinValue));")
			.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodInteger(FunctionOptimizerContext context, FastMathFlags flags)
	{
		var invocation = AbsFunctionOptimizer.GenerateFastAbsMethodInteger(context);

		var builder = new CodeWriter();

		builder.AddIndent("/// ")
			.WriteLine("<summary>")
			.WriteLine("Branchless CopySign for integers.")
			.WriteLine("Note: Does NOT work correctly for <c>T.MinValue</c> due to two's complement overflow in AbsFast.")
			.WriteLine("</summary>")
			.RemoveIndent()
			.WriteLine("private static T CopySignFast<T>(T x, T y) where T : IBinaryInteger<T>")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine($"var absValue = {invocation}(x);")
			.WriteLine("")
			.WriteLine("return T.IsPositive(y) ? absValue : -absValue;")
			.RemoveIndent()
			.WriteLine("}");

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