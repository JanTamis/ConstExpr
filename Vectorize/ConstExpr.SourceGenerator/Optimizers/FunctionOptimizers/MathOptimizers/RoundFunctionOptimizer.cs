using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RoundFunctionOptimizer() : BaseMathFunctionOptimizer("Round", 1, 2, 3)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		// 1) If the inner expression already yields an integer-valued result, keep inner: Truncate/Floor/Ceiling/Round -> they return integer
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Truncate" or "Floor" or "Ceiling" or "Round" } } inv)
		{
			result = inv;
			return true;
		}

		// 2) Integer types: Round(x) -> x (round has no effect on integers)
		if (paramType.IsNonFloatingNumeric())
		{
			result = context.VisitedParameters[0];
			return true;
		}

		// 3) Unary minus: Round(-x) -> -Round(x)
		if (context.VisitedParameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			// Keep sign and round the operand
			var roundCall = CreateInvocation(paramType, "Round", prefix.Operand);

			result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(roundCall));
			return true;
		}

		// 4) check if parent of context.Invocation is casting to integer type
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

		// Default: keep as Round call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}