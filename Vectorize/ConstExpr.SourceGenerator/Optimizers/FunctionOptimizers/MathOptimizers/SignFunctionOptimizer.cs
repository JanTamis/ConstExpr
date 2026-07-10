using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SignFunctionOptimizer() : BaseMathFunctionOptimizer("Sign", n => n is 1), IBaseMathCustomImplementation
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
			context.Usings.Add("System.Numerics");
			context.Usings.Add("System.Runtime.CompilerServices");
			context.AdditionalSyntax.TryAdd(ParseMethodFromString(GenerateFastSignMethod()), false);
		}

		return paramType.SpecialType switch
		{
			SpecialType.System_Single => "FastSign<float, int>",
			SpecialType.System_Double => "FastSign<double, long>",
			_ => base.GenerateCustomImplementation(context, paramType)
		};
	}

	private static string GenerateFastSignMethod()
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static int FastSign<T, TBits>(T x) where T : IBinaryFloatingPointIeee754<T> where TBits : IBinaryInteger<TBits>")
			.StartBlock()
			.WriteLine("if (T.IsZero(x))")
			.StartBlock()
			.WriteLine("return 0;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var bits = Unsafe.BitCast<T, TBits>(x);")
			.WriteLine("return 1 | Int32.CreateChecked((bits >> (Unsafe.SizeOf<TBits>() * 8 - 1)));")
			.EndBlock();

		return builder.ToString();
	}
}