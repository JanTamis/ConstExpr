using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CeilingFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Ceiling")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName;

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		if (!paramType.IsNumericType())
		{
			return false;
		}

		// 1) Idempotence: Ceiling(Ceiling(x)) -> Ceiling(x)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 2) Ceiling(Floor(x)) -> Floor(x) when x is already an integer, but we can't determine that statically
		//    However, Ceiling(Floor(x)) generally simplifies to Floor(x) for most cases
		//    Skip this optimization as it's not always valid

		// 3) Unary minus: Ceiling(-x) -> -Floor(x)
		if (parameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			var floorCall = CreateInvocation(paramType, "Floor", prefix.Operand);
			result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(floorCall));
			return true;
		}

		// Default: keep as Ceiling call (target numeric helper type)
		result = CreateInvocation(paramType, "Ceiling", parameters[0]);
		return true;
	}
}
