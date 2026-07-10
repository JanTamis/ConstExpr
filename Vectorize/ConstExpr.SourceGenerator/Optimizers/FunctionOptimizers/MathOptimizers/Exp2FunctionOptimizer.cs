using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp2FunctionOptimizer() : BaseMathFunctionOptimizer("Exp2", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastExp2MethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastExp2MethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastExp2MethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static float FastExp2(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteLine("if (x >= 128.0f) return float.PositiveInfinity;")
			.WriteLine("if (x < -150.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine($"var k = (int)({copySignInvocation}(0.5f, x));")
			.WriteLine("var r = x - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(0.009618129f, "r", 0.055504109f)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 0.240226507f)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 0.693147181f)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("var bits = (k + 127) << 23;")
			.WriteLine("var scale = BitConverter.Int32BitsToSingle(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastExp2MethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static double FastExp2(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteLine("if (x >= 1024.0) return Double.PositiveInfinity;")
			.WriteLine("if (x < -1100.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine($"var k = (long)({copySignInvocation}(0.5, x));")
			.WriteLine("var r = x - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(9.618129107628477e-3, "r", 5.550410866482158e-2)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 2.402265069591007e-1)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 6.931471805599453e-1)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0)};")
			.WriteWhitespace()
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("var scale = BitConverter.UInt64BitsToDouble(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}
}