// filepath: /Users/jantamiskossen/RiderProjects/Vectorize/Vectorize/ConstExpr.SourceGenerator/Optimizers/FunctionOptimizers/PowFunctionOptimizer.cs
using System;
using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class PowFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Pow")
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

		// Expect two parameters for Pow
		if (parameters.Count != 2)
		{
			return false;
		}

		var x = parameters[0];
		var y = parameters[1];

		// Algebraic simplifications on literal exponents (safe and type-preserving)
		if (TryGetNumericLiteral(y, out var exp))
		{
			// x^0 => 1
			if (IsApproximately(exp, 0.0))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0));
				return true;
			}

			// x^(-1) => Reciprocal(x) bij fast-math, anders 1/x
			if (IsApproximately(exp, -1.0) && IsPure(x))
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "Reciprocal", 1))
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}
				
				var div = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
					SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType)), x);
				result = SyntaxFactory.ParenthesizedExpression(div);
				return true;
			}

			// x^n => x * x * ... * x for small integer n
			if (Math.Abs(exp) > 1.0 && Math.Abs(exp) <= 5.0 && IsPure(x) && Math.Abs(exp - Math.Round(exp)) < Double.Epsilon)
			{
				var n = (int)Math.Round(exp);
				var acc = x;
				
				for (var i = 1; i < Math.Abs(n); i++)
				{
					acc = SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, acc, x);
				}
				
				if (n < 0)
				{
					acc = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
						SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0)), acc);
				}
				
				result = SyntaxFactory.ParenthesizedExpression(acc);
				return true;
			}

			// x^1 => x
			if (IsApproximately(exp, 1.0))
			{
				result = x;
				return true;
			}

			// x^2 => (x * x) when x is pure (no side-effects)
			if (IsApproximately(exp, 2.0) && IsPure(x))
			{
				var mul = SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, x, x);
				result = SyntaxFactory.ParenthesizedExpression(mul);
				return true;
			}

			// x^(1 / 2) => Sqrt(x)
			if (IsApproximately(exp, 1 / 2.0) && HasMethod(paramType, "Sqrt", 1))
			{
				result = CreateInvocation(paramType, "Sqrt", x);
				return true;
			}
			
			// x^(1 / 3) => Cbrt(x)
			if (IsApproximately(exp, 1 / 3.0) && HasMethod(paramType, "Cbrt", 1))
			{
				result = CreateInvocation(paramType, "Cbrt", x);
				return true;
			}

			// x^(1 / n) => RootN(x, n) for small integer n
			if (IsApproximately(1 / exp, Math.Floor(1 / exp)))
			{
				result = CreateInvocation(paramType, "RootN", x, SyntaxHelpers.CreateLiteral((int)Math.Round(1 / exp)));
				return true;
			}
		}

		result = CreateInvocation(paramType, "Pow", x, y);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true; 
			default:
				return false;
		}
	}

	private static bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= Double.Epsilon;
	}
}
