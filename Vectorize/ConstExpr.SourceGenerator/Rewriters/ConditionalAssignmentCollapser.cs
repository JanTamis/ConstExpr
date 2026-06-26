using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Post-DeadCodePruner pass that collapses an if/else whose two branches do nothing but assign the
///   same ordered set of locals — the shape a colour conversion leaves once a known hue/value has been
///   folded away, e.g.
///   <code>
/// double r = 0D, g = 0D, b = 0D;
/// if (s == 0D) { r = 0.5; g = 0.5; b = 0.5; }
/// else        { r = (1D - s) * 0.5; g = 0.5; b = 0.5; }
/// return ((byte)(r * 255D), (byte)(g * 255D), (byte)(b * 255D));
/// </code>
///   A channel written identically in both branches is a constant; it is substituted into the trailing
///   statements and folded away (so <c>(byte)(0.5 * 255D)</c> becomes <c>127</c>). A channel that differs
///   becomes a single <c>var</c> initialised with the conditional, and when its only use is
///   <c>(byte)(channel * scale)</c> the constant <c>scale</c> is folded into the conditional arms and the
///   use is reduced to <c>(byte)channel</c>:
///   <code>
/// var r = s == 0D ? 127.5 : (1D - s) * 127.5;
/// return ((byte)r, 127, 127);
/// </code>
///   The pass only runs against branches DeadCodePruner has already cleaned (no dead temporaries remain),
///   which is why it runs after pruning rather than inside the rewriter. It is deliberately conservative:
///   any shape it does not fully recognise is left untouched.
/// </summary>
public sealed class ConditionalAssignmentCollapser(bool allowReassociation) : CSharpSyntaxRewriter
{
	/// <param name="allowReassociation">
	///   Whether floating-point reassociation is permitted (AssociativeMath). When it is not, a conditional
	///   channel whose scale folding would reassociate a variable factor is left as an if/else rather than
	///   rewritten, matching the rest of the optimizer's FP-safety contract.
	/// </param>
	public static SyntaxNode? Collapse(SyntaxNode? node, bool allowReassociation)
	{
		if (node is null)
		{
			return null;
		}

		return new ConditionalAssignmentCollapser(allowReassociation).Visit(node);
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// Recurse first so nested blocks are handled before the enclosing one.
		var visited = (BlockSyntax) base.VisitBlock(node)!;
		var rewritten = TryCollapseStatements(visited.Statements, allowReassociation);

		return rewritten is null ? visited : visited.WithStatements(rewritten.Value);
	}

	// -----------------------------------------------------------------------
	// Statement-list rewriting
	// -----------------------------------------------------------------------

	private static SyntaxList<StatementSyntax>? TryCollapseStatements(SyntaxList<StatementSyntax> statements, bool allowReassociation)
	{
		for (var ifIndex = 0; ifIndex < statements.Count; ifIndex++)
		{
			if (statements[ifIndex] is not IfStatementSyntax { Else: { } elseClause } ifStatement)
			{
				continue;
			}

			if (!TryGetSimpleAssignmentSequence(ifStatement.Statement, out var thenAssignments)
			    || !TryGetSimpleAssignmentSequence(elseClause.Statement, out var elseAssignments)
			    || thenAssignments.Count < 2
			    || thenAssignments.Count != elseAssignments.Count)
			{
				continue;
			}

			var targets = new List<string>(thenAssignments.Count);

			for (var i = 0; i < thenAssignments.Count; i++)
			{
				if (thenAssignments[i].Target != elseAssignments[i].Target)
				{
					targets.Clear();
					break;
				}

				targets.Add(thenAssignments[i].Target);
			}

			if (targets.Count < 2)
			{
				continue;
			}

			var trailing = statements.Skip(ifIndex + 1).ToList();

			if (trailing.Count == 0)
			{
				continue;
			}

			// Every target must be declared exactly once before the if with a constant initializer, must
			// not be read between its declaration and the if, and must not be written again afterwards.
			if (!TryLocateTargetDeclarations(statements, ifIndex, targets, out var declaratorByTarget)
			    || targets.Any(t => IsWrittenIn(t, trailing)))
			{
				continue;
			}

			var condition = ifStatement.Condition;
			var keptDeclarations = new List<StatementSyntax>();
			var constantValues = new Dictionary<string, double>(StringComparer.Ordinal);
			var failed = false;

			// First pass: classify each target and collect the constants that the trailing statements
			// will need substituted in.
			for (var i = 0; i < targets.Count; i++)
			{
				var thenValue = thenAssignments[i].Assignment.Right;
				var elseValue = elseAssignments[i].Assignment.Right;

				if (!thenValue.EqualsTo(elseValue))
				{
					continue; // differs between branches → conditional channel, handled below
				}

				// Identical in both branches. Only collapse when it is a numeric constant that folds into
				// the trailing statements; a symbolic invariant (such as an unknown `v`) is left untouched
				// so the whole if/else is preserved rather than half-rewritten.
				if (!TryGetNumeric(thenValue, out var constant))
				{
					failed = true;
					break;
				}

				constantValues[targets[i]] = constant;
			}

			if (failed)
			{
				continue;
			}

			// Second pass: build the conditional channels, folding their consumer's scale in.
			for (var i = 0; i < targets.Count && !failed; i++)
			{
				var target = targets[i];

				if (constantValues.ContainsKey(target))
				{
					continue;
				}

				var thenValue = thenAssignments[i].Assignment.Right;
				var elseValue = elseAssignments[i].Assignment.Right;

				if (thenValue.EqualsTo(elseValue))
				{
					continue; // already handled as a non-numeric kept declaration
				}

				if (!TryGetUniformScale(target, trailing, out var scale))
				{
					failed = true;
					break;
				}

				var thenScaled = FoldScale(thenValue, scale, allowReassociation);
				var elseScaled = FoldScale(elseValue, scale, allowReassociation);

				// Only collapse a conditional channel when the scale folds cleanly into both arms; if an arm
				// stays symbolic (e.g. an unknown `v`), leave the if/else intact.
				if (thenScaled is null || elseScaled is null)
				{
					failed = true;
					break;
				}

				var conditional = ConditionalExpression(condition, thenScaled, elseScaled);

				keptDeclarations.Add(DeclareVar(target, conditional));
			}

			if (failed)
			{
				continue;
			}

			// Rewrite the trailing statements: substitute constants (folding them away) and reduce
			// `(byte)(channel * scale)` to `(byte)channel` for the conditional channels.
			var rewriter = new TrailingRewriter(constantValues, targets);
			var newTrailing = trailing.Select(s => (StatementSyntax) rewriter.Visit(s)!).ToList();

			if (rewriter.Failed)
			{
				continue;
			}

			var result = new List<StatementSyntax>();

			// Statements before the if, minus the target declarations we are replacing.
			for (var i = 0; i < ifIndex; i++)
			{
				var replacement = RemoveTargetDeclarators(statements[i], declaratorByTarget);

				if (replacement is not null)
				{
					result.Add(replacement);
				}
			}

			result.AddRange(keptDeclarations);
			result.AddRange(newTrailing);

			return List(result);
		}

		return null;
	}

	// -----------------------------------------------------------------------
	// Pattern helpers
	// -----------------------------------------------------------------------

	private static bool TryGetSimpleAssignmentSequence(
		SyntaxNode? statement,
		out List<(string Target, AssignmentExpressionSyntax Assignment)> assignments)
	{
		assignments = [ ];

		var inner = statement switch
		{
			BlockSyntax block => block.Statements.ToList(),
			ExpressionStatementSyntax expression => [ expression ],
			_ => null
		};

		if (inner is null)
		{
			return false;
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);

		foreach (var item in inner)
		{
			if (item is not ExpressionStatementSyntax
			    {
				    Expression: AssignmentExpressionSyntax
				    {
					    Left: IdentifierNameSyntax { Identifier.Text: var name }
				    } assignment
			    }
			    || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
			    || !seen.Add(name))
			{
				assignments = [ ];
				return false;
			}

			assignments.Add((name, assignment));
		}

		return assignments.Count > 0;
	}

	/// <summary>
	///   Confirms every target is declared with a constant initializer in a statement before the if and
	///   is not read between that declaration and the if. Returns the declarators so they can be removed.
	/// </summary>
	private static bool TryLocateTargetDeclarations(
		SyntaxList<StatementSyntax> statements,
		int ifIndex,
		List<string> targets,
		out Dictionary<string, VariableDeclaratorSyntax> declaratorByTarget)
	{
		declaratorByTarget = new Dictionary<string, VariableDeclaratorSyntax>(StringComparer.Ordinal);

		for (var i = 0; i < ifIndex; i++)
		{
			if (statements[i] is not LocalDeclarationStatementSyntax { Declaration.Variables: var declarators })
			{
				continue;
			}

			foreach (var declarator in declarators)
			{
				var name = declarator.Identifier.Text;

				if (targets.Contains(name))
				{
					if (declarator.Initializer is not { Value: { } init } || !IsConstant(init))
					{
						return false;
					}

					declaratorByTarget[name] = declarator;
				}
			}
		}

		if (declaratorByTarget.Count != targets.Count)
		{
			return false;
		}

		// No target may be read between its declaration and the if.
		for (var i = 0; i < ifIndex; i++)
		{
			foreach (var identifier in statements[i].DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (targets.Contains(identifier.Identifier.Text)
				    && identifier.Parent is not VariableDeclaratorSyntax
				    && !(identifier.Parent is AssignmentExpressionSyntax assign && assign.Left == identifier))
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	///   Returns the statement with any target declarators removed; <see langword="null" /> when the whole
	///   declaration only declared targets and is therefore dropped entirely.
	/// </summary>
	private static StatementSyntax? RemoveTargetDeclarators(
		StatementSyntax statement,
		Dictionary<string, VariableDeclaratorSyntax> declaratorByTarget)
	{
		if (statement is not LocalDeclarationStatementSyntax { Declaration: var declaration } local)
		{
			return statement;
		}

		var remaining = declaration.Variables
			.Where(v => !declaratorByTarget.ContainsKey(v.Identifier.Text))
			.ToList();

		if (remaining.Count == declaration.Variables.Count)
		{
			return statement;
		}

		return remaining.Count == 0
			? null
			: local.WithDeclaration(declaration.WithVariables(SeparatedList(remaining)));
	}

	/// <summary>
	///   Checks that every use of <paramref name="target" /> in the trailing statements is
	///   <c>(byte)(target * scale)</c> for one and the same numeric <c>scale</c>, returning that scale.
	/// </summary>
	private static bool TryGetUniformScale(string target, IEnumerable<StatementSyntax> trailing, out double scale)
	{
		scale = 0;
		var found = false;

		foreach (var statement in trailing)
		{
			foreach (var identifier in statement.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (identifier.Identifier.Text != target)
				{
					continue;
				}

				if (identifier.Parent is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multiply
				    || multiply.Left != identifier
				    || !TryGetNumeric(multiply.Right, out var thisScale)
				    || !IsCastOperand(multiply))
				{
					return false;
				}

				if (found && Math.Abs(thisScale - scale) > Double.Epsilon)
				{
					return false;
				}

				scale = thisScale;
				found = true;
			}
		}

		return found;
	}

	private static bool IsWrittenIn(string name, IEnumerable<StatementSyntax> statements)
	{
		return statements.Any(s => s.DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Any(a => a.Left is IdentifierNameSyntax id && id.Identifier.Text == name));
	}

	/// <summary>Returns the expression with any enclosing parentheses removed.</summary>
	private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
		{
			expression = parenthesized.Expression;
		}

		return expression;
	}

	/// <summary>Returns true when the expression (ignoring enclosing parentheses) is the operand of a cast.</summary>
	private static bool IsCastOperand(ExpressionSyntax expression)
	{
		var ancestor = expression.Parent;

		while (ancestor is ParenthesizedExpressionSyntax parenthesized)
		{
			ancestor = parenthesized.Parent;
		}

		return ancestor is CastExpressionSyntax;
	}

	// -----------------------------------------------------------------------
	// Constant folding helpers
	// -----------------------------------------------------------------------

	private static bool IsConstant(ExpressionSyntax expression)
	{
		return TryGetNumeric(expression, out _)
		       || expression is LiteralExpressionSyntax;
	}

	private static bool TryGetNumeric(ExpressionSyntax expression, out double value)
	{
		switch (expression)
		{
			case PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression, Operand: var operand }
				when TryGetNumeric(operand, out var inner):
			{
				value = -inner;
				return true;
			}
			case ParenthesizedExpressionSyntax parenthesized:
			{
				return TryGetNumeric(parenthesized.Expression, out value);
			}
			case LiteralExpressionSyntax literal:
			{
				switch (literal.Token.Value)
				{
					case double d:
						value = d;
						return true;
					case float f:
						value = f;
						return true;
					case int i:
						value = i;
						return true;
					case long l:
						value = l;
						return true;
					case decimal m:
						value = (double) m;
						return true;
				}

				break;
			}
		}

		value = 0;
		return false;
	}

	/// <summary>
	///   Multiplies an expression by a constant scale, folding the constant in: a bare literal is computed,
	///   and <c>x * c</c> becomes <c>x * (c * scale)</c> so the scale collapses into the existing constant
	///   factor (e.g. <c>(1 - s) * 0.5</c> scaled by 255 becomes <c>(1 - s) * 127.5</c>).
	/// </summary>
	private static ExpressionSyntax? FoldScale(ExpressionSyntax expression, double scale, bool allowReassociation)
	{
		// A pure-literal arm is a compile-time constant multiply — sound under any floating-point mode.
		if (TryGetNumeric(expression, out var literalValue))
		{
			return NumericLiteral(literalValue * scale);
		}

		// Folding the scale into `x * c` regroups a variable factor: (x * c) * scale → x * (c * scale).
		// That is floating-point reassociation, only sound when AssociativeMath is enabled.
		if (allowReassociation
		    && expression is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multiply)
		{
			if (TryGetNumeric(multiply.Right, out var rightConstant))
			{
				return multiply.WithRight(NumericLiteral(rightConstant * scale));
			}

			if (TryGetNumeric(multiply.Left, out var leftConstant))
			{
				return multiply.WithLeft(NumericLiteral(leftConstant * scale));
			}
		}

		// The scale could not be folded into a numeric constant factor — signal the caller to bail.
		return null;
	}

	private static LiteralExpressionSyntax NumericLiteral(double value)
	{
		return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
	}

	private static LocalDeclarationStatementSyntax DeclareVar(string name, ExpressionSyntax initializer)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(initializer)))));
	}

	/// <summary>
	///   Rewrites trailing statements: constant channels are substituted by their value and any resulting
	///   fully-constant subexpression is folded to a literal, while a <c>(byte)(channel * scale)</c> over a
	///   conditional channel is reduced to <c>(byte)channel</c>. Sets <see cref="Failed" /> if a constant
	///   channel is used in a way that does not fold to a literal.
	/// </summary>
	private sealed class TrailingRewriter(
		Dictionary<string, double> constantValues,
		List<string> allTargets) : CSharpSyntaxRewriter
	{
		public bool Failed { get; private set; }

		public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
		{
			// (byte)(channel * scale) → (byte)channel for a conditional channel.
			if (Unwrap(node.Expression) is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multiply
			    && multiply.Left is IdentifierNameSyntax id
			    && allTargets.Contains(id.Identifier.Text)
			    && !constantValues.ContainsKey(id.Identifier.Text))
			{
				return node.WithExpression(id);
			}

			// (byte)(<folds to a constant>) → literal.
			if (TryEvaluate(node, out var value))
			{
				return ToLiteral(value);
			}

			return base.VisitCastExpression(node);
		}

		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (constantValues.TryGetValue(node.Identifier.Text, out var value))
			{
				// A bare constant channel that survives here (not folded by an enclosing cast) is something
				// this conservative pass does not model — bail rather than emit a half-substituted result.
				Failed = true;
				return NumericLiteral(value);
			}

			return base.VisitIdentifierName(node);
		}

		private bool TryEvaluate(ExpressionSyntax expression, out object value)
		{
			value = null!;

			switch (expression)
			{
				case CastExpressionSyntax cast when TryEvaluate(cast.Expression, out var inner):
				{
					return TryApplyCast(cast.Type, inner, out value);
				}
				case ParenthesizedExpressionSyntax parenthesized:
				{
					return TryEvaluate(parenthesized.Expression, out value);
				}
				case IdentifierNameSyntax identifier when constantValues.TryGetValue(identifier.Identifier.Text, out var constant):
				{
					value = constant;
					return true;
				}
				case BinaryExpressionSyntax binary when TryEvaluate(binary.Left, out var left) && TryEvaluate(binary.Right, out var right):
				{
					return TryApplyBinary(binary.Kind(), Convert.ToDouble(left), Convert.ToDouble(right), out value);
				}
				case LiteralExpressionSyntax literal when literal.Token.Value is not null:
				{
					value = literal.Token.Value;
					return value is double or float or int or long or decimal;
				}
			}

			return false;
		}

		private static bool TryApplyBinary(SyntaxKind kind, double left, double right, out object value)
		{
			value = kind switch
			{
				SyntaxKind.MultiplyExpression => left * right,
				SyntaxKind.AddExpression => left + right,
				SyntaxKind.SubtractExpression => left - right,
				SyntaxKind.DivideExpression => left / right,
				_ => Double.NaN
			};

			return !Double.IsNaN((double) value);
		}

		private static bool TryApplyCast(TypeSyntax type, object inner, out object value)
		{
			var number = Convert.ToDouble(inner);

			// Use C# cast (truncation toward zero) semantics, not Convert.* rounding.
			value = type switch
			{
				PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.ByteKeyword } => (byte) number,
				PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.IntKeyword } => (int) number,
				PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.LongKeyword } => (long) number,
				PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.DoubleKeyword } => number,
				_ => null!
			};

			return value is not null;
		}

		private static LiteralExpressionSyntax ToLiteral(object value)
		{
			return value switch
			{
				byte b => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(b)),
				int i => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)),
				long l => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(l)),
				double d => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(d)),
				_ => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Convert.ToDouble(value)))
			};
		}
	}
}