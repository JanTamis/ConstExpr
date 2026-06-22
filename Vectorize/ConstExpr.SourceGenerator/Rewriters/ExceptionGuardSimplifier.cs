using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Removes conditional branches that can never be reached because an unconditionally evaluated
///   sibling throws. When an expression contains a "gate" of the form <c>C ? value : throw</c> that
///   is always evaluated, the whole expression throws unless <c>C</c> holds — so every other
///   <c>cond ? a : b</c> in that same expression may assume <c>C</c> is true.
///   <para>
///     e.g. <c>(count &gt; 0 ? element : throw) + (count &gt; 0 ? 1 : 0)</c> =&gt;
///     <c>(count &gt; 0 ? element : throw) + 1</c>.
///   </para>
///   The gates themselves are never rewritten, so the exceptions they throw are preserved.
/// </summary>
public sealed class ExceptionGuardSimplifier : CSharpSyntaxRewriter
{
	public static SyntaxNode Simplify(SyntaxNode node)
	{
		return new ExceptionGuardSimplifier().Visit(node);
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		return node.Expression is null ? node : node.WithExpression(Fold(node.Expression));
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		return node.WithExpression(Fold(node.Expression));
	}

	public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
	{
		return node.WithValue(Fold(node.Value));
	}

	// Each statement-level expression is its own gate scope: facts only hold for that expression.
	private static ExpressionSyntax Fold(ExpressionSyntax expression)
	{
		var facts = new List<ExpressionSyntax>();
		CollectGateFacts(expression, facts);

		return facts.Count == 0
			? expression
			: (ExpressionSyntax)new ConditionFolder(facts).Visit(expression);
	}

	// A condition C is guaranteed true whenever this expression yields a value, if some always-evaluated
	// sub-expression is `C ? v : throw` (or `C ? throw : v` => !C). Only descend through nodes that don't
	// guard evaluation (parentheses, arithmetic) — not into ternary branches, && / || or lambdas.
	private static void CollectGateFacts(ExpressionSyntax expression, List<ExpressionSyntax> facts)
	{
		switch (expression)
		{
			case ParenthesizedExpressionSyntax paren:
				CollectGateFacts(paren.Expression, facts);
				break;
			case BinaryExpressionSyntax
			{
				RawKind: (int)SyntaxKind.AddExpression
				or (int)SyntaxKind.SubtractExpression
				or (int)SyntaxKind.MultiplyExpression
				or (int)SyntaxKind.DivideExpression
				or (int)SyntaxKind.ModuloExpression
			} binary:
				CollectGateFacts(binary.Left, facts);
				CollectGateFacts(binary.Right, facts);
				break;
			case ConditionalExpressionSyntax { WhenFalse: ThrowExpressionSyntax } gate:
				facts.Add(Unwrap(gate.Condition));
				break;
			case ConditionalExpressionSyntax { WhenTrue: ThrowExpressionSyntax } gate:
				facts.Add(Negate(Unwrap(gate.Condition)));
				break;
		}
	}

	private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax paren)
		{
			expression = paren.Expression;
		}

		return expression;
	}

	// Logical negation of a comparison, used so the collector can register a `C ? throw : v` gate's
	// implied fact and so the folder can recognise `count <= 0` as the negation of `count > 0`.
	private static ExpressionSyntax Negate(ExpressionSyntax expression)
	{
		if (expression is BinaryExpressionSyntax binary && TryFlipComparison((SyntaxKind)binary.RawKind, out var flipped))
		{
			return BinaryExpression(flipped, binary.Left, binary.Right);
		}

		return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(expression));
	}

	private static bool TryFlipComparison(SyntaxKind kind, out SyntaxKind flipped)
	{
		flipped = kind switch
		{
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanExpression,
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanExpression,
			SyntaxKind.EqualsExpression => SyntaxKind.NotEqualsExpression,
			SyntaxKind.NotEqualsExpression => SyntaxKind.EqualsExpression,
			_ => SyntaxKind.None,
		};

		return flipped != SyntaxKind.None;
	}

	private sealed class ConditionFolder(List<ExpressionSyntax> facts) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
		{
			// Never rewrite a gate's condition: folding `count > 2` to `true` here would delete the throw.
			if (node.WhenTrue is ThrowExpressionSyntax || node.WhenFalse is ThrowExpressionSyntax)
			{
				return node
					.WithWhenTrue((ExpressionSyntax)Visit(node.WhenTrue))
					.WithWhenFalse((ExpressionSyntax)Visit(node.WhenFalse));
			}

			var condition = FoldCondition(node.Condition);

			if (TryGetBool(condition, out var value))
			{
				// ponytail: drops the other branch — sound only because ConstExpr closed-forms produce pure,
				// non-throwing branches. Guard with an IsPure check if a side-effecting branch ever reaches here.
				return Visit(value ? node.WhenTrue : node.WhenFalse);
			}

			return node
				.WithCondition(condition.WithTriviaFrom(node.Condition))
				.WithWhenTrue((ExpressionSyntax)Visit(node.WhenTrue))
				.WithWhenFalse((ExpressionSyntax)Visit(node.WhenFalse));
		}

		// Folding `(count > 0 ? 1 : 0)` to its branch leaves the wrapping parens around an atom: `(1)`.
		// Drop parens that became redundant; keep them where precedence still needs them (e.g. `(a ? b : c) * 4`).
		public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
		{
			var inner = (ExpressionSyntax)Visit(node.Expression);

			return inner is LiteralExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax
				or InvocationExpressionSyntax or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax
				? inner.WithTriviaFrom(node)
				: node.WithExpression(inner);
		}

		// Don't fold facts about outer variables into a lambda body: it may be invoked later, when the
		// guaranteeing gate is no longer on the evaluation path.
		public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;
		public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;

		private ExpressionSyntax FoldCondition(ExpressionSyntax condition)
		{
			switch (condition)
			{
				case ParenthesizedExpressionSyntax paren:
					return FoldCondition(paren.Expression);

				case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } and:
				{
					var left = FoldCondition(and.Left);
					var right = FoldCondition(and.Right);

					if (IsBool(left, false) || IsBool(right, false))
					{
						return CreateLiteral(false);
					}

					if (IsBool(left, true))
					{
						return right;
					}

					if (IsBool(right, true))
					{
						return left;
					}
					return and.WithLeft(left).WithRight(right);
				}

				case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } or:
				{
					var left = FoldCondition(or.Left);
					var right = FoldCondition(or.Right);

					if (IsBool(left, true) || IsBool(right, true))
					{
						return CreateLiteral(true);
					}

					if (IsBool(left, false))
					{
						return right;
					}

					if (IsBool(right, false))
					{
						return left;
					}

					return or.WithLeft(left).WithRight(right);
				}

				case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not:
				{
					var operand = FoldCondition(not.Operand);

					if (IsBool(operand, true))
					{
						return CreateLiteral(false);
					}

					if (IsBool(operand, false))
					{
						return CreateLiteral(true);
					}
					return not.WithOperand(operand);
				}
			}

			foreach (var fact in facts)
			{
				if (fact.EqualsTo(condition))
				{
					return CreateLiteral(true);
				}

				if (AreNegations(fact, condition))
				{
					return CreateLiteral(false);
				}
			}

			return condition;
		}

		private static bool AreNegations(ExpressionSyntax a, ExpressionSyntax b)
		{
			return a is BinaryExpressionSyntax ba
			       && b is BinaryExpressionSyntax bb
			       && TryFlipComparison((SyntaxKind)ba.RawKind, out var flipped)
			       && (int)flipped == bb.RawKind
			       && ba.Left.EqualsTo(bb.Left)
			       && ba.Right.EqualsTo(bb.Right);
		}

		private static bool TryGetBool(ExpressionSyntax expression, out bool value)
		{
			if (IsBool(expression, true))
			{
				value = true;
				return true;
			}

			if (IsBool(expression, false))
			{
				value = false;
				return true;
			}
			value = false;
			return false;
		}

		private static bool IsBool(ExpressionSyntax expression, bool value)
		{
			return expression.RawKind == (int)(value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
		}
	}
}