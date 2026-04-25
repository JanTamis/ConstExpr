using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ExpFunctionOptimizer() : BaseMathFunctionOptimizer("Exp", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Exp(Log(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Log" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			// Use order-3 polynomial for float (fastest option tested)
			// Use order-4 polynomial for double (fastest option tested)
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastExpMethodFloat()
				: GenerateFastExpMethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExpMethodFloat()
	{
		return """
			private static float FastExp(float x)
			{
				// Safe bounds
				if (Single.IsNaN(x)) return Single.NaN;
				if (x >= 88.0f) return Single.PositiveInfinity;
				if (x <= -87.0f) return 0.0f;

				const float INV_LN2 = 1.4426950408889634f;  // log₂(e)

				var kf = x * INV_LN2;
				var k  = (int)Single.Round(kf);  // branchless FRINTN + FCVTZS on ARM64
				var r  = kf - k;                // fractional bits of log₂(eˣ), r ∈ [-0.5, 0.5]

				// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!
				// Eliminates the FMA(-k, LN2, x) range-reduction step vs Taylor e^r approach.
				const float c3 = 0.055504108664821580f;  // ln(2)³ / 6
				const float c2 = 0.240226506959100690f;  // ln(2)² / 2
				const float c1 = 0.693147180559945309f;  // ln(2)

				var p    = Single.FusedMultiplyAdd(c3, r, c2);
				p        = Single.FusedMultiplyAdd(p,  r, c1);
				var expR = Single.FusedMultiplyAdd(p,  r, 1.0f);

				return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
			}
			""";
	}

	private static string GenerateFastExpMethodDouble()
	{
		return """
			private static double FastExp(double x)
			{
				// Safe bounds
				if (Double.IsNaN(x)) return Double.NaN;
				if (x >= 709.0) 
					return Double.PositiveInfinity;
					
				if (x <= -708.0) 
					return 0.0;

				const double INV_LN2 = 1.4426950408889634073599246810018921;  // log₂(e)

				var kf = x * INV_LN2;
				var k  = (long)Double.Round(kf);  // branchless on ARM64
				var r  = kf - k;                // fractional bits of log₂(eˣ), r ∈ [-0.5, 0.5]

				// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!
				// Eliminates the FMA(-k, LN2, x) range-reduction step vs Taylor e^r approach.
				const double c4 = 9.618129107628477232e-3;  // ln(2)⁴ / 24
				const double c3 = 5.550410866482157995e-2;  // ln(2)³ / 6
				const double c2 = 2.402265069591006909e-1;  // ln(2)² / 2
				const double c1 = 6.931471805599453094e-1;  // ln(2)

				var p    = Double.FusedMultiplyAdd(c4, r, c3);
				p        = Double.FusedMultiplyAdd(p,  r, c2);
				p        = Double.FusedMultiplyAdd(p,  r, c1);
				var expR = Double.FusedMultiplyAdd(p,  r, 1.0);

				var bits = (ulong)((k + 1023L) << 52);
				return BitConverter.UInt64BitsToDouble(bits) * expR;
			}
			""";
	}
}