using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Comparers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

public static class ConvertIfToSwitchCodeRefactoring
{
	/// <summary>
	///   Tries to convert an if-else-if chain into a switch statement or switch expression.
	///   The chain is eligible when every condition tests the same identifier against a
	///   constant-like value using == (or an is-pattern with constant / or-pattern).
	///   Priority:
	///   1. switch expression  — when every branch is a single <c>return expr;</c>
	///   and an else clause (default) is present.
	///   2. switch statement   — for all other eligible chains.
	///   Minimum requirement: at least 2 if-branches (to avoid generating a noisier one-case
	///   switch for a trivial if/else).
	/// </summary>
	public static bool TryConvertIfElseChainToSwitch(
		IfStatementSyntax node,
		[NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		ExpressionSyntax? switchTarget = null;
		var sections = new List<(List<ExpressionSyntax> Labels, StatementSyntax Body)>();

		// Walk the if / else-if chain.
		SyntaxNode? current = node;

		while (current is IfStatementSyntax ifNode)
		{
			if (!TryExtractSwitchLabels(ifNode.Condition, ref switchTarget, out var labels))
			{
				return false;
			}

			sections.Add((labels, ifNode.Statement));
			current = ifNode.Else?.Statement;
		}

		// Need at least 2 if-branches to be worth converting.
		if (sections.Count < 2)
		{
			return false;
		}

		if (switchTarget is null)
		{
			return false;
		}

		// The remaining node (if any) becomes the default case.
		var defaultBody = current as StatementSyntax;

		// --- Prefer switch expression when all branches are pure returns ---
		if (defaultBody is not null
		    && TryBuildSwitchExpression(switchTarget, sections, defaultBody, out var switchExpr))
		{
			result = switchExpr;
			return true;
		}

		// --- Fall back to switch statement ---
		var switchSections = new List<SwitchSectionSyntax>();

		foreach (var (labels, body) in sections)
		{
			var caseLabel = BuildCaseSwitchLabel(labels);

			switchSections.Add(SwitchSection(
				List([ caseLabel ]),
				List(BuildSwitchSectionStatements(body))));
		}

		if (defaultBody is not null)
		{
			switchSections.Add(SwitchSection(
				List<SwitchLabelSyntax>([ DefaultSwitchLabel() ]),
				List(BuildSwitchSectionStatements(defaultBody))));
		}

		result = SwitchStatement(switchTarget, List(switchSections));
		return true;
	}

	/// <summary>
	///   Tries to convert a nested conditional (ternary) chain into a switch expression.
	///   Eligible when every condition tests the same target against constant-like values using
	///   == / || (or an is-pattern), e.g.
	///   <c>target == 1 ? 0 : target == 5 ? 1 : -1</c> becomes
	///   <c>target switch { 1 => 0, 5 => 1, _ => -1 }</c>.
	///   Requires at least 2 matched arms (so a single comparison stays a plain ternary).
	/// </summary>
	public static bool TryConvertConditionalChainToSwitch(
		ConditionalExpressionSyntax node,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;
		ExpressionSyntax? switchTarget = null;
		var arms = new List<SwitchExpressionArmSyntax>();

		var current = (ExpressionSyntax) node;

		while (UnwrapParentheses(current) is ConditionalExpressionSyntax conditional
		       && TryExtractSwitchLabels(conditional.Condition, ref switchTarget, out var labels))
		{
			PatternSyntax pattern = ConstantPattern(labels[0]);

			for (var i = 1; i < labels.Count; i++)
			{
				pattern = BinaryPattern(SyntaxKind.OrPattern, pattern, ConstantPattern(labels[i]));
			}

			arms.Add(SwitchExpressionArm(pattern, conditional.WhenTrue));
			current = conditional.WhenFalse;
		}

		// Need at least 2 matched arms to be worth converting. The first expression that is not a
		// matching conditional becomes the default arm.
		if (arms.Count < 2 || switchTarget is null)
		{
			return false;
		}

		arms.Add(SwitchExpressionArm(DiscardPattern(), current));

		result = SwitchExpression(switchTarget, SeparatedList(arms));
		return true;
	}

	/// <summary>
	///   Tries to build a <c>return x switch { ... };</c> statement.
	///   Succeeds only when every section (and the default body) contains a single
	///   <c>return expr;</c> statement.
	/// </summary>
	private static bool TryBuildSwitchExpression(
		ExpressionSyntax switchTarget,
		List<(List<ExpressionSyntax> Labels, StatementSyntax Body)> sections,
		StatementSyntax defaultBody,
		[NotNullWhen(true)] out StatementSyntax? result)
	{
		result = null;
		var arms = new List<SwitchExpressionArmSyntax>();

		foreach (var (labels, body) in sections)
		{
			if (!TryGetSingleReturnExpression(body, out var returnExpr))
			{
				return false;
			}

			PatternSyntax pattern = ConstantPattern(labels[0]);

			for (var i = 1; i < labels.Count; i++)
			{
				pattern = BinaryPattern(
					SyntaxKind.OrPattern,
					pattern,
					ConstantPattern(labels[i]));
			}

			arms.Add(SwitchExpressionArm(pattern, returnExpr));
		}

		if (!TryGetSingleReturnExpression(defaultBody, out var defaultExpr))
		{
			return false;
		}

		arms.Add(SwitchExpressionArm(DiscardPattern(), defaultExpr));

		result = ReturnStatement(
			SwitchExpression(
				switchTarget,
				SeparatedList(arms)));

		return true;
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="body" /> is (or wraps) a single
	///   <c>return expr;</c> statement, and sets <paramref name="expression" /> to the returned
	///   expression.
	/// </summary>
	private static bool TryGetSingleReturnExpression(
		StatementSyntax body,
		[NotNullWhen(true)] out ExpressionSyntax? expression)
	{
		expression = null;

		return body switch
		{
			ReturnStatementSyntax { Expression: { } expr }
				=> (expression = expr) is not null,
			BlockSyntax { Statements: [ ReturnStatementSyntax { Expression: { } expr } ] }
				=> (expression = expr) is not null,
			_ => false
		};
	}

	/// <summary>
	///   Extracts one or more switch case labels from an if-condition.
	///   Handles:
	///   <c>x == constant</c> / <c>constant == x</c>
	///   <c>x == a || x == b</c>
	///   <c>x is constant</c>
	///   <c>x is (A or B)</c>
	/// </summary>
	private static bool TryExtractSwitchLabels(
		ExpressionSyntax condition,
		ref ExpressionSyntax? switchTarget,
		[NotNullWhen(true)] out List<ExpressionSyntax>? labels)
	{
		labels = null;
		var collected = new List<ExpressionSyntax>();

		if (!TryCollectLabels(condition, ref switchTarget, collected)
		    || collected.Count == 0)
		{
			return false;
		}

		labels = collected;
		return true;
	}

	private static bool TryCollectLabels(
		ExpressionSyntax condition,
		ref ExpressionSyntax? switchTarget,
		List<ExpressionSyntax> labels)
	{
		// x == a || x == b
		if (condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } orExpr)
		{
			return TryCollectLabels(orExpr.Left, ref switchTarget, labels)
			       && TryCollectLabels(orExpr.Right, ref switchTarget, labels);
		}

		// x == constant  /  constant == x
		if (condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } eq)
		{
			ExpressionSyntax? target = null;
			ExpressionSyntax? label = null;

			if (IsConstantLike(eq.Right))
			{
				target = eq.Left;
				label = eq.Right;
			}
			else if (IsConstantLike(eq.Left))
			{
				target = eq.Right;
				label = eq.Left;
			}

			if (target is null || label is null)
			{
				return false;
			}

			if (switchTarget is not null && !SyntaxNodeComparer.Get().Equals(switchTarget, target))
			{
				return false;
			}

			switchTarget = target;
			labels.Add(label);
			return true;
		}

		// x is constant  /  x is (A or B)
		if (condition is IsPatternExpressionSyntax { Expression: IdentifierNameSyntax targetExpr } isExpr)
		{
			if (switchTarget is not null && !SyntaxNodeComparer.Get().Equals(switchTarget, targetExpr))
			{
				return false;
			}

			if (!TryCollectPatternLabels(isExpr.Pattern, labels))
			{
				return false;
			}

			switchTarget = targetExpr;
			return true;
		}

		return false;
	}

	private static bool TryCollectPatternLabels(PatternSyntax pattern, List<ExpressionSyntax> labels)
	{
		switch (pattern)
		{
			case ConstantPatternSyntax constPat:
			{
				labels.Add(constPat.Expression);
				return true;
			}

			case BinaryPatternSyntax orPat when orPat.IsKind(SyntaxKind.OrPattern):
			{
				return TryCollectPatternLabels(orPat.Left, labels)
				       && TryCollectPatternLabels(orPat.Right, labels);
			}

			case ParenthesizedPatternSyntax paren:
			{
				return TryCollectPatternLabels(paren.Pattern, labels);
			}

			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	///   Returns <see langword="true" /> when the expression is constant-like and therefore safe
	///   to use as a switch case label (literal, negative literal, enum member access).
	/// </summary>
	private static bool IsConstantLike(ExpressionSyntax expr)
	{
		return expr is LiteralExpressionSyntax
			or MemberAccessExpressionSyntax // e.g. Color.Red
			or PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken };
		// -1
	}

	/// <summary>
	///   Builds the statement list for a single switch section, automatically appending a
	///   <c>break;</c> when the body does not already end with a jump statement.
	/// </summary>
	private static List<StatementSyntax> BuildSwitchSectionStatements(StatementSyntax body)
	{
		var result = new List<StatementSyntax>();

		if (body is BlockSyntax block)
		{
			result.AddRange(block.Statements);
		}
		else
		{
			result.Add(body);
		}

		if (result.Count == 0 || !ContainsJumpStatement(result[^1]))
		{
			result.Add(BreakStatement());
		}

		return result;
	}

	/// <summary>
	///   Builds a single switch label for a set of case values.
	///   <list type="bullet">
	///     <item>Single value   → <c>case 1:</c></item>
	///     <item>Multiple values → <c>case 1 or 2 or 3:</c></item>
	///   </list>
	/// </summary>
	private static SwitchLabelSyntax BuildCaseSwitchLabel(List<ExpressionSyntax> labels)
	{
		if (labels.Count == 1)
		{
			return CaseSwitchLabel(labels[0]);
		}

		// Build an or-pattern: case 1 or 2 or 3:
		PatternSyntax pattern = ConstantPattern(labels[0]);

		for (var i = 1; i < labels.Count; i++)
		{
			pattern = BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				ConstantPattern(labels[i]));
		}

		return CasePatternSwitchLabel(pattern, Token(SyntaxKind.ColonToken));
	}

	/// <summary>
	///   Returns <see langword="true" /> when a statement unconditionally ends with a jump
	///   (return, break, continue, or throw), making it safe to combine consecutive if
	///   statements with identical bodies using <c>||</c>.
	/// </summary>
	private static bool ContainsJumpStatement(StatementSyntax statement)
	{
		return statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && ContainsJumpStatement(block.Statements.Last()),
			_ => false
		};
	}

	/// <summary>
	///   Tries to decompose a single else-less if-statement into a switch section: a target
	///   identifier, a case label, the body, and the numeric value-set its condition matches.
	///   Only conditions over a single identifier compared to numeric literals are accepted.
	/// </summary>
	internal static bool TryGetConsecutiveIfSection(IfStatementSyntax ifNode, out IfSwitchSection section)
	{
		section = default;

		if (ifNode.Else is not null)
		{
			return false;
		}

		IdentifierNameSyntax? target = null;

		if (!TryExtractTargetPattern(ifNode.Condition, ref target, out var pattern, out var intervals) || target is null)
		{
			return false;
		}

		// A single constant becomes a plain `case 1:` label; everything else (relational or
		// or-patterns) becomes a `case <pattern>:` label.
		var label = pattern is ConstantPatternSyntax constant
			? (SwitchLabelSyntax) CaseSwitchLabel(constant.Expression)
			: CasePatternSwitchLabel(pattern, Token(SyntaxKind.ColonToken));

		section = new IfSwitchSection(target, label, ifNode.Statement, intervals);
		return true;
	}

	/// <summary>
	///   Returns <see langword="true" /> when the value-sets matched by the two sections are
	///   disjoint, so converting the independent ifs into a switch cannot change behaviour.
	/// </summary>
	internal static bool AreMutuallyExclusive(IfSwitchSection a, IfSwitchSection b)
	{
		foreach (var ia in a.Intervals)
		{
			foreach (var ib in b.Intervals)
			{
				if (IntervalsOverlap(ia, ib))
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="body" /> writes to a variable named
	///   <paramref name="name" /> (assignment, compound assignment, ++/--, or ref/out argument).
	///   A purely syntactic check so it works on already-rewritten (synthetic) bodies.
	/// </summary>
	internal static bool AssignsToIdentifier(StatementSyntax body, string name)
	{
		foreach (var node in body.DescendantNodesAndSelf())
		{
			switch (node)
			{
				case AssignmentExpressionSyntax assignment
					when UnwrapParentheses(assignment.Left) is IdentifierNameSyntax id && id.Identifier.ValueText == name:
				case PostfixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PostIncrementExpression or (int) SyntaxKind.PostDecrementExpression } postfix
					when UnwrapParentheses(postfix.Operand) is IdentifierNameSyntax postId && postId.Identifier.ValueText == name:
				case PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PreIncrementExpression or (int) SyntaxKind.PreDecrementExpression } prefix
					when UnwrapParentheses(prefix.Operand) is IdentifierNameSyntax preId && preId.Identifier.ValueText == name:
				case ArgumentSyntax { RefKindKeyword.RawKind: not (int) SyntaxKind.None } argument
					when UnwrapParentheses(argument.Expression) is IdentifierNameSyntax argId && argId.Identifier.ValueText == name:
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	///   Builds the switch statement for a validated run of sections. Fails when two sections
	///   declare a top-level local of the same name (switch sections share one declaration scope).
	/// </summary>
	internal static bool TryBuildConsecutiveIfsSwitch(
		ExpressionSyntax target,
		IReadOnlyList<IfSwitchSection> sections,
		[NotNullWhen(true)] out SwitchStatementSyntax? result)
	{
		result = null;
		var declaredNames = new HashSet<string>();
		var switchSections = new List<SwitchSectionSyntax>(sections.Count);

		foreach (var section in sections)
		{
			foreach (var name in GetSectionLocalNames(section.Body))
			{
				if (!declaredNames.Add(name))
				{
					return false;
				}
			}

			switchSections.Add(SwitchSection(
				List([ section.Label ]),
				List(BuildSwitchSectionStatements(section.Body))));
		}

		result = SwitchStatement(target, List(switchSections));
		return true;
	}

	private static IEnumerable<string> GetSectionLocalNames(StatementSyntax body)
	{
		var statements = body is BlockSyntax block
			? (IEnumerable<StatementSyntax>) block.Statements
			: [ body ];

		foreach (var statement in statements)
		{
			if (statement is LocalDeclarationStatementSyntax local)
			{
				foreach (var variable in local.Declaration.Variables)
				{
					yield return variable.Identifier.ValueText;
				}
			}
		}
	}

	private static bool TryExtractTargetPattern(
		ExpressionSyntax condition,
		ref IdentifierNameSyntax? target,
		out PatternSyntax pattern,
		out List<NumericInterval> intervals)
	{
		pattern = null!;
		intervals = null!;

		switch (UnwrapParentheses(condition))
		{
			// x == a || x == b  (recursively folds into one or-pattern)
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } orExpr:
			{
				if (!TryExtractTargetPattern(orExpr.Left, ref target, out var leftPattern, out var leftIntervals)
				    || !TryExtractTargetPattern(orExpr.Right, ref target, out var rightPattern, out var rightIntervals))
				{
					return false;
				}

				pattern = BinaryPattern(SyntaxKind.OrPattern, leftPattern, rightPattern);
				leftIntervals.AddRange(rightIntervals);
				intervals = leftIntervals;
				return true;
			}

			// x == constant  /  constant == x
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } eq:
			{
				if (!TryGetTargetAndLiteral(eq.Left, eq.Right, ref target, out var literal, out var value))
				{
					return false;
				}

				pattern = ConstantPattern(literal);
				intervals = [ NumericInterval.Point(value) ];
				return true;
			}

			// x < constant, x <= constant, x > constant, x >= constant (and flipped forms)
			case BinaryExpressionSyntax relational when IsRelational(relational.Kind()):
			{
				return TryExtractRelational(relational, ref target, out pattern, out intervals);
			}

			// x is <pattern>
			case IsPatternExpressionSyntax isExpr when UnwrapParentheses(isExpr.Expression) is IdentifierNameSyntax id:
			{
				return SetTarget(ref target, id) && TryExtractPattern(isExpr.Pattern, out pattern, out intervals);
			}

			default:
			{
				return false;
			}
		}
	}

	private static bool TryExtractRelational(
		BinaryExpressionSyntax binary,
		ref IdentifierNameSyntax? target,
		out PatternSyntax pattern,
		out List<NumericInterval> intervals)
	{
		pattern = null!;
		intervals = null!;

		SyntaxKind expressionKind;

		if (UnwrapParentheses(binary.Left) is IdentifierNameSyntax leftId && TryGetNumericLiteral(binary.Right, out var value, out var literal))
		{
			if (!SetTarget(ref target, leftId))
			{
				return false;
			}

			expressionKind = binary.Kind();
		}
		else if (UnwrapParentheses(binary.Right) is IdentifierNameSyntax rightId && TryGetNumericLiteral(binary.Left, out value, out literal))
		{
			if (!SetTarget(ref target, rightId))
			{
				return false;
			}

			// `constant < x` is equivalent to `x > constant`: flip the operator.
			expressionKind = FlipRelational(binary.Kind());
		}
		else
		{
			return false;
		}

		var operatorToken = RelationalOperatorToken(expressionKind);
		pattern = RelationalPattern(operatorToken, literal);
		intervals = [ IntervalForToken(operatorToken.Kind(), value) ];
		return true;
	}

	private static bool TryExtractPattern(PatternSyntax patternSyntax, out PatternSyntax pattern, out List<NumericInterval> intervals)
	{
		pattern = null!;
		intervals = null!;

		switch (UnwrapPattern(patternSyntax))
		{
			case ConstantPatternSyntax constant when TryGetNumericLiteral(constant.Expression, out var value, out var literal):
			{
				pattern = ConstantPattern(literal);
				intervals = [ NumericInterval.Point(value) ];
				return true;
			}

			case RelationalPatternSyntax relational
				when IsRelationalToken(relational.OperatorToken.Kind()) && TryGetNumericLiteral(relational.Expression, out var value, out var literal):
			{
				pattern = RelationalPattern(relational.OperatorToken, literal);
				intervals = [ IntervalForToken(relational.OperatorToken.Kind(), value) ];
				return true;
			}

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.OrPattern } orPattern:
			{
				if (!TryExtractPattern(orPattern.Left, out var leftPattern, out var leftIntervals)
				    || !TryExtractPattern(orPattern.Right, out var rightPattern, out var rightIntervals))
				{
					return false;
				}

				pattern = BinaryPattern(SyntaxKind.OrPattern, leftPattern, rightPattern);
				leftIntervals.AddRange(rightIntervals);
				intervals = leftIntervals;
				return true;
			}

			default:
			{
				return false;
			}
		}
	}

	private static bool TryGetTargetAndLiteral(
		ExpressionSyntax side1,
		ExpressionSyntax side2,
		ref IdentifierNameSyntax? target,
		out ExpressionSyntax literal,
		out double value)
	{
		if (UnwrapParentheses(side1) is IdentifierNameSyntax id1 && TryGetNumericLiteral(side2, out value, out literal))
		{
			return SetTarget(ref target, id1);
		}

		if (UnwrapParentheses(side2) is IdentifierNameSyntax id2 && TryGetNumericLiteral(side1, out value, out literal))
		{
			return SetTarget(ref target, id2);
		}

		literal = null!;
		value = 0;
		return false;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expression, out double value, out ExpressionSyntax literal)
	{
		value = 0;
		literal = null!;
		var unwrapped = UnwrapParentheses(expression);

		switch (unwrapped)
		{
			case LiteralExpressionSyntax lit when IsNumericValue(lit.Token.Value, out value):
			{
				literal = lit;
				return true;
			}

			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } negation
				when TryGetNumericLiteral(negation.Operand, out var inner, out _):
			{
				value = -inner;
				literal = unwrapped;
				return true;
			}

			default:
			{
				return false;
			}
		}
	}

	private static bool IsNumericValue(object? value, out double result)
	{
		switch (value)
		{
			case byte b:
				result = b;
				return true;
			case sbyte sb:
				result = sb;
				return true;
			case short s:
				result = s;
				return true;
			case ushort us:
				result = us;
				return true;
			case int i:
				result = i;
				return true;
			case uint ui:
				result = ui;
				return true;
			case long l:
				result = l;
				return true;
			case ulong ul:
				result = ul;
				return true;
			case float f:
				result = f;
				return true;
			case double d:
				result = d;
				return true;
			case decimal dec:
				result = (double) dec;
				return true;
			case char c:
				result = c;
				return true;
			default:
				result = 0;
				return false;
		}
	}

	private static bool SetTarget(ref IdentifierNameSyntax? target, IdentifierNameSyntax candidate)
	{
		if (target is null)
		{
			target = candidate;
			return true;
		}

		return target.Identifier.ValueText == candidate.Identifier.ValueText;
	}

	private static bool IsRelational(SyntaxKind kind)
	{
		return kind is SyntaxKind.LessThanExpression
			or SyntaxKind.LessThanOrEqualExpression
			or SyntaxKind.GreaterThanExpression
			or SyntaxKind.GreaterThanOrEqualExpression;
	}

	private static bool IsRelationalToken(SyntaxKind tokenKind)
	{
		return tokenKind is SyntaxKind.LessThanToken
			or SyntaxKind.LessThanEqualsToken
			or SyntaxKind.GreaterThanToken
			or SyntaxKind.GreaterThanEqualsToken;
	}

	private static SyntaxKind FlipRelational(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
			_ => kind
		};
	}

	private static SyntaxToken RelationalOperatorToken(SyntaxKind expressionKind)
	{
		return expressionKind switch
		{
			SyntaxKind.LessThanExpression => Token(SyntaxKind.LessThanToken),
			SyntaxKind.LessThanOrEqualExpression => Token(SyntaxKind.LessThanEqualsToken),
			SyntaxKind.GreaterThanExpression => Token(SyntaxKind.GreaterThanToken),
			SyntaxKind.GreaterThanOrEqualExpression => Token(SyntaxKind.GreaterThanEqualsToken),
			_ => Token(SyntaxKind.LessThanToken)
		};
	}

	private static NumericInterval IntervalForToken(SyntaxKind operatorTokenKind, double value)
	{
		return operatorTokenKind switch
		{
			SyntaxKind.LessThanToken => new NumericInterval(Double.NegativeInfinity, false, value, false),
			SyntaxKind.LessThanEqualsToken => new NumericInterval(Double.NegativeInfinity, false, value, true),
			SyntaxKind.GreaterThanToken => new NumericInterval(value, false, Double.PositiveInfinity, false),
			SyntaxKind.GreaterThanEqualsToken => new NumericInterval(value, true, Double.PositiveInfinity, false),
			_ => NumericInterval.Point(value)
		};
	}

	private static bool IntervalsOverlap(NumericInterval a, NumericInterval b)
	{
		var aLeftOfB = a.High < b.Low || a.High == b.Low && (!a.HighInclusive || !b.LowInclusive);
		var aRightOfB = a.Low > b.High || a.Low == b.High && (!a.LowInclusive || !b.HighInclusive);

		return !(aLeftOfB || aRightOfB);
	}

	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
		{
			expression = parenthesized.Expression;
		}

		return expression;
	}

	private static PatternSyntax UnwrapPattern(PatternSyntax pattern)
	{
		while (pattern is ParenthesizedPatternSyntax parenthesized)
		{
			pattern = parenthesized.Pattern;
		}

		return pattern;
	}

	// =====================================================================================
	//  Consecutive (non-else) if-statements → switch
	//
	//  Converts a run of independent sibling if-statements that all test the same identifier
	//  against mutually-exclusive constant / relational patterns into a single switch, e.g.
	//
	//      if (n == 0) return 1;          switch (n)
	//      if (n < 0)  n = -n;     =>      {
	//                                          case 0:    return 1;
	//                                          case < 0:  n = -n; break;
	//                                      }
	//
	//  Safety relies on three invariants checked by the caller / builder:
	//    * the case patterns are mutually exclusive (see AreMutuallyExclusive), so at most one
	//      body runs — exactly the switch semantics;
	//    * only the last section may write to the target (a switch reads the target once, while
	//      sequential ifs re-evaluate it — see AssignsToIdentifier);
	//    * no two sections declare the same top-level local (switch sections share one scope).
	// =====================================================================================

	/// <summary>A half-open or closed numeric interval used for mutual-exclusivity reasoning.</summary>
	internal readonly struct NumericInterval(double low, bool lowInclusive, double high, bool highInclusive)
	{
		public double Low { get; } = low;
		public bool LowInclusive { get; } = lowInclusive;
		public double High { get; } = high;
		public bool HighInclusive { get; } = highInclusive;

		public static NumericInterval Point(double value)
		{
			return new NumericInterval(value, true, value, true);
		}
	}

	/// <summary>One eligible if-statement decomposed into a switch section.</summary>
	internal readonly struct IfSwitchSection(IdentifierNameSyntax target, SwitchLabelSyntax label, StatementSyntax body, List<NumericInterval> intervals)
	{
		public IdentifierNameSyntax Target { get; } = target;
		public SwitchLabelSyntax Label { get; } = label;
		public StatementSyntax Body { get; } = body;
		public List<NumericInterval> Intervals { get; } = intervals;
	}
}