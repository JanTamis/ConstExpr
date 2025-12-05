using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Rewriter that performs constant folding and safe partial evaluation over C# syntax trees.
/// This class is split across multiple partial files for better organization:
/// - ConstExprPartialRewriter.cs (this file): Core class definition, constructor, and base overrides
/// - ConstExprPartialRewriter.Expressions.cs: Expression visitors (binary, unary, literal, etc.)
/// - ConstExprPartialRewriter.Statements.cs: Statement visitors (if, for, while, etc.)
/// - ConstExprPartialRewriter.Invocations.cs: Method invocations and member access
/// - ConstExprPartialRewriter.Declarations.cs: Variable declarations and assignments
/// - ConstExprPartialRewriter.Patterns.cs: Pattern matching (switch, is-pattern)
/// - ConstExprPartialRewriter.Lambda.cs: Lambda expressions
/// - ConstExprPartialRewriter.Misc.cs: Object creation and list visiting
/// - ConstExprPartialRewriter.Helpers.cs: Helper methods for conversions and optimizations
/// </summary>
public partial class ConstExprPartialRewriter(
	SemanticModel semanticModel,
	MetadataLoader loader,
	Action<SyntaxNode?, Exception> exceptionHandler,
	IDictionary<string, VariableItem> variables,
	IDictionary<SyntaxNode, bool> additionalMethods,
	ISet<string> usings,
	ConstExprAttribute attribute,
	CancellationToken token,
	HashSet<IMethodSymbol>? visitingMethods = null)
	: BaseRewriter(semanticModel, loader, variables)
{
	#region Fields and Lazy Initializers

	private readonly Lazy<Type[]> _stringOptimizers = new(() =>
	{
		return typeof(BaseStringFunctionOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseStringFunctionOptimizer).IsAssignableFrom(t))
			.ToArray();
	}, isThreadSafe: true);

	private readonly Lazy<BaseMathFunctionOptimizer[]> _mathOptimizers = new(() =>
	{
		return typeof(BaseMathFunctionOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseMathFunctionOptimizer).IsAssignableFrom(t))
			.Select(t => Activator.CreateInstance(t) as BaseMathFunctionOptimizer)
			.OfType<BaseMathFunctionOptimizer>()
			.ToArray();
	}, isThreadSafe: true);

	#endregion

	#region Base Visit Overrides

	[return: NotNullIfNotNull(nameof(node))]
	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception e)
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
		if (variables.TryGetValue(node.Identifier.Text, out var value))
		{
			if (value.HasValue && !value.IsAltered)
			{
				if (TryGetLiteral(value.Value, out var expression))
				{
					// Variable is inlined to a literal, so it's not "accessed" in the generated code
					return expression;
				}

				if (value.Value is IdentifierNameSyntax identifier
				    && variables.TryGetValue(identifier.Identifier.Text, out var nestedValue)
				    && nestedValue is { IsAltered: true })
				{
					// Variable points to an altered variable, keep the original reference
					value.IsAccessed = true;
					return node;
				}

				// Variable is inlined to its value (which is a SyntaxNode)
				return value.Value as SyntaxNode;
			}
			
			// Variable doesn't have a known value or has been altered, mark as accessed
			value.IsAccessed = true;
		}

		return node;
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		// Special handling for increment/decrement expressions used as statements
		if (node.Expression is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)
		{
			var originalExpression = node.Expression;
			var result = Visit(originalExpression);

			return result switch
			{
				// If the result is a literal, preserve the original increment/decrement for side-effects
				LiteralExpressionSyntax => node,
				ExpressionSyntax expression => node.WithExpression(expression),
				_ => result
			};

		}

		var visitedResult = Visit(node.Expression);

		if (visitedResult is not ExpressionSyntax syntax)
		{
			if (visitedResult is null)
			{
				return node;
			}

			return visitedResult;
		}

		return node.WithExpression(syntax);
	}

	/// <summary>
	/// Visits an expression that may be an increment/decrement used for side-effects.
	/// </summary>
	private ExpressionSyntax VisitIncrementExpression(ExpressionSyntax expression)
	{
		if (expression is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)
		{
			var result = Visit(expression);

			switch (result)
			{
				case LiteralExpressionSyntax:
					return expression;
				case ExpressionSyntax expr:
					return expr;
			}

		}

		var visited = Visit(expression);
		return visited as ExpressionSyntax ?? expression;
	}

	#endregion
}