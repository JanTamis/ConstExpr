using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Common Subexpression Elimination (CSE) by identifying repeated expressions
///   and replacing them with local variables.
/// </summary>
public sealed class CommonSubexpressionEliminator : CSharpSyntaxRewriter
{
	private int _cseCounter;
	private readonly HashSet<string> _usedNames = new();
	private static readonly IEqualityComparer<ExpressionSyntax> _comparer = new NormalizedExpressionComparer();

	private string GenerateName(ExpressionSyntax expr)
	{
		expr = Unparenthesize(expr);

		var baseName = expr switch
		{
			BinaryExpressionSyntax binary => binary.Kind() switch
			{
				SyntaxKind.AddExpression => "sum",
				SyntaxKind.SubtractExpression => "diff",
				SyntaxKind.MultiplyExpression => "prod",
				SyntaxKind.DivideExpression => "quot",
				SyntaxKind.ModuloExpression => "mod",
				SyntaxKind.LeftShiftExpression => "lshift",
				SyntaxKind.RightShiftExpression => "rshift",
				SyntaxKind.BitwiseAndExpression => "and",
				SyntaxKind.BitwiseOrExpression => "or",
				SyntaxKind.ExclusiveOrExpression => "xor",
				_ => "val"
			},
			InvocationExpressionSyntax invocation => invocation.Expression switch
			{
				IdentifierNameSyntax id => $"{id.Identifier.Text.ToLowerInvariant()}_val",
				MemberAccessExpressionSyntax ma => $"{ma.Name.Identifier.Text.ToLowerInvariant()}_val",
				_ => "call_val"
			},
			ElementAccessExpressionSyntax => "item",
			CastExpressionSyntax => "cast_val",
			_ => "val"
		};

		var name = baseName;
		var counter = 1;

		while (_usedNames.Contains(name))
		{
			name = $"{baseName}_{++counter}";
		}

		_usedNames.Add(name);
		return name;
	}

	/// <summary>
	///   Eliminates common subexpressions from the given syntax node.
	/// </summary>
	public static SyntaxNode? Eliminate(SyntaxNode? node)
	{
		if (node is null)
		{
			return null;
		}

		var eliminator = new CommonSubexpressionEliminator();

		return eliminator.Visit(node);
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// First, visit nested blocks to handle them in isolation (bottom-up)
		if (base.VisitBlock(node) is not BlockSyntax visitedNode)
		{
			return null;
		}

		// Reset names for this block context to avoid carrying over from unrelated blocks if the instance was reused
		// (though we create a new instance per Eliminate call, VisitBlock is recursive)

		var counts = new Dictionary<ExpressionSyntax, int>(_comparer);
		var lValues = new HashSet<ExpressionSyntax>(_comparer);
		var collector = new ExpressionCollector(counts, lValues);

		foreach (var statement in visitedNode.Statements)
		{
			collector.Visit(statement);
		}

		var candidates = counts.Where(kvp => kvp.Value > 1 && ShouldConsider(kvp.Key, lValues))
			.Select(kvp => kvp.Key)
			.OrderByDescending(c => c.DescendantNodes().Count()) // Prefer larger expressions first
			.ToList();

		if (candidates.Count == 0)
		{
			return visitedNode;
		}

		var newStatements = new List<StatementSyntax>();
		var replacementMap = new Dictionary<ExpressionSyntax, string>(_comparer);

		foreach (var statement in visitedNode.Statements)
		{
			var currentStatement = statement;

			// Identify which candidates appear in this statement for the first time
			foreach (var candidate in candidates)
			{
				if (replacementMap.ContainsKey(candidate))
				{
					continue;
				}

				if (ContainsExpression(currentStatement, candidate))
				{
					var name = GenerateName(candidate);
					replacementMap[candidate] = name;

					var declaration = LocalDeclarationStatement(
						VariableDeclaration(IdentifierName("var"))
							.WithVariables(SingletonSeparatedList(
								VariableDeclarator(Identifier(name))
									.WithInitializer(EqualsValueClause(Unparenthesize(candidate)))
							))
					);
					newStatements.Add(declaration);
				}
			}

			// Rewrite the statement using the current replacement map
			var rewriter = new ExpressionReplacementRewriter(replacementMap);
			newStatements.Add((StatementSyntax) rewriter.Visit(currentStatement));
		}

		return visitedNode.WithStatements(List(newStatements));
	}

	private static bool ContainsExpression(SyntaxNode root, ExpressionSyntax expression)
	{
		return root.DescendantNodesAndSelf(n => n is not BlockSyntax && n is not AnonymousFunctionExpressionSyntax)
			.OfType<ExpressionSyntax>()
			.Any(e => _comparer.Equals(e, expression));
	}

	private static bool ShouldConsider(ExpressionSyntax expr, HashSet<ExpressionSyntax> lValues)
	{
		expr = Unparenthesize(expr);

		// Expressions used as L-values cannot be CSE'd safely
		if (lValues.Contains(expr))
		{
			return false;
		}

		// If any identifier referenced by the expression is also mutated in this block,
		// the expression's value may change between occurrences and cannot be safely CSE'd.
		// e.g. in `positions = positions % 6; if (...) positions += 6; result[0] = ...[positions % 6]`
		// `positions % 6` appears twice but `positions` is mutated in between.
		if (expr.DescendantNodesAndSelf()
		    .OfType<IdentifierNameSyntax>()
		    .Any(id => lValues.Any(lv => lv is IdentifierNameSyntax lId && lId.Identifier.Text == id.Identifier.Text)))
		{
			return false;
		}

		// Only consider "expensive" or complex expressions
		if (expr is BinaryExpressionSyntax)
		{
			return true;
		}

		if (expr is InvocationExpressionSyntax invocation)
		{
			// Avoid CSE for expressions containing lambdas, as 'var' might fail to infer the delegate type
			return !invocation.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax);
		}

		if (expr is MemberAccessExpressionSyntax ma)
		{
			return ShouldConsider(ma.Expression, lValues);
		}

		if (expr is ElementAccessExpressionSyntax)
		{
			return true;
		}

		if (expr is CastExpressionSyntax cast)
		{
			return ShouldConsider(cast.Expression, lValues);
		}

		return false;
	}

	private static ExpressionSyntax Unparenthesize(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax p)
		{
			expr = p.Expression;
		}

		return expr;
	}

	private class NormalizedExpressionComparer : IEqualityComparer<ExpressionSyntax>
	{
		public bool Equals(ExpressionSyntax? x, ExpressionSyntax? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x == null || y == null)
			{
				return false;
			}

			return SyntaxNodeComparer.Get<ExpressionSyntax>().Equals(Unparenthesize(x), Unparenthesize(y));
		}

		public int GetHashCode(ExpressionSyntax obj)
		{
			return SyntaxNodeComparer.Get<ExpressionSyntax>().GetHashCode(Unparenthesize(obj));
		}
	}

	private class ExpressionCollector(Dictionary<ExpressionSyntax, int> counts, HashSet<ExpressionSyntax> lValues) : CSharpSyntaxWalker
	{
		public override void VisitBlock(BlockSyntax node)
		{
			/* Don't recurse into nested blocks */
		}

		public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) { }
		public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) { }
		public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) { }
		public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) { }

		public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
		{
			lValues.Add(Unparenthesize(node.Left));
			base.VisitAssignmentExpression(node);
		}

		public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
			}
			base.VisitPrefixUnaryExpression(node);
		}

		public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
			}
			base.VisitPostfixUnaryExpression(node);
		}

		public override void Visit(SyntaxNode? node)
		{
			if (node is ExpressionSyntax expr && node is not ParenthesizedExpressionSyntax)
			{
				var normalized = Unparenthesize(expr);
				counts.TryGetValue(normalized, out var count);
				counts[normalized] = count + 1;
			}

			base.Visit(node);
		}
	}

	private class ExpressionReplacementRewriter(Dictionary<ExpressionSyntax, string> replacementMap) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? Visit(SyntaxNode? node)
		{
			if (node is ExpressionSyntax expr)
			{
				// Do not replace if this is an L-value position
				if (IsLValue(expr))
				{
					return base.Visit(node);
				}

				if (replacementMap.TryGetValue(expr, out var name))
				{
					return IdentifierName(name).WithTriviaFrom(node);
				}
			}

			return base.Visit(node);
		}

		private static bool IsLValue(ExpressionSyntax node)
		{
			var current = node;

			while (current.Parent is ParenthesizedExpressionSyntax p)
			{
				current = p;
			}

			var parent = current.Parent;

			if (parent is AssignmentExpressionSyntax assignment && assignment.Left == current)
			{
				return true;
			}

			if (parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax)
			{
				if (parent.IsKind(SyntaxKind.PreIncrementExpression,
					    SyntaxKind.PostIncrementExpression,
					    SyntaxKind.PreDecrementExpression,
					    SyntaxKind.PostDecrementExpression, SyntaxKind.PreDecrementExpression,
					    SyntaxKind.PostDecrementExpression))
				{
					return true;
				}
			}

			return false;
		}
	}
}