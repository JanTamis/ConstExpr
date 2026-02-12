using System;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CopySignFunctionOptimizer() : BaseMathFunctionOptimizer("CopySign", 2)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		var x = context.VisitedParameters[0];
		var y = context.VisitedParameters[1];

		// 1) Unsigned integer: sign has no effect
		if (paramType.IsUnsignedInteger())
		{
			result = x;
			return true;
		}

		// 2) If sign is a numeric literal 0 => Abs(x)
		if (TryGetNumericLiteral(y, out var signVal) && IsApproximately(signVal, 0.0))
		{
			if (HasMethod(paramType, "Abs", 1))
			{
				result = CreateInvocation(paramType, "Abs", x);
				return true;
			}
		}

		// Default: forward to target numeric helper type
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}
}
