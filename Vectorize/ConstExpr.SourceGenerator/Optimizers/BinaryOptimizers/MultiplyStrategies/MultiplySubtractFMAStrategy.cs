using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
///   Distributes <c>(a - b) * c</c> into a fused multiply-add, in either operand order, but only for the
///   two cases where one product reuses an existing operand and so is guaranteed at-least-as-good:
///   <list type="bullet">
///     <item><c>a == 1</c> => <c>FMA(-b, c, c)</c>, e.g. <c>(1 - k) * 255 => FMA(-k, 255, 255)</c></item>
///     <item><c>b == 1</c> => <c>FMA(a, c, -c)</c>, e.g. <c>(k - 1) * prod => FMA(k, prod, -prod)</c></item>
///   </list>
///   The fully general <c>(a - b) * c</c> is deliberately left alone: distributing it either adds a
///   multiply (all-variable operands) or, when a factor is constant, pre-empts the global add/subtract
///   FMA chaining and produces worse code (e.g. YUV's <c>(v - 128) * 1.4075</c> keeps its better nested form).
///   <c>c</c> is duplicated into the fma, so it must be a leaf (identifier/literal/member-access) to avoid
///   evaluating a side-effecting operand twice. Requires the FusedMultiplyAdd flag as FMA rounds differently.
/// </summary>
public class MultiplySubtractFMAStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.FusedMultiplyAdd ];

	/// <summary>
	///   <paramref name="context" /> Left is the <c>(a - b)</c> side and Right is the factor <c>c</c>; the
	///   symmetric base also invokes this with the operands swapped, covering <c>c * (a - b)</c>.
	/// </summary>
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (RemoveParentheses(context.Left.Syntax) is not BinaryExpressionSyntax sub || !sub.IsKind(SyntaxKind.SubtractExpression))
		{
			return false;
		}

		var c = RemoveParentheses(context.Right.Syntax);

		// c is emitted into the fma multiply and reused as the addend, so only duplicate leaves.
		if (c is not (IdentifierNameSyntax or LiteralExpressionSyntax or MemberAccessExpressionSyntax))
		{
			return false;
		}

		var useEstimate = ContainsMultiplyAddEstimate(context.Type);

		if (!useEstimate && !ContainsFusedMultiplyAdd(context.Type))
		{
			return false;
		}

		var a = RemoveParentheses(sub.Left);
		var b = RemoveParentheses(sub.Right);

		ArgumentListSyntax arguments;

		if (context.TryGetValue(a, out var aValue) && IsOne(aValue))
		{
			// (1 - b) * c == -b*c + c => FMA(-b, c, c)
			arguments = ArgumentList(SeparatedList([ Argument(Negate(b)), Argument(c), Argument(c) ]));
		}
		else if (context.TryGetValue(b, out var bValue) && IsOne(bValue))
		{
			// (a - 1) * c == a*c - c => FMA(a, c, -c)
			arguments = ArgumentList(SeparatedList([ Argument(a), Argument(c), Argument(UnaryMinusExpression(c)) ]));
		}
		else
		{
			return false;
		}

		var host = ParseName(context.Type.Name);

		optimized = InvocationExpression(
			MemberAccessExpression(host, IdentifierName(useEstimate ? "MultiplyAddEstimate" : "FusedMultiplyAdd")),
			arguments);

		return true;
	}

	private static ExpressionSyntax Negate(ExpressionSyntax expr)
	{
		return expr is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax
			? UnaryMinusExpression(ParenthesizedExpression(expr))
			: UnaryMinusExpression(expr);
	}

	private static bool IsOne(object? value)
	{
		return value switch
		{
			byte v => v == 1,
			sbyte v => v == 1,
			short v => v == 1,
			ushort v => v == 1,
			int v => v == 1,
			uint v => v == 1,
			long v => v == 1,
			ulong v => v == 1,
			float v => v == 1f,
			double v => v == 1d,
			decimal v => v == 1m,
			_ => false
		};
	}

	private static bool ContainsMultiplyAddEstimate(ITypeSymbol type)
	{
		return type.HasMethod("MultiplyAddEstimate", m =>
			m.Parameters.Length == 3 &&
			m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, type)));
	}

	private static bool ContainsFusedMultiplyAdd(ITypeSymbol type)
	{
		return type.HasMethod("FusedMultiplyAdd", m =>
			m.Parameters.Length == 3 &&
			m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, type)));
	}
}