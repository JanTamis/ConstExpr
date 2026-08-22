using System.Collections.Generic;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
///   Guards against silently changing what a local declaration's type infers to once its
///   initializer has been rewritten by an optimizer.
/// </summary>
public static class VarDeclarationTypeGuard
{
	/// <summary>
	///   Strict form used where <c>var</c> is only ever introduced (no explicit type existed to
	///   preserve to begin with): safe only when the right-hand side provably infers to the exact
	///   type requested - a literal of that type, or an identifier already tracked as that type.
	///   Anything else (an unrecognised expression shape) is treated as unsafe.
	/// </summary>
	public static bool CanSafelyInferVar(TypeSyntax declaredType, ExpressionSyntax? rhs, IDictionary<string, VariableItem> variables)
	{
		if (rhs is null || !TryGetPredefinedSpecialType(declaredType, out var declaredSpecial))
		{
			return false;
		}

		var rhsSpecial = rhs switch
		{
			LiteralExpressionSyntax lit => LiteralSpecialType(lit),
			IdentifierNameSyntax id when variables.TryGetValue(id.Identifier.Text, out var v) => v.Type.SpecialType,
			_ => SpecialType.None
		};

		return rhsSpecial == declaredSpecial;
	}

	/// <summary>
	///   Permissive form used where an EXPLICIT declared type already exists and the question is
	///   whether collapsing it to <c>var</c> is safe. Erasing to <c>var</c> is safe unless it is
	///   provably dangerous: the local was declared floating-point (<c>double</c>/<c>float</c>/
	///   <c>decimal</c>) and the rewritten initializer can be shown to now produce an integral
	///   value - e.g. an optimizer retargeted <c>Math.Min(double,double)</c> to the narrower
	///   <c>Byte.Min(byte,byte)</c> overload. Without this check, <c>var min = Byte.Min(...)</c>
	///   silently infers <c>byte</c>, and a later division by <c>min</c> - intended as floating-point
	///   division - silently becomes integer division instead.
	///   <para>
	///     Any expression shape this can't positively classify (an arbitrary method call, an
	///     unrecognised operator, ...) is assumed SAFE, matching the erase-to-var behaviour every
	///     other declaration in this codebase already relies on: this check exists to catch the one
	///     specific hazardous direction, not to prove every declaration's type in general.
	///   </para>
	/// </summary>
	public static bool WouldNarrowFloatingToIntegral(TypeSyntax declaredType, ExpressionSyntax? rhs, IDictionary<string, VariableItem> variables)
	{
		if (rhs is null || !TryGetPredefinedSpecialType(declaredType, out var declaredSpecial) || !IsFloatingPoint(declaredSpecial))
		{
			return false;
		}

		return TryInferSpecialType(rhs, variables, out var rhsSpecial) && !IsFloatingPoint(rhsSpecial);
	}

	private static bool TryInferSpecialType(ExpressionSyntax expression, IDictionary<string, VariableItem> variables, out SpecialType specialType)
	{
		switch (expression)
		{
			case ParenthesizedExpressionSyntax paren:
				return TryInferSpecialType(paren.Expression, variables, out specialType);

			case LiteralExpressionSyntax lit:
				specialType = LiteralSpecialType(lit);
				return specialType != SpecialType.None;

			case IdentifierNameSyntax id when variables.TryGetValue(id.Identifier.Text, out var item):
				specialType = item.Type.SpecialType;
				return specialType != SpecialType.None;

			case CastExpressionSyntax cast:
				return TryGetPredefinedSpecialType(cast.Type, out specialType);

			case PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression or (int) SyntaxKind.UnaryPlusExpression } prefix:
				return TryInferSpecialType(prefix.Operand, variables, out specialType);

			case BinaryExpressionSyntax bin when bin.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression:

				if (TryInferSpecialType(bin.Left, variables, out var leftType) && TryInferSpecialType(bin.Right, variables, out var rightType))
				{
					specialType = CombineArithmetic(leftType, rightType);
					return true;
				}

				specialType = SpecialType.None;
				return false;

			case ConditionalExpressionSyntax cond:
				if (TryInferSpecialType(cond.WhenTrue, variables, out var whenTrueType) && TryInferSpecialType(cond.WhenFalse, variables, out var whenFalseType) && whenTrueType == whenFalseType)
				{
					specialType = whenTrueType;
					return true;
				}

				specialType = SpecialType.None;
				return false;

			// The optimizer retargets e.g. `Math.Min(double,double)` to the narrower `Byte.Min(byte,byte)`
			// by swapping the receiver to the BCL numeric type (BaseFunctionOptimizer.CreateInvocation
			// builds that receiver as a PredefinedTypeSyntax keyword, e.g. `byte`, not an identifier).
			// Recognising that shape is what lets us prove the result is now integral.
			case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: var receiver } }
				when TryGetReceiverSpecialType(receiver, out specialType):
				return true;

			default:
				specialType = SpecialType.None;
				return false;
		}
	}

	private static SpecialType CombineArithmetic(SpecialType left, SpecialType right)
	{
		if (left == SpecialType.System_Double || right == SpecialType.System_Double)
		{
			return SpecialType.System_Double;
		}

		if (left == SpecialType.System_Single || right == SpecialType.System_Single)
		{
			return SpecialType.System_Single;
		}

		if (left == SpecialType.System_Decimal || right == SpecialType.System_Decimal)
		{
			return SpecialType.System_Decimal;
		}

		return SpecialType.System_Int32;
	}

	private static bool IsFloatingPoint(SpecialType type)
	{
		return type is SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal;
	}

	private static bool TryGetReceiverSpecialType(ExpressionSyntax receiver, out SpecialType specialType)
	{
		if (receiver is PredefinedTypeSyntax predefined)
		{
			return TryGetPredefinedSpecialType(predefined, out specialType);
		}

		if (receiver is IdentifierNameSyntax id)
		{
			return TryGetSpecialTypeByPredefinedName(id.Identifier.Text, out specialType);
		}

		specialType = SpecialType.None;
		return false;
	}

	private static bool TryGetSpecialTypeByPredefinedName(string name, out SpecialType specialType)
	{
		specialType = name switch
		{
			"Byte" => SpecialType.System_Byte,
			"SByte" => SpecialType.System_SByte,
			"Int16" => SpecialType.System_Int16,
			"UInt16" => SpecialType.System_UInt16,
			"Int32" => SpecialType.System_Int32,
			"UInt32" => SpecialType.System_UInt32,
			"Int64" => SpecialType.System_Int64,
			"UInt64" => SpecialType.System_UInt64,
			"Single" => SpecialType.System_Single,
			"Double" => SpecialType.System_Double,
			"Decimal" => SpecialType.System_Decimal,
			_ => SpecialType.None
		};

		return specialType != SpecialType.None;
	}

	public static bool TryGetPredefinedSpecialType(TypeSyntax type, out SpecialType specialType)
	{
		specialType = type is PredefinedTypeSyntax predefined
			? predefined.Keyword.Kind() switch
			{
				SyntaxKind.DoubleKeyword => SpecialType.System_Double,
				SyntaxKind.FloatKeyword => SpecialType.System_Single,
				SyntaxKind.DecimalKeyword => SpecialType.System_Decimal,
				SyntaxKind.IntKeyword => SpecialType.System_Int32,
				SyntaxKind.UIntKeyword => SpecialType.System_UInt32,
				SyntaxKind.LongKeyword => SpecialType.System_Int64,
				SyntaxKind.ULongKeyword => SpecialType.System_UInt64,
				SyntaxKind.ShortKeyword => SpecialType.System_Int16,
				SyntaxKind.UShortKeyword => SpecialType.System_UInt16,
				SyntaxKind.ByteKeyword => SpecialType.System_Byte,
				SyntaxKind.SByteKeyword => SpecialType.System_SByte,
				SyntaxKind.BoolKeyword => SpecialType.System_Boolean,
				SyntaxKind.CharKeyword => SpecialType.System_Char,
				SyntaxKind.StringKeyword => SpecialType.System_String,
				_ => SpecialType.None
			}
			: SpecialType.None;

		return specialType != SpecialType.None;
	}

	public static SpecialType LiteralSpecialType(LiteralExpressionSyntax literal)
	{
		return literal.Token.Value switch
		{
			double => SpecialType.System_Double,
			float => SpecialType.System_Single,
			decimal => SpecialType.System_Decimal,
			int => SpecialType.System_Int32,
			uint => SpecialType.System_UInt32,
			long => SpecialType.System_Int64,
			ulong => SpecialType.System_UInt64,
			byte => SpecialType.System_Byte,
			sbyte => SpecialType.System_SByte,
			short => SpecialType.System_Int16,
			ushort => SpecialType.System_UInt16,
			char => SpecialType.System_Char,
			bool => SpecialType.System_Boolean,
			string => SpecialType.System_String,
			_ => SpecialType.None
		};
	}
}