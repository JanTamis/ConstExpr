using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Rewriter that performs constant folding and safe partial evaluation over C# syntax trees.
///   This class is split across multiple partial files for better organization:
///   - ConstExprPartialRewriter.cs (this file): Core class definition, constructor, and base overrides
///   - ConstExprPartialRewriter.Expressions.cs: Expression visitors (binary, unary, literal, etc.)
///   - ConstExprPartialRewriter.Statements.cs: Statement visitors (if, for, while, etc.)
///   - ConstExprPartialRewriter.Invocations.cs: Method invocations and member access
///   - ConstExprPartialRewriter.Declarations.cs: Variable declarations and assignments
///   - ConstExprPartialRewriter.Patterns.cs: Pattern matching (switch, is-pattern)
///   - ConstExprPartialRewriter.Lambda.cs: Lambda expressions
///   - ConstExprPartialRewriter.Misc.cs: Object creation and list visiting
///   - ConstExprPartialRewriter.Helpers.cs: Helper methods for conversions and optimizations
/// </summary>
public partial class ConstExprPartialRewriter(
	SemanticModel semanticModel,
	MetadataLoader loader,
	Action<SyntaxNode?, Exception> exceptionHandler,
	IDictionary<string, VariableItem> variables,
	IDictionary<SyntaxNode, bool> additionalMethods,
	ISet<string> usings,
	ConstExprAttribute attribute,
	ConcurrentDictionary<ulong, ISymbol> symbolStore,
	CancellationToken token,
	HashSet<IMethodSymbol>? visitingMethods = null)
	: BaseRewriter(semanticModel, loader, variables, symbolStore)
{
	#region Fields and Lazy Initializers

	private static readonly BaseMathFunctionOptimizer[] _mathOptimizers = OptimizerRegistry.MathOptimizers;
	private static readonly BaseLinqFunctionOptimizer[] _linqOptimizers = OptimizerRegistry.LinqOptimizers;
	private static readonly BaseSimdFunctionOptimizer[] _simdOptimizers = OptimizerRegistry.SimdOptimizers;
	private static readonly BaseRegexFunctionOptimizer[] _regexOptimizers = OptimizerRegistry.RegexOptimizers;

	private Dictionary<SyntaxNode, SyntaxNode?>? _visitMemo;
	private long _visitMemoFingerprint;
	private long _mutationTicks;

	#endregion

	#region Base Visit Overrides

	[return: NotNullIfNotNull(nameof(node))]
	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception e) when (node is not LiteralExpressionSyntax)
		{
			exceptionHandler(node, e);
			return node;
		}
	}

	/// <summary>
	///   Visits <paramref name="node" /> the way <see cref="Visit" /> does, but returns the previous result
	///   when this exact node instance was already visited under the same variable state.
	///   <para>
	///     A single invocation is handled by walking its receiver up to four times: once speculatively in
	///     <c>TryExecuteInstanceMethod</c> (whose visited node is discarded - only "did it fold to a literal"
	///     is read off it), once in the LINQ optimizer's <c>TryExecutePredicates</c>, once more in that same
	///     optimizer's <c>UpdateInvocation</c>, and finally when the invocation's own expression is rebuilt.
	///     Because every one of those re-entries lands back in <c>VisitInvocationExpression</c> for the next
	///     link, the cost of a LINQ chain was exponential in its length - measured at ~88% of all rewriter
	///     time on the LINQ test corpus, against ~11% for the one walk that actually produces the result.
	///   </para>
	///   <para>
	///     Reuse is gated on <see cref="GetVariableStateFingerprint" />: syntax nodes are immutable, so the
	///     only thing that can make the same node visit to a different result is the rewriter's own mutable
	///     state - the tracked variables, the in-progress method set, and anything mutated in place through
	///     reflection (which the sites doing it flag with <see cref="MarkStateMutated" />). A visit that
	///     changes that state itself is deliberately not cached, and drops the whole memo.
	///   </para>
	/// </summary>
	private SyntaxNode? VisitMemoized(SyntaxNode? node)
	{
		if (node is null)
		{
			return null;
		}

		var fingerprint = GetVariableStateFingerprint();

		if (_visitMemo is not null
		    && _visitMemoFingerprint == fingerprint
		    && _visitMemo.TryGetValue(node, out var cached))
		{
			// Returned as-is, null included: a null result is a real answer here (a void call or a local
			// function that folds away entirely), not a "nothing cached" sentinel.
			return cached;
		}

		var result = Visit(node);

		// The visit mutated something a later visit of the same node would read - caching it would hand
		// out a result computed against state that no longer exists.
		if (GetVariableStateFingerprint() != fingerprint)
		{
			_visitMemo = null;
			return result;
		}

		if (_visitMemo is null || _visitMemoFingerprint != fingerprint)
		{
			_visitMemo = new Dictionary<SyntaxNode, SyntaxNode?>(NodeReferenceComparer.Instance);
			_visitMemoFingerprint = fingerprint;
		}

		_visitMemo[node] = result;
		return result;
	}

	/// <summary>
	///   Everything a <see cref="Visit" /> result can depend on besides the (immutable) node itself.
	///   <see cref="VariableItem.Value" /> is compared by reference: a value replaced by a fold produces a
	///   new object, while a value mutated in place through reflection does not - which is why
	///   <see cref="_mutationTicks" /> is folded in and bumped at the reflective-execution sites.
	///   <see cref="VariableItem.IsAccessed" /> is left out on purpose; it records that a read happened and
	///   never feeds back into what a node folds to.
	/// </summary>
	private long GetVariableStateFingerprint()
	{
		// visitingMethods is in here because HandleStaticMethodInvocation reads it as a recursion guard:
		// the same call folds to an inlined body or is left alone depending on whether its target is
		// currently on the stack. It only grows and shrinks along one recursion path, so the count
		// separates the states.
		var hash = _mutationTicks * 1000003L + variables.Count * 7L + (visitingMethods?.Count ?? 0);

		foreach (var pair in variables)
		{
			var variable = pair.Value;

			hash = hash * 31 + StringComparer.Ordinal.GetHashCode(pair.Key);
			hash = hash * 31 + RuntimeHelpers.GetHashCode(variable.Value);
			hash = hash * 31 + (variable.HasValue ? 1 : 0)
			                 + (variable.IsAltered ? 2 : 0)
			                 + (variable.CanBeInlined ? 4 : 0)
			                 + (variable.IsInitialized ? 8 : 0);
			hash = hash * 31 + (variable.UnknownIndices?.Count ?? 0);
		}

		return hash;
	}

	/// <summary>
	///   Marks tracked state as changed in a way <see cref="GetVariableStateFingerprint" /> cannot observe -
	///   an object held in <see cref="VariableItem.Value" /> that was mutated in place rather than replaced.
	/// </summary>
	private void MarkStateMutated()
	{
		_mutationTicks++;
		_visitMemo = null;
	}

	/// <summary>
	///   netstandard2.0 has no <c>ReferenceEqualityComparer</c>. Reference identity is the point here:
	///   two structurally equal nodes from different places in the tree can legitimately fold differently.
	/// </summary>
	private sealed class NodeReferenceComparer : IEqualityComparer<SyntaxNode>
	{
		public static readonly NodeReferenceComparer Instance = new();

		public bool Equals(SyntaxNode? x, SyntaxNode? y)
		{
			return ReferenceEquals(x, y);
		}

		public int GetHashCode(SyntaxNode obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		return null;
	}

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (!variables.TryGetValue(node.Identifier.Text, out var variable))
		{
			return node;
		}

		if (ShouldPreserveIdentifier(node))
		{
			variable.IsAltered = true;
			return node.WithTypeSymbolAnnotation(variable.Type, symbolStore);
		}

		// For inlinable variables with expression values, try to get constant value first for const variables
		if (variable is { CanBeInlined: true, Value: ExpressionSyntax expr })
		{
			// Try to evaluate const expressions to get their constant values
			if (variable.HasValue && TryCreateLiteral(variable.Value, out var literal))
			{
				return literal;
			}

			var result = ParenthesizedExpression(expr);
			var parent = node.Parent;

			if (parent is ArgumentSyntax argument)
			{
				parent = argument.Parent;
			}

			if (result.CanRemoveParentheses(parent, semanticModel, CancellationToken.None))
			{
				return result.Expression;
			}

			return result;
		}

		// If variable has a known constant value and hasn't been altered, inline it.
		// HasUnknownElements blocks inlining a partially runtime-written array as a whole literal.
		if (variable.HasValue && variable is { IsAltered: false, HasUnknownElements: false })
		{
			// Try to convert to a literal
			if (TryCreateLiteral(variable.Value, out var literal))
			{
				return literal;
			}

			// If the value is another identifier, keep original when:
			// - the referenced variable was altered (would produce stale value), or
			// - the referenced variable has no concrete value (propagating an unknown alias adds no information)
			if (variable.Value is IdentifierNameSyntax nestedId
			    && variables.TryGetValue(nestedId.Identifier.Text, out var nestedVar)
			    && (nestedVar.IsAltered || !nestedVar.HasValue))
			{
				return node;
			}

			// Inline the syntax node value
			return variable.Value as SyntaxNode ?? node;
		}

		if (variable is { Value: SyntaxNode syntax, HasValue: true })
		{
			return syntax;
		}

		// if (variable is { Value: ExpressionSyntax expr, IsAltered: false, CanBeInlined: true } && CanBeInlined(expr))
		// {
		// 	var result = ParenthesizedExpression(expr);
		// 	var parent = node.Parent;
		//
		// 	if (parent is ArgumentSyntax)
		// 	{
		// 		parent = parent.Parent;
		// 	}
		//
		// 	if (result.CanRemoveParentheses(parent, semanticModel, CancellationToken.None))
		// 	{
		// 		return Visit(result.Expression.WithTypeSymbolAnnotation(variable.Type, symbolStore));
		// 	}
		//
		// 	return Visit(result).WithTypeSymbolAnnotation(variable.Type, symbolStore);
		// }

		return node.WithTypeSymbolAnnotation(variable.Type, symbolStore);
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		// a ??= b; as a standalone statement, when a is provably non-null: VisitAssignmentExpression
		// already folded the coalesce-assignment down to the bare target in that case (result stops
		// being an AssignmentExpressionSyntax). A bare identifier expression statement (`a;`) isn't
		// legal C# (CS0201: only assignment/call/increment/decrement/await/new-object expressions can
		// be a statement), so the whole statement becomes a no-op instead.
		if (node.Expression is AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.CoalesceAssignmentExpression } && result is not AssignmentExpressionSyntax)
		{
			return EmptyStatement();
		}

		return result switch
		{
			// For increment/decrement of a plain (possibly symbolic/unknown) variable that evaluate
			// to literals, keep the original syntax so the real runtime increment still happens.
			// Element-access increments (e.g. counts[c]++) that fold this far are on a fully
			// tracked array, so the literal placeholder is safe — DeadCodePruner removes it.
			LiteralExpressionSyntax when node.Expression is PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax } or PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax } => node,
			ExpressionSyntax expr => node.WithExpression(expr),
			_ => result
		};
	}

	private static bool ShouldPreserveIdentifier(IdentifierNameSyntax node)
	{
		return node.Parent switch
		{
			ElementAccessExpressionSyntax { Expression: var expression } elementAccess when expression == node => IsWritableStorageAccess(elementAccess),
			MemberAccessExpressionSyntax { Expression: var expression } memberAccess when expression == node => IsWritableStorageAccess(memberAccess),
			_ => false
		};
	}

	private static bool IsWritableStorageAccess(ExpressionSyntax access)
	{
		SyntaxNode current = access;

		while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
		{
			current = parenthesized;
		}

		return current.Parent switch
		{
			AssignmentExpressionSyntax assignment when assignment.Left == current => true,
			PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression) => true,
			PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression) => true,
			ArgumentSyntax { RefKindKeyword.RawKind: not 0 } => true,
			_ => false
		};
	}

	#endregion
}