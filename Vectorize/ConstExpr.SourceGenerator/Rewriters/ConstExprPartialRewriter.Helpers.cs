using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
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
	private static readonly FrozenDictionary<BinaryOperatorKind, BaseBinaryOptimizer> _binaryOptimizers = typeof(BaseBinaryOptimizer).Assembly
		.GetTypes()
		.Where(t => !t.IsAbstract && typeof(BaseBinaryOptimizer).IsAssignableFrom(t))
		.Select(t => Activator.CreateInstance(t) as BaseBinaryOptimizer)
		.OfType<BaseBinaryOptimizer>()
		.ToFrozenDictionary(t => t.Kind);

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
				parent
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

	private static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax or LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			ElementAccessExpressionSyntax e => IsPure(e.Expression) && e.ArgumentList.Arguments.All(a => IsPure(a.Expression)),
			_ => false
		};
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