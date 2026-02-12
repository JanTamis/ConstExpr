using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

	private readonly Lazy<BaseLinqFunctionOptimizer[]> _linqOptimizers = new(() =>
	{
		return typeof(BaseLinqFunctionOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseLinqFunctionOptimizer).IsAssignableFrom(t))
			.Select(Activator.CreateInstance)
			.OfType<BaseLinqFunctionOptimizer>()
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
		if (!variables.TryGetValue(node.Identifier.Text, out var variable))
		{
			return node;
		}

		// If variable has a known constant value and hasn't been altered, inline it
		if (variable.HasValue && !variable.IsAltered)
		{
			// Try to convert to a literal
			if (TryGetLiteral(variable.Value, out var literal))
			{
				return literal;
			}

			// If the value is another identifier pointing to an altered variable, keep original
			if (variable.Value is IdentifierNameSyntax nestedId
			    && variables.TryGetValue(nestedId.Identifier.Text, out var nestedVar)
			    && nestedVar.IsAltered)
			{
				return node;
			}

			// Inline the syntax node value
			return variable.Value as SyntaxNode ?? node;
		}

		if (variable is { HasValue: true, Value: SyntaxNode variableNode })
		{
			return variableNode;
		}

		return node;
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

	#endregion
}