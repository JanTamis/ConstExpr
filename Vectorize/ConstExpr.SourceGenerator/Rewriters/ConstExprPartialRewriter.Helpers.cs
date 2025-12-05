using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Helper partial containing small focused helpers used by ConstExprPartialRewriter.
/// Keeping these in a separate file improves readability of the main visitor logic.
/// </summary>
public partial class ConstExprPartialRewriter
{
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
	/// </summary>
	private bool TryOptimizeNode(BinaryOperatorKind kind, ITypeSymbol type, ExpressionSyntax leftExpr, ITypeSymbol? leftType, ExpressionSyntax rightExpr, ITypeSymbol? rightType, out SyntaxNode? syntaxNode)
	{
		// Select optimizer based on operator kind
		var strategies = typeof(BaseBinaryOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseBinaryOptimizer).IsAssignableFrom(t))
			.Select(t => Activator.CreateInstance(t) as BaseBinaryOptimizer)
			.OfType<BaseBinaryOptimizer>()
			.Where(o => o.Kind == kind)
			.SelectMany(s => s.GetStrategies());

		var context = new BinaryOptimizeContext
		{
			Left = new BinaryOptimizeElement
			{
				HasValue = TryGetLiteralValue(leftExpr, out var leftVal),
				Value = leftVal,
				Type = leftType,
				Syntax = leftExpr,
			},
			Right = new BinaryOptimizeElement
			{
				HasValue = TryGetLiteralValue(rightExpr, out var rightVal),
				Value = rightVal,
				Type = rightType,
				Syntax = rightExpr,
			},
			Type = type,
			Variables = variables,
			Kind = kind,
			TryGetLiteral = TryGetLiteralValue
		};

		foreach (var strategy in strategies)
		{
			if (strategy.CanBeOptimized(context))
			{
				var result = strategy.Optimize(context);

				if (result is not null)
				{
					if (result is BinaryExpressionSyntax binary
					    && TryOptimizeNode(binary.Kind().ToBinaryOperatorKind(), type, binary.Left, leftType, binary.Right, rightType, out var nested))
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
	private IEnumerable<string> AssignedVariables(SyntaxNode? block)
	{
		if (block == null)
		{
			return [ ];
		}

		var data = semanticModel.AnalyzeDataFlow(block, block);

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
}