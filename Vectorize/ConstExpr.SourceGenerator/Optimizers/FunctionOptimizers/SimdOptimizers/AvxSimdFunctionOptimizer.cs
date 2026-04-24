using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class AvxSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Avx")
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
					case "LoadVector256":
					{
						result = CreateSimdInvocation(context, vectorType, "Load", context.VisitedParameters);
						return true;
					}
					case "LoadAlignedVector256":
					{
						result = CreateSimdInvocation(context, vectorType, "LoadAligned", context.VisitedParameters);
						return true;
					}

					// RoundToZero → Truncate
					case "RoundToZero":
					{
						result = CreateSimdInvocation(context, vectorType, "Truncate", context.VisitedParameters);
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

					// Store with different name
					case "StoreNonTemporal":
					{
						result = CreateSimdInvocation(context, vectorType, "StoreAlignedNonTemporal", context.VisitedParameters);
						return true;
					}

					// Convert with different names
					case "ConvertToVector256Single":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToSingle", context.VisitedParameters);
						return true;
					}
					case "ConvertToVector256Double":
					{
						result = CreateSimdInvocation(context, vectorType, "ConvertToDouble", context.VisitedParameters);
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

					// Shuffle(v1, v2, byteConst): decode 4 2-bit lane indices from the byte literal
					case "Shuffle" when context.VisitedParameters[^1] is LiteralExpressionSyntax { Token.Value: byte shuffleConst }:
					{
						var indexes = new int[4];

						for (var i = 0; i < 4; i++)
						{
							indexes[i] = (byte) (shuffleConst >> i * 2 & 0b11);
						}

						result = CreateSimdInvocation(context, vectorType, "Shuffle", context.VisitedParameters.Take(2).Append(CreateInvocation("Create", indexes.Select(s => CreateLiteral(s)))));
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