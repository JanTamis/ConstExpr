using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class SseSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Sse")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// Load operations with different names (take a single pointer parameter)
					case "LoadVector128":
					{
						result = CreateSimdInvocation(context, vectorType, "Load", context.VisitedParameters);
						return true;
					}
					case "LoadAlignedVector128":
					{
						result = CreateSimdInvocation(context, vectorType, "LoadAligned", context.VisitedParameters);
						return true;
					}
					case "LoadScalarVector128":
					{
						result = CreateSimdInvocation(context, vectorType, "CreateScalar", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
			case 2:
			{
				switch (context.Method.Name)
				{
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
					case "Multiply":
					{
						result = MultiplyExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
					case "Divide":
					{
						result = DivideExpression(context.VisitedParameters[0], context.VisitedParameters[1]);
						return true;
					}
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
					// Comparisons with different names — must use vectorType so the generated
					// call resolves against Vector128<T>, not an unqualified identifier.
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
					case "CompareNotEqual":
					{
						// OnesComplement(Equals(...))
						result = BitwiseNotExpression(CreateSimdInvocation(context, vectorType, "Equals", context.VisitedParameters));
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