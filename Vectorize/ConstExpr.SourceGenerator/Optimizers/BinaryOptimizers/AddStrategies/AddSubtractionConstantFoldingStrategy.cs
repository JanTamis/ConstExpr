using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for constant folding when adding a constant to a subtraction expression:
/// (C1 - x) + C2 => (C1 + C2) - x  — constant on the left of inner subtract
/// (x - C1) + C2 => x + (C2 - C1)  — constant on the right of inner subtract
/// Both patterns also apply symmetrically: C2 + (C1 - x) and C2 + (x - C1)
/// Example: (1 - start) + 1 => 2 - start
/// Example: (start - 3) + 5 => start + 2
/// Requires AssociativeMath for floating-point safety.
/// </summary>
public class AddSubtractionConstantFoldingStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.SubtractExpression)
{
	public override FastMathFlags RequiredFlags => FastMathFlags.AssociativeMath;

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var c2 = context.Right.Syntax.Token.Value;

		// Pattern 1: (C1 - x) + C2 => (C1 + C2) - x
		if (context.TryGetValue(context.Left.Syntax.Left, out var c1Left)
		    && TryCreateLiteral(c1Left.Add(c2), out var foldedLeft))
		{
			optimized = SubtractExpression(foldedLeft, context.Left.Syntax.Right);
			return true;
		}

		// Pattern 2: (x - C1) + C2 => x + (C2 - C1)
		if (context.TryGetValue(context.Left.Syntax.Right, out var c1Right)
		    && TryCreateLiteral(c2.Subtract(c1Right), out var foldedRight))
		{
			optimized = AddExpression(context.Left.Syntax.Left, foldedRight);
			return true;
		}

		optimized = null;
		return false;
	}
}