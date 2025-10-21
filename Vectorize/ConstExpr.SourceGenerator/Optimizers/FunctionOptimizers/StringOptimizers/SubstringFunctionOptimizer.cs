using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	public class SubstringFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Substring")
	{
		public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
		{
			result = null;

			if (!IsValidMethod(method, out var stringType))
			{
				return false;
			}

			if (method.Parameters.Length == 1)
			{
				var targetExpr = instance as ExpressionSyntax ?? (invocation.Expression is MemberAccessExpressionSyntax m ? m.Expression : null);

				if (targetExpr == null)
				{
					return false;
				}

				var range = RangeExpression(parameters[0], null);
				var bracketedArgs = BracketedArgumentList(SingletonSeparatedList(Argument(range)));

				result = ElementAccessExpression(targetExpr, bracketedArgs);
				return true;
			}

			if (method.Parameters.Length == 2)
			{
				var targetExpr = instance as ExpressionSyntax ?? (invocation.Expression is MemberAccessExpressionSyntax m ? m.Expression : null);

				if (targetExpr == null)
				{
					return false;
				}

				var start = parameters[0];
				var length = parameters[1];

				var toExpr = BinaryExpression(SyntaxKind.AddExpression, length, start);
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
						// var bracketedArgs = BracketedArgumentList(SingletonSeparatedList(Argument(RangeExpression(null, length))));
						// result = ElementAccessExpression(targetExpr, bracketedArgs);
						// return true;
					}
				}
			
				var bracketedArgs = BracketedArgumentList(SingletonSeparatedList(Argument(range)));

				result = ElementAccessExpression(targetExpr, bracketedArgs);
				return true;
			}

			return false;
		}
	}
}