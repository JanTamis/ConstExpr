using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class Avx512FSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Avx512F")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					case "LoadVector512":
					{
						result = CreateSimdInvocation(context, vectorType, "Load", context.VisitedParameters);
						return true;
					}
					// ConvertToVector512Single / ConvertToVector512Double have
					// platform-specific names; map to the cross-platform equivalents.
					case "ConvertToVector512Single":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToSingle", context.VisitedParameters);
						return true;
					}
					case "ConvertToVector512Double":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToDouble", context.VisitedParameters);
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
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "Subtract":
					{
						result = SubtractExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "Multiply":
					{
						result = MultiplyExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "Divide":
					{
						result = DivideExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}

					// Bitwise operators
					case "And":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "Or":
					{
						result = BitwiseOrExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "Xor":
					{
						result = ExclusiveOrExpression(context.VisitedParameters[0], context.VisitedParameters[1])
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
						return true;
					}
					case "AndNot":
					{
						result = BitwiseAndExpression(context.VisitedParameters[0], BitwiseNotExpression(context.VisitedParameters[1]))
							.WithTypeSymbolAnnotation(vectorType, context.SymbolStore);
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
					// CompareNotEqual → ~Equals
					case "CompareNotEqual":
					{
						result = BitwiseNotExpression(CreateSimdInvocation(context, vectorType, "Equals", context.VisitedParameters));
						return true;
					}

					// Shift with different name
					case "ShiftLeftLogical":
					{
						result = CreateSimdInvocation(context, vectorType, "ShiftLeft", context.VisitedParameters);
						return true;
					}

					// Store with different name
					case "StoreNonTemporal":
					{
						result = CreateSimdInvocation(context, vectorType, "StoreAlignedNonTemporal", context.VisitedParameters);
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
