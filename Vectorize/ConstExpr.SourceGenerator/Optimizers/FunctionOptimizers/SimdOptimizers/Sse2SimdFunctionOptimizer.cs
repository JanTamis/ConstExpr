using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class Sse2SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Sse2")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// Load operations with different names
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

					// Convert with different names
					case "ConvertToVector128Double":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToDouble", context.VisitedParameters);
						return true;
					}
					case "ConvertToVector128Int32":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToInt32", context.VisitedParameters);
						return true;
					}
					case "ConvertToVector128Single":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToSingle", context.VisitedParameters);
						return true;
					}

					// Shuffle(vec, byteConst): decode 4 2-bit lane indices from the byte literal
					case "Shuffle" when context.VisitedParameters[^1] is LiteralExpressionSyntax { Token.Value: byte shuffleConst }:
					{
						var indexes = new int[4];

						for (var i = 0; i < 4; i++)
						{
							indexes[i] = (byte) (shuffleConst >> i * 2 & 0b11);
						}

						result = CreateSimdInvocation(context, vectorType, "Shuffle", context.VisitedParameters.Take(1).Append(CreateInvocation("Create", indexes.Select(s => CreateLiteral(s)))));
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