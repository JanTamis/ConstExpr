using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes usages of <c>string.Substring</c>. This optimizer:
/// - Converts certain Substring calls to range-based element access when possible (for example, <c>s.Substring(start, length)</c> -> <c>s[start..(start+length)]</c> or <c>s[..to]</c> when start is 0).
/// - Returns an empty string literal for zero-length requests when that can be determined at compile time.
/// - Rewrites the context.Invocation to use range/element access expressions when appropriate while preserving semantics.
/// </summary>
/// <param name="instance">Optional syntax node instance provided by the optimizer infrastructure; may be null.</param>
public class SubstringFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Substring", false, n => n is 1 or 2)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				var targetExpr = instance as ExpressionSyntax ?? (context.Invocation.Expression is MemberAccessExpressionSyntax m ? m.Expression : null);

				if (targetExpr == null)
				{
					return false;
				}

				var range = RangeExpression(context.VisitedParameters[0], null);
				var bracketedArgs = BracketedArgumentList(SingletonSeparatedList(Argument(range)));

				result = ElementAccessExpression(targetExpr, bracketedArgs);
				return true;
			}
			case 2:
			{
				var targetExpr = instance as ExpressionSyntax ?? (context.Invocation.Expression is MemberAccessExpressionSyntax m ? m.Expression : null);

				if (targetExpr == null)
				{
					return false;
				}

				var start = context.VisitedParameters[0];
				var length = context.VisitedParameters[1];

				var toExpr = AddExpression(length, start);
				var range = RangeExpression(start, ParenthesizedExpression(toExpr));

				// If length is a constant 0, return empty string literal
				if (length is LiteralExpressionSyntax lengthLit && lengthLit.IsKind(SyntaxKind.NumericLiteralExpression))
				{
					if (lengthLit.Token.Value is 0)
					{
						result = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(String.Empty));
						return true;
					}
				}

				// If start is a constant 0, use the `..to` range form
				if (start is LiteralExpressionSyntax startLit && startLit.IsKind(SyntaxKind.NumericLiteralExpression))
				{
					if (startLit.Token.Value is 0)
					{
						range = RangeExpression(null, length);
					}
				}

				var bracketedArgs = BracketedArgumentList(SingletonSeparatedList(Argument(range)));

				result = ElementAccessExpression(targetExpr, bracketedArgs);
				return true;
			}
			default:
			{
				return false;
			}
		}
	}
}