using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

/// <summary>ARM Advanced SIMD (Neon) intrinsics.</summary>
public class AdvSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("AdvSimd", "Arm")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// Not → OnesComplement (~a)
					case "Not":
					{
						result = BitwiseNotExpression(context.VisitedParameters[0])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					// Negate → unary minus (-a)
					case "Negate":
					{
						result = UnaryMinusExpression(context.VisitedParameters[0])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					// Load with different name
					case "LoadVector128":
					{
						result = CreateSimdInvocation(context, vectorType, "Load", context.VisitedParameters);
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
						result = AddExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					case "Subtract":
					{
						result = SubtractExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					case "Multiply":
					{
						result = MultiplyExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}

					// Bitwise operators
					case "And":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					case "Or":
					{
						result = BitwiseOrExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					case "Xor":
					{
						result = ExclusiveOrExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					// BitwiseClear(a, b) = a & ~b → maps to Vector128.AndNot
					case "BitwiseClear":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], BitwiseNotExpression(context.VisitedParameters[1]))
							.WithTypeSymbolAnnotation(vectorType);
						return true;
					}
					// OrNot(a, b) = a | ~b
					case "OrNot":
					{
						result = BitwiseOrExpression(context.VisitedParameters[0], BitwiseNotExpression(context.VisitedParameters[1]))
							.WithTypeSymbolAnnotation(vectorType);
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
					case "CompareLessThan":
					{
						result = CreateSimdInvocation(context, vectorType, "LessThan", context.VisitedParameters);
						return true;
					}
					case "CompareGreaterThanOrEqual":
					{
						result = CreateSimdInvocation(context, vectorType, "GreaterThanOrEqual", context.VisitedParameters);
						return true;
					}
					case "CompareLessThanOrEqual":
					{
						result = CreateSimdInvocation(context, vectorType, "LessThanOrEqual", context.VisitedParameters);
						return true;
					}

					// Shift with different name
					case "ShiftLeftLogical":
					{
						result = CreateSimdInvocation(context, vectorType, "ShiftLeft", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
			case 3:
			{
				switch (context.Method.Name)
				{
					// BitwiseSelect(mask, left, right) → ConditionalSelect(mask, left, right)
					case "BitwiseSelect":
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
