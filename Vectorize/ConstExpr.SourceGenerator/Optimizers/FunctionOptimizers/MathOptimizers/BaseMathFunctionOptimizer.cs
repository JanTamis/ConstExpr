using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public abstract class BaseMathFunctionOptimizer(string name, Func<int, bool> isValidParameterCount) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public Func<int, bool> IsValidParameterCount { get; } = isValidParameterCount;
	protected abstract bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result);

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			result = null;
			return false;
		}

		return TryOptimizeMath(context, paramType, out result);
	}

	protected static bool HasMethod(ITypeSymbol type, string name, int parameterCount)
	{
		return type.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == parameterCount
			          && m.DeclaredAccessibility == Accessibility.Public
			          && SymbolEqualityComparer.Default.Equals(type, m.ContainingType));
	}

	protected bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= Double.Epsilon;
	}

	protected static Func<object, object, object, string> MultiplyAddEstimate(FunctionOptimizerContext context, ITypeSymbol type)
	{
		if (context.FastMathFlags.HasFlag(FastMathFlags.FusedMultiplyAdd))
		{
			var typeName = type.ToDisplayString();

			return HasMethod(type, "MultiplyAddEstimate", 3)
				? (a, b, c) => $"{typeName}.MultiplyAddEstimate({Format(a)}, {Format(b)}, {Format(c)})"
				: (a, b, c) => $"{typeName}.FusedMultiplyAdd({Format(a)}, {Format(b)}, {Format(c)})";
		}

		// Parenthesized so a multiplyAdd(...) result nested as an operand of another multiplyAdd(...) call
		// still multiplies/adds with the intended grouping.
		return (a, b, c) => $"({Format(a)} * {Format(b)} + {Format(c)})";

		// Operands are either an identifier/sub-expression (string, emitted as-is) or a literal value
		// (formatted via CreateLiteral); CreateLiteral would otherwise quote a string as a C# string literal.
		static string Format(object value) => value as string ?? CreateLiteral(value).ToString();
	}

	protected bool TryGetCustomMethodInvocation<MathOptimizer>(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out string? methodName) where MathOptimizer : IBaseMathCustomImplementation, new()
	{
		var optimizer = new MathOptimizer();

		if (optimizer.TryGenerateCustomImplementation(context, paramType, out var method))
		{
			methodName = method.Identifier.Text;
			return true;
		}

		methodName = null;
		return false;
	}

	private bool IsValidMathMethod(IMethodSymbol method, [NotNullWhen(true)] out ITypeSymbol? type)
	{
		type = method.Parameters
			.Select(s => s.Type)
			.FirstOrDefault();

		return method.Name == Name
		       && IsValidParameterCount(method.Parameters.Length);
	}
}