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
			if (BitConverter.DoubleToInt64Bits(signVal) >= 0)
			{
				// CopySign(x, +0) → +|x|  and  CopySign(x, pos) → |x|
				result = CreateInvocation(paramType, "Abs", x);
				return true;
			}

			// CopySign(x, neg) → -|x|
			var absCall = CreateInvocation(paramType, "Abs", x);
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, absCall);
			return true;
		}

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCopySignMethodFloat(),
			SpecialType.System_Double => GenerateFastCopySignMethodDouble(),
			_ => GenerateFastCopySignMethodInteger(context, context.FastMathFlags)
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastCopySignMethodFloat()
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastCopySign(float x, float y)")
			.StartBlock()
			.WriteLine("var xBits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var yBits = BitConverter.SingleToInt32Bits(y);")
			.WriteLine("return BitConverter.Int32BitsToSingle((xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000)));")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodDouble()
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastCopySign(double x, double y)")
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

		builder.WriteLine("private static T FastCopySign<T>(T x, T y) where T : IBinaryInteger<T>")
			.StartBlock()
			.WriteLine($"var absValue = {invocation}(x);")
			.WriteLine("var bits = Unsafe.SizeOf<T>() * 8 - 1;")
			.WriteLine("var signMask = y >> bits;")
			.WriteWhitespace()
			.WriteLine("return (absValue ^ signMask) - signMask;")
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