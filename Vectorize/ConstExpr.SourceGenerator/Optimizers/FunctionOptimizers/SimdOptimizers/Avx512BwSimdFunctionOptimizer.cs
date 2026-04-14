using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class Avx512BwSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Avx512BW")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
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
				}
				break;
			}
		}

		result = null;
		return false;
	}
}

