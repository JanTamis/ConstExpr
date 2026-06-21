using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Optimizes Replace calls:
///   - "hello".Replace("l", "r") → "herro" (constant fold when instance is a literal)
///   - s.Replace("a", "a") → s (no-op when old and new are equal)
///   - s.Replace('a', 'a') → s
/// </summary>
public class ReplaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Replace", false, n => n is 2)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (context.VisitedParameters[0] is not LiteralExpressionSyntax first
		    || context.VisitedParameters[1] is not LiteralExpressionSyntax second)
		{
			return false;
		}

		// Constant fold when instance is a known string literal
		if (TryGetStringInstance(out var str) && str is not null)
		{
			if (first.Token.Value is string oldStr && second.Token.Value is string newStr)
			{
				result = CreateLiteral(str.Replace(oldStr, newStr));
				return true;
			}

			if (first.Token.Value is char oldChar && second.Token.Value is char newChar)
			{
				result = CreateLiteral(str.Replace(oldChar, newChar));
				return true;
			}
		}

		// No-op when old and new values are the same: s.Replace("a", "a") → s
		if (Equals(first.Token.Value, second.Token.Value))
		{
			result = Instance;
			return true;
		}

		return false;
	}
}