using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RoundFunctionOptimizer() : BaseMathFunctionOptimizer("Round", 1, 2, 3)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		// 1) If the inner expression already yields an integer-valued result, keep inner: Truncate/Floor/Ceiling/Round -> they return integer
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Truncate" or "Floor" or "Ceiling" or "Round" } } inv)
		{
			result = inv;
			return true;
		}

		// 2) Integer types: Round(x) -> x (round has no effect on integers)
		if (paramType.IsNonFloatingNumeric())
		{
			result = parameters[0];
			return true;
		}

		// 3) Unary minus: Round(-x) -> -Round(x)
		if (parameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			// Keep sign and round the operand
			var roundCall = CreateInvocation(paramType, "Round", prefix.Operand);

			result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(roundCall));
			return true;
		}

		// 4) check if parent of invocation is casting to integer type
		if (invocation.Parent is CastExpressionSyntax
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
				&& parameters.Count == 2)
		{
			// Check that the second argument is a compile-time MidpointRounding enum member
			string? enumMember = null;

			switch (parameters[1])
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
					result = CreateInvocation(paramType, "Truncate", parameters.Take(1));
					return true;
				case "ToPositiveInfinity":
					result = CreateInvocation(paramType, "Ceiling", parameters.Take(1));
					return true;
				case "ToNegativeInfinity":
					result = CreateInvocation(paramType, "Floor", parameters.Take(1));
					return true;
			}
		}

		// Default: keep as Round call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}
}