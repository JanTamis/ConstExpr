using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Helper partial containing small focused helpers used by ConstExprPartialRewriter.
/// Keeping these in a separate file improves readability of the main visitor logic.
/// </summary>
public partial class ConstExprPartialRewriter
{
	private static readonly FrozenDictionary<BinaryOperatorKind, BaseBinaryOptimizer> _binaryOptimizers = OptimizerRegistry.BinaryOptimizers;

	/// <summary>
	/// Strips unnecessary outer parentheses from an expression.
	/// Recursively unwraps parentheses when the inner expression doesn't require them.
	/// </summary>
	private static SyntaxNode? StripUnnecessaryParentheses(SyntaxNode? node)
	{
		while (node is ParenthesizedExpressionSyntax paren)
		{
			var inner = paren.Expression;

			switch (inner)
			{
				// These expression types never need parentheses
				case IdentifierNameSyntax
					or LiteralExpressionSyntax
					or InvocationExpressionSyntax
					or ObjectCreationExpressionSyntax
					or MemberAccessExpressionSyntax
					or ElementAccessExpressionSyntax
					or InterpolatedStringExpressionSyntax
					or ParenthesizedExpressionSyntax
					or BinaryExpressionSyntax:
				{
					node = inner;
					continue;
				}
			}

			// Keep parentheses for other expression types
			break;
		}

		return node;
	}

	private static bool IsExpressionPure(SyntaxNode node) => node switch
	{
		IdentifierNameSyntax or LiteralExpressionSyntax => true,
		ParenthesizedExpressionSyntax par => IsExpressionPure(par.Expression),
		PrefixUnaryExpressionSyntax u => IsExpressionPure(u.Operand),
		BinaryExpressionSyntax b => IsExpressionPure(b.Left) && IsExpressionPure(b.Right),
		MemberAccessExpressionSyntax m => IsExpressionPure(m.Expression),
		CastExpressionSyntax c => IsExpressionPure(c.Expression),
		_ => false
	};

	/// <summary>
	/// Returns <see langword="true"/> for the built-in two's-complement integer types.
	/// Used to guard rewrites that rely on the identity <c>~y == -y - 1</c>, which only holds
	/// for these types (never for a user type with overloaded <c>operator ~</c>/<c>operator -</c>).
	/// </summary>
	private static bool IsBuiltInInteger(SpecialType type) => type switch
	{
		SpecialType.System_SByte or SpecialType.System_Byte
			or SpecialType.System_Int16 or SpecialType.System_UInt16
			or SpecialType.System_Int32 or SpecialType.System_UInt32
			or SpecialType.System_Int64 or SpecialType.System_UInt64
			or SpecialType.System_IntPtr or SpecialType.System_UIntPtr => true,
		_ => false
	};

	/// <summary>
	/// Returns <see langword="true"/> for primary expressions that never require parentheses
	/// when placed as the operand of a unary <c>-</c> or either side of a binary operator.
	/// Used to keep negation-distribution rewrites precedence-safe without inserting parens.
	/// </summary>
	private static bool IsSimpleOperand(ExpressionSyntax e) => e is
		IdentifierNameSyntax or LiteralExpressionSyntax or MemberAccessExpressionSyntax
		or ElementAccessExpressionSyntax or InvocationExpressionSyntax or ParenthesizedExpressionSyntax;

	/// <summary>
	/// Signed native-width integers where unary <c>-</c> is legal and does not widen the operand,
	/// so two's-complement rewrites such as <c>~(x - 1) => -x</c> stay in the same arithmetic domain.
	/// Deliberately excludes <c>byte/short/uint/ulong/nuint</c>: small unsigned/signed types promote
	/// to <c>int</c> and the wide unsigned types either wrap (<c>uint</c>) or have no unary minus (<c>ulong</c>).
	/// </summary>
	private static bool IsSignedNativeInteger(SpecialType type) => type
		is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_IntPtr;

	/// <summary>
	/// Guards the two's-complement rewrites (<c>~(-x)</c>, <c>-(~x)</c>, <c>~(x ± 1)</c>): the surviving
	/// operand and the whole expression must be the same signed native integer type. This rejects
	/// unsigned operands (wrapping / no unary minus) and wider-literal cases (e.g. <c>~(intVar - 1L)</c>)
	/// that would silently diverge from the original.
	/// </summary>
	private bool IsTwosComplementSafe(ExpressionSyntax operand, ExpressionSyntax node)
	{
		return semanticModel.TryGetTypeSymbol(operand, symbolStore, out var operandType)
		       && IsSignedNativeInteger(operandType.SpecialType)
		       && semanticModel.TryGetTypeSymbol(node, symbolStore, out var nodeType)
		       && operandType.SpecialType == nodeType.SpecialType;
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="value"/> is an integral constant equal to 1.
	/// </summary>
	private static bool IsIntegralOne(object? value) => value switch
	{
		sbyte v => v == 1,
		byte v => v == 1,
		short v => v == 1,
		ushort v => v == 1,
		int v => v == 1,
		uint v => v == 1,
		long v => v == 1,
		ulong v => v == 1,
		_ => false
	};

	/// <summary>
	/// Execute a Roslyn conversion operation either via operator method or basic Convert.*
	/// Returns a runtime value that can be turned into a literal by CreateLiteral/TryGetLiteral.
	/// </summary>
	private object? ExecuteConversion(IConversionOperation conversion, object? value)
	{
		// If there's a conversion method, use it and produce a literal syntax node
		if (loader.TryExecuteMethod(conversion.OperatorMethod, null, new VariableItemDictionary(variables), [ value ], out var result))
		{
			return result;
		}

		// Convert the runtime value to the requested special type, then create a literal syntax node
		return conversion.Type?.SpecialType switch
		{
			SpecialType.System_Boolean => Convert.ToBoolean(value),
			SpecialType.System_Byte => Convert.ToByte(value),
			SpecialType.System_Char => Convert.ToChar(value),
			SpecialType.System_DateTime => Convert.ToDateTime(value),
			SpecialType.System_Decimal => Convert.ToDecimal(value),
			SpecialType.System_Double => Convert.ToDouble(value),
			SpecialType.System_Int16 => Convert.ToInt16(value),
			SpecialType.System_Int32 => Convert.ToInt32(value),
			SpecialType.System_Int64 => Convert.ToInt64(value),
			SpecialType.System_SByte => Convert.ToSByte(value),
			SpecialType.System_Single => Convert.ToSingle(value),
			SpecialType.System_String => Convert.ToString(value),
			SpecialType.System_UInt16 => Convert.ToUInt16(value),
			SpecialType.System_UInt32 => Convert.ToUInt32(value),
			SpecialType.System_UInt64 => Convert.ToUInt64(value),
			_ => value,
		};
	}

	/// <summary>
	/// Normalizes a sequence of visited nodes into a single statement or a block.
	/// </summary>
	private StatementSyntax ToStatementSyntax(IEnumerable<SyntaxNode?> nodes)
	{
		var items = nodes
			.SelectMany<SyntaxNode?, SyntaxNode?>(s => s is BlockSyntax block ? block.Statements : new[] { s })
			.OfType<StatementSyntax>()
			.ToList();

		if (items.Count == 1)
		{
			return items[0];
		}

		return Block(items);
	}

	/// <summary>
	/// Try to apply registered binary optimization strategies for the given operator and operands.
	/// Only strategies whose <see cref="IBinaryStrategy.RequiredFlags"/> are satisfied by the
	/// current <see cref="attribute"/> <c>MathOptimizations</c> flags are considered.
	/// </summary>
	private bool TryOptimizeNode(BinaryOperatorKind kind, List<BinaryExpressionSyntax> expressions, ITypeSymbol? type, ExpressionSyntax leftExpr, ITypeSymbol? leftType, ExpressionSyntax rightExpr, ITypeSymbol? rightType, SyntaxNode? parent, [NotNullWhen(true)] out SyntaxNode? syntaxNode)
	{
		// if (depth > MaxOptimizeNodeDepth)
		// {
		// 	syntaxNode = null;
		// 	return false;
		// }

		if (_binaryOptimizers.TryGetValue(kind, out var optimizer))
		{
			foreach (var strategy in optimizer.GetStrategies())
			{
				// Skip strategy if its required flags are not satisfied by the current math mode.
				// FastMathFlags.Strict (= 0) is always satisfied, so integer/boolean strategies run unconditionally.
				var reqFlags = strategy.RequiredFlags;

				if (!reqFlags.Contains(FastMathFlags.Strict) && !reqFlags.Any(f => attribute.MathOptimizations.HasFlag(f)))
				{
					continue;
				}

				if (TryOptimizeWithStrategy(strategy, expressions, type, leftExpr, leftType, rightExpr, rightType, parent, out var result)
				    && result != null)
				{
					if (result is BinaryExpressionSyntax binary
					    && TryOptimizeNode(binary.Kind().ToBinaryOperatorKind(), expressions, type, binary.Left, leftType, binary.Right, rightType, parent, out var nested))
					{
						syntaxNode = nested;
						return true;
					}

					syntaxNode = result;
					return true;
				}
			}
		}

		syntaxNode = null;
		return false;
	}

	/// <summary>
	/// Tries to optimize using a specific strategy by invoking GetContext and TryOptimize via reflection.
	/// </summary>
	private bool TryOptimizeWithStrategy(IBinaryStrategy strategy, List<BinaryExpressionSyntax> expressions, ITypeSymbol? type, ExpressionSyntax leftExpr, ITypeSymbol? leftType, ExpressionSyntax rightExpr, ITypeSymbol? rightType, SyntaxNode? parent, out ExpressionSyntax? result)
	{
		result = null;

		try
		{
			var strategyType = strategy.GetType();

			// Find GetContext method
			var getContextMethod = strategyType.GetMethod(nameof(BaseBinaryStrategy.GetContext));

			if (getContextMethod is null)
			{
				return false;
			}

			// Call GetContext with all required parameters
			var context = getContextMethod.Invoke(strategy,
			[
				expressions,
				type,
				leftExpr,
				leftType,
				rightExpr,
				rightType,
				variables,
				(TryGetValueDelegate) TryGetLiteralValue,
				parent,
				semanticModel,
				symbolStore
			]);

			if (context is null)
			{
				return false;
			}

			// Find TryOptimize method
			var tryOptimizeMethod = strategyType.GetMethod(nameof(BaseBinaryStrategy.TryOptimize));

			if (tryOptimizeMethod is null)
			{
				return false;
			}

			// Prepare parameters for TryOptimize (context, out result)
			var parameters = new[] { context, null };
			var success = tryOptimizeMethod.Invoke(strategy, parameters);

			if (success is true && parameters[1] is ExpressionSyntax optimized)
			{
				result = optimized;
				return true;
			}

			return false;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Checks if a method call with string arguments can be replaced with a char overload.
	/// </summary>
	private bool TryGetCharOverload(
		IMethodSymbol currentMethod,
		IReadOnlyList<SyntaxNode?> arguments,
		out IMethodSymbol? charMethod)
	{
		charMethod = null;

		// Check if any arguments are strings with length 1
		var stringIndices = new List<int>();
		var charValues = new List<char>();

		for (var i = 0; i < currentMethod.Parameters.Length && i < arguments.Count; i++)
		{
			var param = currentMethod.Parameters[i];

			// Only consider string parameters
			if (param.Type.SpecialType != SpecialType.System_String)
			{
				continue;
			}

			var arg = arguments[i];

			// Check if the argument is a string literal with length 1
			if (TryGetLiteralValue(arg, out var value) && value is string { Length: 1 } str)
			{
				stringIndices.Add(i);
				charValues.Add(str[0]);
			}
		}

		// If no single-char strings found, no optimization possible
		if (stringIndices.Count == 0)
		{
			return false;
		}

		// Look for an overload with char parameters at those positions
		var methodName = currentMethod.Name;
		var containingType = currentMethod.ContainingType;

		var candidateMethods = containingType
			.GetMembers(methodName)
			.OfType<IMethodSymbol>()
			.Where(m =>
				m.Parameters.Length == currentMethod.Parameters.Length &&
				m.IsStatic == currentMethod.IsStatic &&
				!SymbolEqualityComparer.Default.Equals(m, currentMethod));

		foreach (var candidate in candidateMethods)
		{
			var isMatch = true;
			var charIndicesInCandidate = new HashSet<int>();

			// Check if parameters match, with char at the identified positions
			for (var i = 0; i < candidate.Parameters.Length; i++)
			{
				var candidateParam = candidate.Parameters[i];
				var currentParam = currentMethod.Parameters[i];

				if (stringIndices.Contains(i))
				{
					// This position should be char in the candidate
					if (candidateParam.Type.SpecialType == SpecialType.System_Char)
					{
						charIndicesInCandidate.Add(i);
					}
					else
					{
						isMatch = false;
						break;
					}
				}
				else
				{
					// Other positions should have the same type
					if (!SymbolEqualityComparer.Default.Equals(candidateParam.Type, currentParam.Type))
					{
						isMatch = false;
						break;
					}
				}
			}

			// Check return type matches
			if (isMatch && !SymbolEqualityComparer.Default.Equals(candidate.ReturnType, currentMethod.ReturnType))
			{
				isMatch = false;
			}

			if (isMatch && charIndicesInCandidate.Count == stringIndices.Count)
			{
				// Found a matching char overload!
				charMethod = candidate;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the set of local/parameter names assigned within a given block node using Roslyn data flow.
	/// </summary>
	private IEnumerable<string> AssignedVariables(StatementSyntax? block)
	{
		if (block == null)
		{
			return [ ];
		}

		if (semanticModel.Compilation.TryGetSemanticModel(block, out var model))
		{
			var data = model.AnalyzeDataFlow(block, block);

			if (!data.Succeeded)
			{
				return [ ];
			}

			// Get all variables that are written to within the block
			var assignedVariables = data.WrittenInside
				.Where(symbol => symbol is ILocalSymbol or IParameterSymbol)
				.Select(symbol => symbol.Name);

			return assignedVariables;
		}

		return [ ];
	}

	/// <summary>
	/// Tries to get the compound assignment kind for a given binary expression kind.
	/// For example, LeftShiftExpression maps to LeftShiftAssignmentExpression.
	/// </summary>
	private static SyntaxKind TryGetCompoundAssignmentKind(SyntaxKind binaryKind)
	{
		return binaryKind switch
		{
			SyntaxKind.AddExpression => SyntaxKind.AddAssignmentExpression,
			SyntaxKind.SubtractExpression => SyntaxKind.SubtractAssignmentExpression,
			SyntaxKind.MultiplyExpression => SyntaxKind.MultiplyAssignmentExpression,
			SyntaxKind.DivideExpression => SyntaxKind.DivideAssignmentExpression,
			SyntaxKind.ModuloExpression => SyntaxKind.ModuloAssignmentExpression,
			SyntaxKind.LeftShiftExpression => SyntaxKind.LeftShiftAssignmentExpression,
			SyntaxKind.RightShiftExpression => SyntaxKind.RightShiftAssignmentExpression,
			SyntaxKind.BitwiseAndExpression => SyntaxKind.AndAssignmentExpression,
			SyntaxKind.BitwiseOrExpression => SyntaxKind.OrAssignmentExpression,
			SyntaxKind.ExclusiveOrExpression => SyntaxKind.ExclusiveOrAssignmentExpression,
			_ => SyntaxKind.None
		};
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="method"/> is a LINQ extension method
	/// declared on <see cref="System.Linq.Enumerable"/> or <see cref="System.Linq.Queryable"/>.
	/// </summary>
	private static bool IsLinqMethod(IMethodSymbol method)
	{
		var containingType = method.ContainingType;

		if (containingType is null)
		{
			return false;
		}

		var fullName = $"{containingType.ContainingNamespace}.{containingType.Name}";

		return fullName == "System.Linq.Enumerable";
	}

	private bool IsEmptyMethod(IMethodSymbol method)
	{
		if (!method.ReturnsVoid || method.IsAbstract)
		{
			return false;
		}

		var hasDeclaration = false;
		var allEmpty = true;

		foreach (var syntaxRef in method.DeclaringSyntaxReferences)
		{
			hasDeclaration = true;
			var syntax = syntaxRef.GetSyntax(token);

			switch (syntax)
			{
				case MethodDeclarationSyntax { Body: { } body }:
				{
					if (!IsBlockEffectivelyEmpty(body))
					{
						allEmpty = false;
					}
					break;
				}
				case MethodDeclarationSyntax { ExpressionBody: not null }:
				{
					return false;
				}
				case LocalFunctionStatementSyntax { Body: { } body }:
				{
					if (!IsBlockEffectivelyEmpty(body))
					{
						allEmpty = false;
					}
					break;
				}
				case LocalFunctionStatementSyntax { ExpressionBody: not null }:
				{
					return false;
				}
				default:
				{
					allEmpty = false;
					break;
				}
			}
		}

		return hasDeclaration && allEmpty;

		static bool IsBlockEffectivelyEmpty(BlockSyntax block)
		{
			if (block.Statements.Count == 0)
			{
				return true;
			}

			return block.Statements.All(static s => s switch
			{
				BlockSyntax { Statements.Count: 0 } => true,
				EmptyStatementSyntax => true,
				ReturnStatementSyntax { Expression: null } => true,
				_ => false
			});
		}
	}

	/// <summary>
	/// Snapshots the current value of every tracked variable so it can be restored later.
	/// </summary>
	private Dictionary<string, (bool HasValue, object? Value, bool IsAltered, bool IsInitialized)> SaveVariableState()
	{
		return variables.ToDictionary(
			kvp => kvp.Key,
			kvp => (kvp.Value.HasValue, kvp.Value.Value, kvp.Value.IsAltered, kvp.Value.IsInitialized));
	}

	/// <summary>
	/// Restores a previously saved variable snapshot, removing any variables that were
	/// introduced after the snapshot was taken.
	/// </summary>
	private void RestoreVariableState(Dictionary<string, (bool HasValue, object? Value, bool IsAltered, bool IsInitialized)> snapshot)
	{
		// Remove variables that were added since the snapshot
		foreach (var key in variables.Keys.Except(snapshot.Keys).ToList())
		{
			variables.Remove(key);
		}

		// Restore each variable to its snapshotted state
		foreach (var kvp in snapshot)
		{
			if (variables.TryGetValue(kvp.Key, out var variable))
			{
				variable.HasValue = kvp.Value.HasValue;
				variable.Value = kvp.Value.Value;
				variable.IsAltered = kvp.Value.IsAltered;
				variable.IsInitialized = kvp.Value.IsInitialized;
			}
		}
	}
}