using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class Avx2SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Avx2")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// Widen conversions: take a single narrow vector and produce a wider Vector256.
					// These have platform-specific names but map to Vector256.Widen on the
					// cross-platform API.
					case "ConvertToVector256Int16":
					case "ConvertToVector256Int32":
					case "ConvertToVector256Int64":
					case "ConvertToVector256UInt16":
					case "ConvertToVector256UInt32":
					case "ConvertToVector256UInt64":
					{
						result = CreateSimdInvocation(context, vectorType, "Widen", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
			case 2:
			{
				switch (context.Method.Name)
				{
					// Arithmetic operators
					case "Add":
					{
						result = AddExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
					case "Subtract":
					{
						result = SubtractExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}

					// Bitwise operators
					case "And":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
					case "Or":
					{
						result = BitwiseOrExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
					case "Xor":
					{
						result = ExclusiveOrExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
					case "AndNot":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], BitwiseNotExpression(context.VisitedParameters[1]));
						return true;
					}

					// Comparisons with different names
					case "CompareEqual":
					{
						result = CreateSimdInvocation(context, vectorType, "Equals", context.VisitedParameters);
						return true;
					}
					case "CompareGreaterThan":
					{
						result = CreateSimdInvocation(context, vectorType, "GreaterThan", context.VisitedParameters);
						return true;
					}

					// Shift with different name
					case "ShiftLeftLogical":
					{
						result = CreateSimdInvocation(context, vectorType, "ShiftLeft", context.VisitedParameters);
						return true;
					}

					// Saturating narrow conversions: Avx2.PackSignedSaturate /
					// PackUnsignedSaturate clamp on overflow; use NarrowWithSaturation
					// (not Narrow, which truncates).
					case "PackSignedSaturate":
					case "PackUnsignedSaturate":
					{
						result = CreateSimdInvocation(context, vectorType, "NarrowWithSaturation", context.VisitedParameters);
						return true;
					}

					// Shuffle(vec, byteConst): decode 4 2-bit lane indices from the byte literal
					case "Shuffle" when context.VisitedParameters[^1] is LiteralExpressionSyntax { Token.Value: byte shuffleConst }:
					{
						var indexes = new int[4];

						for (var i = 0; i < 4; i++)
						{
							indexes[i] = (byte)(shuffleConst >> i * 2 & 0b11);
						}

						result = CreateSimdInvocation(context, vectorType, "Shuffle", context.VisitedParameters.Take(1).Append(CreateInvocation("Create", indexes.Select(s => CreateLiteral(s)))));
						return true;
					}
				}
				break;
			}
			case 3:
			{
				switch (context.Method.Name)
				{
					// BlendVariable → ConditionalSelect
					case "BlendVariable":
					{
						result = CreateSimdInvocation(context, vectorType, "ConditionalSelect", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
		}

		result = null;
		return false;
	}
}
