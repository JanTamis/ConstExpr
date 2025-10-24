using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for normalizing negative divisor: x % (-m) => x % m (signed integers)
/// </summary>
public class ModuloNormalizeNegativeDivisorStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context) || context.Type.IsUnsignedInteger())
			return false;

		if (!context.Right.HasValue || context.Right.Value == null)
			return false;

		var zero = 0.ToSpecialType(context.Type.SpecialType);
		var isNegative = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, context.Right.Value, zero) is true;
		
		if (!isNegative)
			return false;

		// Skip when rightValue is MinValue for the signed type
		var isMin = context.Type.SpecialType switch
		{
			SpecialType.System_SByte => context.Right.Value is sbyte.MinValue,
			SpecialType.System_Int16 => context.Right.Value is short.MinValue,
			SpecialType.System_Int32 => context.Right.Value is int.MinValue,
			SpecialType.System_Int64 => context.Right.Value is long.MinValue,
			_ => false
		};

		return !isMin;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var abs = ObjectExtensions.Abs(context.Right.Value, context.Type.SpecialType);

		if (abs != null && SyntaxHelpers.TryGetLiteral(abs, out var absLiteral))
		{
			return BinaryExpression(SyntaxKind.ModuloExpression, context.Left.Syntax, absLiteral);
		}

		return null;
	}
}
