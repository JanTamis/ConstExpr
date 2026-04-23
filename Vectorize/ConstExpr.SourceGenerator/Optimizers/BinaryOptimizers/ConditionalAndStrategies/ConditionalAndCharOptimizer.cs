using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalAndCharOptimizer() 
	: SymmetricStrategy<BooleanBinaryStrategy, BinaryExpressionSyntax, BinaryExpressionSyntax>(SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.LessThanOrEqualExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax.Left, context.Variables)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftValue)
		    || !context.TryGetValue(context.Right.Syntax.Right, out var rightValue)
		    || leftValue is not char
		    || rightValue is not char)
		{
			optimized = null;
			return false;
		}

		var memberName = (leftValue, rightValue) switch
		{
			('A', 'Z') => "IsAsciiLetterUpper",
			('a', 'z') => "IsAsciiLetterLower",
			('0', '9') => "IsAsciiDigit",
			_ => String.Empty
		};

		if (!String.IsNullOrEmpty(memberName))
		{
			if (context.TryGetValue(context.Left.Syntax.Left, out var charValue))
			{
				// get method via reflection and invoke it on the constant char value to fold the entire expression to a bool constant
				var charType = typeof(char);
				var charMethod = charType.GetMethod(memberName, [ charType ]);

				if (charMethod is null)
				{
					optimized = null;
					return false;
				}

				return TryCreateLiteral(charMethod.Invoke(null, [ charValue ]), out optimized);
			}
			
			optimized =  InvocationExpression(
				MemberAccessExpression(ParseTypeName("Char"), IdentifierName(memberName)),
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax.Left))));
			
			return true;
		}

		if (leftValue.LessThan(rightValue))
		{
			if (context.TryGetValue(context.Left.Syntax.Left, out var charValue))
			{
				// get method via reflection and invoke it on the constant char value to fold the entire expression to a bool constant
				var charType = typeof(char);
				var charMethod = charType.GetMethod("IsBetween", [ charType, charType, charType ]);

				if (charMethod is null)
				{
					optimized = null;
					return false;
				}

				return TryCreateLiteral(charMethod.Invoke(null, [ charValue, leftValue, rightValue ]), out optimized);
			}
			
			optimized = InvocationExpression(
				MemberAccessExpression(ParseTypeName("Char"), IdentifierName("IsBetween")),
				ArgumentList([ Argument(context.Left.Syntax.Left), Argument(context.Left.Syntax.Right), Argument(context.Right.Syntax.Right) ]));
			
			return true;
		}
		
		optimized = null;
		return false;
	}
}