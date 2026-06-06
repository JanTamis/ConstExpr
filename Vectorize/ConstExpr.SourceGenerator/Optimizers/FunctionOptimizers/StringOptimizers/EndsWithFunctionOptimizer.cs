using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes EndsWith calls on string literals:
/// - "hello".EndsWith("lo") → true
/// - "hello".EndsWith("world") → false
/// - "hello".EndsWith('o') → 'o' == 'o' (or false for empty string)
/// </summary>
public class EndsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "EndsWith", false, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (!TryGetStringInstance(out var instanceString))
		{
			return false;
		}

		if (context.Method.Parameters[0].Type.SpecialType == SpecialType.System_Char)
		{
			if (String.IsNullOrEmpty(instanceString))
			{
				result = CreateLiteral(false);
				return true;
			}

			result = EqualsExpression(CreateLiteral(instanceString![^1]), context.VisitedParameters[0]);
			return true;
		}

		if (context.Method.Parameters[0].Type.SpecialType == SpecialType.System_String)
		{
			if (instanceString is null)
			{
				return false;
			}

			if (context.VisitedParameters[0] is not LiteralExpressionSyntax literal
			    || !literal.IsKind(SyntaxKind.StringLiteralExpression))
			{
				return false;
			}

			result = CreateLiteral(instanceString.EndsWith(literal.Token.ValueText, StringComparison.Ordinal));
			return true;
		}

		return false;
	}
}