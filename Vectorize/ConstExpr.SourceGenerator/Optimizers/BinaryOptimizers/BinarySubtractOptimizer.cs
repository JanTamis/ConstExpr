using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System.Linq;
using ConstExpr.SourceGenerator.Visitors;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinarySubtractOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Subtract;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}
		
		Left.TryGetLiteralValue(loader, variables, out var leftValue);
		Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x - 0 = x
		if (rightValue.IsNumericZero())
		{
			result = Left;
			return true;
		}

		// 0 - x = -x
		if (leftValue.IsNumericZero())
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Right);
			return true;
		}

		// x - x = 0 (pure)
		if (LeftEqualsRight(variables) && IsPure(Left) && IsPure(Right))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x - -y  => x + y (pure)
		if (Right is PrefixUnaryExpressionSyntax pre && pre.IsKind(SyntaxKind.UnaryMinusExpression)
		    && IsPure(Left) && IsPure(pre.Operand))
		{
			result = BinaryExpression(SyntaxKind.AddExpression, Left, pre.Operand);
			return true;
		}

		// Fused Multiply-Add pattern: (a * b) - c => FMA(a,b,-c)
		// Only in FastMath and for float/double (semantic change due to single rounding)
		if (Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, Type))))
		{
			var host = ParseName(Type.Name);
			var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

			if (Left is BinaryExpressionSyntax multLeftSyntax && multLeftSyntax.IsKind(SyntaxKind.MultiplyExpression)
			    && IsPure(multLeftSyntax.Left) && IsPure(multLeftSyntax.Right) && IsPure(Right))
			{
				var aExpr = multLeftSyntax.Left;
				var bExpr = multLeftSyntax.Right;
				var cExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Right);

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(cExpr) ])));
				return true;
			}

			// Fused Multiply-Add pattern: c - (a * b) => FMA(-a, b, c)
			if (Right is BinaryExpressionSyntax multRightSyntax && multRightSyntax.IsKind(SyntaxKind.MultiplyExpression)
			    && IsPure(multRightSyntax.Left) && IsPure(multRightSyntax.Right) && IsPure(Left))
			{
				var aExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, multRightSyntax.Left);
				var bExpr = multRightSyntax.Right;

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(Left) ])));
				return true;
			}
		}

		return false;
	}
}