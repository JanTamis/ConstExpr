using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for constant folding in integer addition chains without requiring AssociativeMath.
/// Integer addition is always associative and commutative, so this is unconditionally safe.
/// Handles:
///   Pattern 1:  (x + C1) + C2       => x + (C1 + C2)
///   Pattern 1b: (C1 + x) + C2       => x + (C1 + C2)
///   Pattern 2:  ((x + C1) + y) + C2 => (x + y) + (C1 + C2)  — collects non-adjacent constants
/// </summary>
public class AddIntegerConstantFoldingStrategy : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		// Right operand must be a constant and the left operand must be an addition chain.
		if (!context.TryGetValue(context.Right.Syntax, out var c2)
		    || !context.Left.Syntax.IsKind(SyntaxKind.AddExpression))
		{
			return false;
		}

		// Walk the left-hand addition chain, extract the first constant found,
		// combine it with c2, and place the sum at the rightmost position.
		if (TryExtractConstant(context.TryGetValue, context.Left.Syntax, out var remaining, out var extracted))
		{
			var sum = extracted.Add(c2);
			optimized = AddExpression(remaining, CreateLiteral(sum));
			return true;
		}

		return false;
	}

	/// <summary>
	/// Walks an addition chain (left-associative) and extracts the first constant leaf,
	/// returning the remaining expression with the constant removed.
	/// </summary>
	private static bool TryExtractConstant(
		TryGetValueDelegate tryGetValue,
		ExpressionSyntax node,
		out ExpressionSyntax remaining,
		out object extracted,
		int maxDepth = 4)
	{
		remaining = node;
		extracted = null!;

		if (node is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.AddExpression))
		{
			return false;
		}

		// Check right side of this addition
		if (tryGetValue(binary.Right, out extracted!))
		{
			remaining = binary.Left;
			return true;
		}

		// Check left side of this addition
		if (tryGetValue(binary.Left, out extracted!))
		{
			remaining = binary.Right;
			return true;
		}

		// Recurse into left subtree (the chain grows leftward in left-associative parsing)
		if (maxDepth > 0
		    && TryExtractConstant(tryGetValue, binary.Left, out var innerRemaining, out extracted, maxDepth - 1))
		{
			remaining = AddExpression(innerRemaining, binary.Right);
			return true;
		}

		return false;
	}
}

