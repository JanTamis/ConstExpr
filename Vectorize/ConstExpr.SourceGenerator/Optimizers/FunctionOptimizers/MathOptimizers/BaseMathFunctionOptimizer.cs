using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public abstract class BaseMathFunctionOptimizer(string name, Func<int, bool> isValidParameterCount) : BaseFunctionOptimizer, IBaseMathCustomImplementation
{
	public string Name { get; } = name;
	public Func<int, bool> IsValidParameterCount { get; } = isValidParameterCount;

	/// <summary>
	///   Which fast-math flags let this optimizer run, following the
	///   <see cref="Optimizers.BinaryOptimizers.Strategies.IBinaryStrategy.RequiredFlags" /> convention:
	///   <see cref="FastMathFlags.Strict" /> in the list means "always", otherwise the optimizer runs
	///   only when the attribute has one of the listed flags set.
	///   <para>
	///     The default is <see cref="FastMathFlags.NoNaN" />: most math rewrites here are
	///     approximations, or assume NaN-free operands, or diverge on a signed zero / overflow edge —
	///     all of which a fast-math user has opted into. An optimizer whose output matches the BCL
	///     method bit-for-bit for every input overrides this with <c>[FastMathFlags.Strict]</c>.
	///   </para>
	/// </summary>
	public virtual FastMathFlags[] RequiredFlags => [ FastMathFlags.NoNaN ];

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

	protected static string GetMethodInvocation<MathOptimizer>(FunctionOptimizerContext context, ITypeSymbol paramType) where MathOptimizer : IBaseMathCustomImplementation, new()
	{
		var optimizer = new MathOptimizer();

		return optimizer.GenerateCustomImplementation(context, paramType);
	}

	private bool IsValidMathMethod(IMethodSymbol method, [NotNullWhen(true)] out ITypeSymbol? type)
	{
		type = method.Parameters
			.Select(s => s.Type)
			.FirstOrDefault();

		return method.Name == Name
		       && IsValidParameterCount(method.Parameters.Length);
	}

	public virtual string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		return $"{paramType.Name}.{Name}";
	}
}