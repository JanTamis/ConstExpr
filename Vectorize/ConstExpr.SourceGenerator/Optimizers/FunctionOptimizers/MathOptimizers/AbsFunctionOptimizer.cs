using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AbsFunctionOptimizer() : BaseMathFunctionOptimizer("Abs", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// 1) Unsigned integer: Abs(x) -> x
		if (paramType.IsUnsignedInteger())
		{
			result = arg;
			return true;
		}

		switch (arg)
		{
			// 2) Idempotence: Abs(Abs(x)) -> Abs(x)
			case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Abs" } } innerInv:
			{
				result = innerInv;
				return true;
			}
			// 3) Unary minus: Abs(-x) -> Abs(x)
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } prefix:
			{
				result = CreateInvocation(paramType, Name, prefix.Operand);
				return true;
			}
		}

		var method = GenerateCustomImplementation(context, paramType);

		if (paramType.IsFloatingNumeric())
		{
			var bitType = paramType.SpecialType switch
			{
				SpecialType.System_Single => CreateTypeSyntax(typeof(uint)),
				SpecialType.System_Double => CreateTypeSyntax(typeof(ulong))
			};

			result = CreateInvocation(method, [ paramType.AsTypeSyntax(), bitType ], context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(method, context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		return paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAbsMethodFloating(context),
			SpecialType.System_Double => GenerateFastAbsMethodFloating(context),
			_ when paramType.IsInteger() => GenerateFastAbsMethodInteger(context),
			_ => $"{paramType.Name}.{Name}"
		};
	}

	public static string GenerateFastAbsMethodInteger(FunctionOptimizerContext context)
	{
		return GenerateFastAbsMethodInteger(context.Usings, context.AdditionalSyntax);
	}

	public static string GenerateFastAbsMethodInteger(ISet<string> usings, IDictionary<SyntaxNode, bool> additionalSyntax)
	{
		var method = ParseMethodFromString(BuildFastAbsMethodIntegerSource(usings))!;

		additionalSyntax.TryAdd(method, false);

		return method.Identifier.Text;
	}

	private static string BuildFastAbsMethodIntegerSource(ISet<string> usings)
	{
		usings.Add("System.Runtime.CompilerServices");
		usings.Add("System.Numerics");

		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast absolute-value implementation for integer values.</summary>")
			.WriteLine("/// <remarks>Uses branchless bit manipulation and requires a signed integer type parameter. Note: Does NOT work correctly for T.MinValue due to two's complement overflow</remarks>")
			.WriteLine("/// <typeparam name=\"T\">The integer type.</typeparam>")
			.WriteLine("/// <param name=\"x\">The value to convert to its absolute value.</param>")
			.WriteLine("/// <returns>The absolute value of x.</returns>")
			.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("private static T FastAbs<T>(T x) where T : IBinaryInteger<T>")
			.StartBlock()
			.WriteLine("var bits = Unsafe.SizeOf<T>() * 8 - 1;")
			.WriteLine("var mask = x >> bits;")
			.WriteWhitespace()
			.WriteLine("return (x + mask) ^ mask;")
			.EndBlock();

		return builder.ToString();
	}

	public static string GenerateFastAbsMethodFloating(FunctionOptimizerContext context)
	{
		return GenerateFastAbsMethodFloating(context.Usings, context.AdditionalSyntax);
	}

	public static string GenerateFastAbsMethodFloating(ISet<string> usings, IDictionary<SyntaxNode, bool> additionalSyntax)
	{
		var method = ParseMethodFromString(BuildFastAbsMethodFloatingSource(usings))!;

		additionalSyntax.TryAdd(method, false);

		return method.Identifier.Text;
	}

	private static string BuildFastAbsMethodFloatingSource(ISet<string> usings)
	{
		usings.Add("System.Runtime.CompilerServices");
		usings.Add("System.Numerics");

		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast absolute-value implementation for IEEE 754 floating-point values.</summary>")
			.WriteLine("/// <remarks>Clears the sign bit via a bitcast to the underlying integer representation. Correct for all values including NaN and infinity.</remarks>")
			.WriteLine("/// <typeparam name=\"T\">The floating-point type.</typeparam>")
			.WriteLine("/// <typeparam name=\"TBits\">The integer type with the same bit width as T.</typeparam>")
			.WriteLine("/// <param name=\"x\">The value to convert to its absolute value.</param>")
			.WriteLine("/// <returns>The absolute value of x.</returns>")
			.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("private static T FastAbs<T, TBits>(T x) where TBits : IBitwiseOperators<TBits, TBits, TBits>, IMinMaxValue<TBits>")
			.StartBlock()
			.WriteLine("var bits = Unsafe.BitCast<T, TBits>(x);")
			.WriteWhitespace()
			.WriteLine("return Unsafe.BitCast<TBits, T>(bits & TBits.MaxValue);")
			.EndBlock();

		return builder.ToString();
	}
}