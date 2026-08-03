using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
///   Strategy that simplifies (uint)x &lt;= 0 → (uint)x == 0.
///   Unsigned integer types can never be negative, so &lt;= 0 is equivalent to == 0.
///   Safe under Strict.
/// </summary>
public class LessThanOrEqualUnsignedZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (uint)x <= 0 → (uint)x == 0
		if (IsUnsignedType(context.Left.Type)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = EqualsExpression(context.Left.Syntax, context.Right.Syntax);
			return true;
		}

		optimized = null;
		return false;
	}

	private static bool IsUnsignedType(ITypeSymbol? type)
	{
		return type?.SpecialType is SpecialType.System_Byte
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64;
	}
}