using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.BitOperationsOptimizers;

/// <summary>
///   Inlines <c>BitOperations.RotateLeft(value, offset)</c> to bitshift expressions.
///   For <c>uint</c>:  <c>(value &lt;&lt; offset) | (value &gt;&gt; (32 - offset))</c>
///   For <c>ulong</c>: <c>(value &lt;&lt; offset) | (value &gt;&gt; (64 - offset))</c>
///   Constant arguments are folded by <c>TryExecuteWithConstantArguments</c> upstream.
/// </summary>
public class RotateLeftFunctionOptimizer() : BaseBitOperationsFunctionOptimizer("RotateLeft", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var value = context.VisitedParameters[0];
		var offset = context.VisitedParameters[1];

		if (!IsPure(value))
		{
			result = null;
			return false;
		}

		var bitWidth = paramType.SpecialType switch
		{
			SpecialType.System_UInt32 => 32,
			SpecialType.System_UInt64 => 64,
			_ => 0
		};

		if (bitWidth == 0)
		{
			result = null;
			return false;
		}

		// (value << offset) | (value >> (bitWidth - offset))
		result = BuildRotate(value, offset, bitWidth, true);
		return true;
	}

	internal static ExpressionSyntax BuildRotate(ExpressionSyntax value, ExpressionSyntax offset, int bitWidth, bool left)
	{
		var width = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(bitWidth));
		var complement = BinaryExpression(SyntaxKind.SubtractExpression, width, offset);

		var (firstShift, secondShift) = left
			? (SyntaxKind.LeftShiftExpression, SyntaxKind.RightShiftExpression)
			: (SyntaxKind.RightShiftExpression, SyntaxKind.LeftShiftExpression);

		var shiftA = ParenthesizedExpression(BinaryExpression(firstShift, value, offset));
		var shiftB = ParenthesizedExpression(BinaryExpression(secondShift, value, ParenthesizedExpression(complement)));

		return BinaryExpression(SyntaxKind.BitwiseOrExpression, shiftA, shiftB);
	}
}