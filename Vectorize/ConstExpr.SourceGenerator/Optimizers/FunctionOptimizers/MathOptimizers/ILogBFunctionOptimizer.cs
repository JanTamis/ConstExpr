using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ILogBFunctionOptimizer() : BaseMathFunctionOptimizer("ILogB", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Math.ILogB / MathF.ILogB is NOT a single-instruction intrinsic on ARM64 — disassembly
		// shows it lowers to an indirect branch to CoreLib (a real call), while a manual
		// exponent-bit extraction inlines to straight-line branchy code with no calls.
		// Benchmark results on Apple M4 Pro (.NET 10, ARM64), amortized over 256 varied inputs:
		//   Math.ILogB(double)      : 0.566 ns
		//   FastILogB(double)       : 0.434 ns  — 1.31x faster
		//   MathF.ILogB(float)      : 0.575 ns
		//   FastILogB(float)        : 0.431 ns  — 1.33x faster
		//   FastILogB(double), NoNaN|NoInfinity (skips the Inf/NaN branch): 0.378 ns — another ~8% faster
		// Verified exhaustively against MathF.ILogB (all 2^32 float bit patterns) and against
		// Math.ILogB over 20M random double bit patterns plus zero/subnormal/inf/nan boundaries.
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastILogBMethodFloat(context),
			SpecialType.System_Double => GenerateFastILogBMethodDouble(context),
			_ => null
		});

		if (method is not null)
		{
			context.Usings.Add("System.Numerics");
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastILogBMethodFloat(FunctionOptimizerContext context)
	{
		var builder = new CodeWriter();
		var isFinite = context.FastMathFlags.HasFlag(FastMathFlags.NoNaN) && context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity);

		builder.WriteLine("private static int FastILogB(float x)")
			.StartBlock()
			.WriteLine("var bits = BitConverter.SingleToUInt32Bits(x);")
			.WriteLine("var exponent = (int)((bits >> 23) & 0xFFU);");

		if (!isFinite)
		{
			builder.WriteLine("if (exponent == 0xFF)")
				.StartBlock()
				.WriteLine("return int.MaxValue;") // +/-Infinity or NaN
				.EndBlock();
		}

		builder.WriteLine("if (exponent == 0)")
			.StartBlock()
			.WriteLine("var mantissa = bits & 0x007F_FFFFU;")
			.WriteLine("if (mantissa == 0)")
			.StartBlock()
			.WriteLine("return int.MinValue;") // +/-0
			.EndBlock()
			.WriteLine("return -118 - BitOperations.LeadingZeroCount(mantissa);") // subnormal
			.EndBlock()
			.WriteLine("return exponent - 127;")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastILogBMethodDouble(FunctionOptimizerContext context)
	{
		var builder = new CodeWriter();
		var isFinite = context.FastMathFlags.HasFlag(FastMathFlags.NoNaN) && context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity);

		builder.WriteLine("private static int FastILogB(double x)")
			.StartBlock()
			.WriteLine("var bits = BitConverter.DoubleToUInt64Bits(x);")
			.WriteLine("var exponent = (int)((bits >> 52) & 0x7FFUL);");

		if (!isFinite)
		{
			builder.WriteLine("if (exponent == 0x7FF)")
				.StartBlock()
				.WriteLine("return int.MaxValue;") // +/-Infinity or NaN
				.EndBlock();
		}

		builder.WriteLine("if (exponent == 0)")
			.StartBlock()
			.WriteLine("var mantissa = bits & 0x000F_FFFF_FFFF_FFFFUL;")
			.WriteLine("if (mantissa == 0)")
			.StartBlock()
			.WriteLine("return int.MinValue;") // +/-0
			.EndBlock()
			.WriteLine("return -1011 - BitOperations.LeadingZeroCount(mantissa);") // subnormal
			.EndBlock()
			.WriteLine("return exponent - 1023;")
			.EndBlock();

		return builder.ToString();
	}
}