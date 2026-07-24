using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
		if (paramType.IsInteger())
		{
			var method = ParseMethodFromString(GenerateFastCopySignMethodInteger())!;
			context.AdditionalSyntax.TryAdd(method, false);

			return method.Identifier.Text;
		}
		else
		{
			var method = ParseMethodFromString(GenerateFastCopySignMethodFloating());

			context.AdditionalSyntax.TryAdd(method, false);
			context.Usings.Add("System.Runtime.Intrinsics");

			return paramType.SpecialType switch
			{
				SpecialType.System_Single => $"{method.Identifier.Text}<float, int>",
				SpecialType.System_Double => $"{method.Identifier.Text}<double, long>",
				_ => method.Identifier.Text
			};
		}
	}

	private static string GenerateFastCopySignMethodFloating()
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static T FastCopySign<T, TBits>(T x, T y) where T : IFloatingPointIeee754<T> where TBits : IBinaryInteger<TBits>, IMinMaxValue<TBits>")
			.StartBlock()
			.WriteLine("if (Vector.IsHardwareAccelerated)")
			.StartBlock()
			.WriteLine("return Vector128.ConditionalSelect(Vector128.CreateScalarUnsafe(T.NegativeZero), Vector128.CreateScalarUnsafe(y), Vector128.CreateScalarUnsafe(x)).ToScalar();")
			.EndBlock()
			.WriteLine("var xBits = Unsafe.BitCast<T, TBits>(x);")
			.WriteLine("var yBits = Unsafe.BitCast<T, TBits>(y);")
			.WriteLine("return Unsafe.BitCast<TBits, T>((xBits & TBits.MaxValue) | (yBits & TBits.MinValue));")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCopySignMethodInteger()
	{
		var builder = new CodeWriter();

		builder.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("private static T FastCopySign<T>(T x, T y) where T : IBinaryInteger<T>")
			.StartBlock()
			.WriteLine("var bits = Unsafe.SizeOf<T>() * 8 - 1;")
			.WriteLine("var mask = (x >> bits) ^ (y >> bits);")
			.WriteWhitespace()
			.WriteLine("return (x ^ mask) - mask;")
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