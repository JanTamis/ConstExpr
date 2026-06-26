using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
		if (variable.HasValue && !variable.IsAltered && !variable.HasUnknownElements)
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

		return result switch
		{
			// For increment/decrement that evaluate to literals, keep original for side-effects
			LiteralExpressionSyntax when node.Expression is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax => node,
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