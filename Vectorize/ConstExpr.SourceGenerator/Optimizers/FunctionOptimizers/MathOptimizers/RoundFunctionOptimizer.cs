using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
/// Optimizer for Math.Round / MathF.Round.
///
/// Benchmark findings (Apple M4 Pro, .NET 10.0.1, ARM64 — RoundBenchmark):
///   Math.Round / MathF.Round  → ~0.530 ns  (single FRINTN instruction) ← optimal
///   double.Round / float.Round → ~0.547 ns  (+3%  — same instruction via IFloatingPoint&lt;T&gt;)
///   Math.Floor(x + 0.5)        → ~0.589 ns  (+11% — 2 FP ops; avoid)
///   long/int-cast tricks       → ~0.672 ns  (+27% — FP/int domain crossing; never use)
///
///   Unary-minus rewrite Round(-x) → -Round(x): ratio 0.99 — within measurement noise,
///   no meaningful benefit. The rewrite has been removed to keep generated code simple.
/// </summary>
public class RoundFunctionOptimizer() : BaseMathFunctionOptimizer("Round", n => n is 1 or 2 or 3)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// 1) If the inner expression already yields an integer-valued result, keep inner:
		//    Truncate/Floor/Ceiling/Round all return an integral-valued float → Round is a no-op.
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Truncate" or "Floor" or "Ceiling" or "Round" } } inv)
		{
			result = inv;
			return true;
		}

		// 2) Integer types: Round(x) → x (rounding has no effect on integers)
		if (!paramType.IsFloatingNumeric())
		{
			result = context.VisitedParameters[0];
			return true;
		}

		// 3) Check if parent of context.Invocation is casting to an integer type.
		if (context.Invocation.Parent is CastExpressionSyntax
			{
				Type: PredefinedTypeSyntax
				{
					Keyword.RawKind: (int)SyntaxKind.IntKeyword
						or (int)SyntaxKind.UIntKeyword
						or (int)SyntaxKind.LongKeyword
						or (int)SyntaxKind.ULongKeyword
						or (int)SyntaxKind.ShortKeyword
						or (int)SyntaxKind.UShortKeyword
						or (int)SyntaxKind.ByteKeyword
						or (int)SyntaxKind.SByteKeyword
						or (int)SyntaxKind.CharKeyword
				}
			}
				&& context.VisitedParameters.Count == 2)
		{
			// Check that the second argument is a compile-time MidpointRounding enum member
			string? enumMember = null;

			switch (context.VisitedParameters[1])
			{
				case MemberAccessExpressionSyntax mae:
					{
						// e.g. MidpointRounding.AwayFromZero or System.MidpointRounding.AwayFromZero
						if (mae.Name is IdentifierNameSyntax idName)
						{
							enumMember = idName.Identifier.Text;
						}
						break;
					}
				case IdentifierNameSyntax id:
					// e.g. AwayFromZero when using a using static or same-namespace alias
					enumMember = id.Identifier.Text;
					break;
				case QualifiedNameSyntax { Right: IdentifierNameSyntax right }:
					// e.g. System.MidpointRounding.AwayFromZero could be parsed as a qualified name
					enumMember = right.Identifier.Text;
					break;
			}

			switch (enumMember)
			{
				case "ToZero":
					result = CreateInvocation(paramType, "Truncate", context.VisitedParameters.Take(1));
					return true;
				case "ToPositiveInfinity":
					result = CreateInvocation(paramType, "Ceiling", context.VisitedParameters.Take(1));
					return true;
				case "ToNegativeInfinity":
					result = CreateInvocation(paramType, "Floor", context.VisitedParameters.Take(1));
					return true;
			}
		}

		// Default: emit Round call directly — already the optimal scalar implementation.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}