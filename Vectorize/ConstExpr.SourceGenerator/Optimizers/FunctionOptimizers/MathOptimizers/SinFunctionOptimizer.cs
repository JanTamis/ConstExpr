using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
///   Optimizer for Math.Sin / MathF.Sin.
///   Range reduction uses a Cody-Waite two-term Tau split (TauHi/TauLo) instead of a single-constant
///   subtraction, which loses precision catastrophically for large |x| via cancellation. Verified via
///   arbitrary-precision (mpmath) reference: the two-term reduction is exact (to double ULP) up to
///   |x| ~ 2e7-5e7. The 5e6 fallback threshold mirrors VectorMath.CosDouble/SinDouble's own ARG_HUGE
///   constant (CoreLib System.Runtime.Intrinsics.VectorMath) with comfortable margin; beyond it, the
///   real BCL Sin is used instead of hand-porting BCL's full Payne-Hanek reduction.
/// </summary>
public class SinFunctionOptimizer() : BaseMathFunctionOptimizer("Sin", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastSinMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static float FastSin(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (Single.Abs(x) >= 5e6f) return Single.Sin(x);")
			.WriteWhitespace()
			.WriteLine("var xd = (double)x;")
			.WriteLine("var k = Math.Round(xd * (1.0 / Double.Tau));")
			.WriteLine("xd -= k * 6.2831853069365025;")
			.WriteLine("xd -= k * 2.4308402026024769e-10;")
			.WriteLine("x = (float)xd;")
			.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteLine($"x = {minInvocation}(x, Single.Pi - x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(-1.9841269841e-4f, "x2", 8.3333333333e-3f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -1.6666666667e-1f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0f)};")
			.WriteLine("ret *= x;")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static double FastSin(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (Double.Abs(x) >= 5e6) return Double.Sin(x);")
			.WriteWhitespace()
			.WriteLine($"var k = {roundInvocation}(x * (1.0 / Double.Tau));")
			.WriteLine("x -= k * 6.2831853069365025;")
			.WriteLine("x -= k * 2.4308402026024769e-10;")
			.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("if (x > Double.Pi / 2.0)")
			.StartBlock()
			.WriteLine("x = Double.Pi - x;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(2.6019406621361745e-9, "x2", -1.9839531932589676e-7)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 8.3333333333216515e-6)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.00019841269836761127)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.0083333333333332177)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.16666666666666666)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0)};")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret * x, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}
}