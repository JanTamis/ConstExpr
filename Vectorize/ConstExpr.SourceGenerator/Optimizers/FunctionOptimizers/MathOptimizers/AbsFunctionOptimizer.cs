using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AbsFunctionOptimizer() : BaseMathFunctionOptimizer("Abs", n => n is 1)
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

		if (paramType.IsInteger())
		{
			var method = GenerateFastAbsMethodInteger(context);

			result = CreateInvocation(method, context.VisitedParameters);
			return true;
		}

		// Default: keep as Abs call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	public static string GenerateFastAbsMethodInteger(FunctionOptimizerContext context)
	{
		context.Usings.Add("System.Runtime.CompilerServices");
		context.Usings.Add("System.Numerics");

		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast absolute-value implementation for integer values.</summary>")
			.WriteLine("/// <remarks>Uses branchless bit manipulation and requires a signed integer type parameter. Note: Does NOT work correctly for T.MinValue due to two's complement overflow</remarks>")
			.WriteLine("/// <typeparam name=\"T\">The integer type.</typeparam>")
			.WriteLine("/// <param name=\"x\">The value to convert to its absolute value.</param>")
			.WriteLine("/// <returns>The absolute value of x.</returns>")
			.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("private static T AbsFast<T>(T x) where T : IBinaryInteger<T>")
			.StartBlock()
			.WriteLine("var bits = Unsafe.SizeOf<T>() * 8 - 1;")
			.WriteLine("var mask = x >> bits;")
			.WriteWhitespace()
			.WriteLine("return (x + mask) ^ mask;")
			.EndBlock();

		var method = ParseMethodFromString(builder.ToString())!;

		context.AdditionalSyntax.TryAdd(method, false);

		return method.Identifier.Text;
	}
}