using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LerpFunctionOptimizer() : BaseMathFunctionOptimizer("Lerp", n => n is 3), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		if (paramType.IsFloatingNumeric())
		{
			var method = ParseMethodFromString(GenerateFastLerpMethod(context, paramType));
			context.AdditionalSyntax.TryAdd(method, false);

			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastLerpMethod(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast linear interpolation (Lerp) for floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a fused multiply-add formulation for numerical stability and performance. Returns a + t(b - a).</remarks>")
			.WriteLine("/// <param name=\"a\">Start value.</param>")
			.WriteLine("/// <param name=\"b\">End value.</param>")
			.WriteLine("/// <param name=\"t\">Interpolation factor.</param>")
			.WriteLine("/// <returns>The interpolated value.</returns>")
			.WriteLine("private static T FastLerp<T>(T a, T b, T t) where T : IFloatingPointIeee754<T>")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder
				.WriteLine("if (T.IsNaN(a) || T.IsNaN(b) || T.IsNaN(t)) return T.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("return T.MultiplyAddEstimate(t, b - a, a);")
			.EndBlock();

		return builder.ToString();
	}
}