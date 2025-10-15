using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Helpers;

public sealed class BlockFormattingRewriter : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (SyntaxHelpers.TryGetLiteral(node.Token.Value, out var expression))
		{
			return expression
				.WithLeadingTrivia(node.GetLeadingTrivia())
				.WithTrailingTrivia(node.GetTrailingTrivia());
		}

		return node;
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		// Visit child nodes first
		var visitedNode = base.VisitIfStatement(node);

		if (visitedNode is not IfStatementSyntax visited)
		{
			return visitedNode;
		}

		// Process the if-statement body
		if (visited.Statement is BlockSyntax { Statements.Count: 1 } ifBlock)
		{
			visited = visited.WithStatement(ifBlock.Statements[0]);
		}

		// Handle the else clause (including else-if)
		if (visited.Else is not null)
		{
			var elseClause = visited.Else;

			// If the else statement is a block with a single statement (and not a nested if)
			if (elseClause.Statement is BlockSyntax { Statements.Count: 1 } elseBlock &&
				elseBlock.Statements[0] is not IfStatementSyntax)
			{
				visited = visited.WithElse(elseClause.WithStatement(elseBlock.Statements[0]));
			}
		}

		return visited;
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = new List<StatementSyntax>(node.Statements.Count);

		foreach (var stmt in node.Statements)
		{
			if (Visit(stmt) is StatementSyntax statement)
			{
				visited.Add(statement);
			}
		}

		if (visited.Count == 0)
		{
			return node;
		}

		// Merge simple declaration + next assignment patterns
		CombineSimpleDeclarationWithAssignment(visited);

		// Group local declarations and surround with blank lines
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is LocalDeclarationStatementSyntax)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is LocalDeclarationStatementSyntax);
			}
		}

		// Group yield return statements
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is YieldStatementSyntax ys && ys.Kind() == SyntaxKind.YieldReturnStatement)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is YieldStatementSyntax ys2 && ys2.Kind() == SyntaxKind.YieldReturnStatement);
			}
		}

		// Group expression statements (e.g., FMA chains)
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is ExpressionStatementSyntax)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is ExpressionStatementSyntax);
			}
		}

		// Control-flow and return spacing (blank line before/after where appropriate)
		for (var i = 0; i < visited.Count; i++)
		{
			var current = visited[i];
			var isLocalFunc = current is LocalFunctionStatementSyntax;
			var isCtrlNonLocal = !isLocalFunc && IsTarget(current);
			var isReturn = current is ReturnStatementSyntax;

			if (!isCtrlNonLocal && !isReturn)
			{
				continue;
			}

			if (i > 0)
			{
				var prev = visited[i - 1];

				// Don't add blank line if previous statement has a comment that belongs to current statement
				if (!HasTrailingCommentForNext(prev) && NeedsBlankLineBefore(prev))
				{
					visited[i - 1] = EnsureTrailingBlankLine(prev);
				}

				visited[i] = TrimLeadingBlankLinesTo(visited[i], 0);
			}

			if (isCtrlNonLocal && i < visited.Count - 1)
			{
				// Only add blank line after control flow if next statement doesn't start with a comment
				var next = visited[i + 1];
				if (!HasLeadingComment(next))
				{
					visited[i] = EnsureTrailingBlankLine(visited[i]);
				}
			}
		}

		// Ensure exactly one blank line before comment-only statements (so a blank line precedes comments)
		for (var i = 0; i < visited.Count; i++)
		{
			if (HasLeadingComment(visited[i]))
			{
				if (i == 0)
				{
					// First statement in block: limit multiple leading blank lines to at most 1
					visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
				}
				else
				{
					// Ensure there is a full blank line (2 EOLs) before the comment line
					visited[i - 1] = EnsureTrailingBlankLine(visited[i - 1]);
					visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
				}
			}
		}

		// No blank line before the first statement
		visited[0] = TrimLeadingBlankLinesTo(visited[0], 0);

		var newNode = node.WithStatements(SyntaxFactory.List(visited));
		newNode = NormalizeOpenBraceTrailing(newNode);

		return newNode;
	}

	private static void CombineSimpleDeclarationWithAssignment(List<StatementSyntax> visited)
	{
		// Combine simple pattern: single-variable declaration without initializer
		// immediately followed by a simple assignment to that variable, e.g.
		// "int x; x = expr;" -> "int x = expr;"
		for (var i = 0; i < visited.Count - 1; i++)
		{
			if (visited[i] is not LocalDeclarationStatementSyntax declStmt)
			{
				continue;
			}

			var decl = declStmt.Declaration;
			if (decl is null || decl.Variables.Count != 1 || decl.Variables[0].Initializer is not null)
			{
				continue;
			}

			var varId = decl.Variables[0].Identifier.ValueText;
			if (string.IsNullOrEmpty(varId) || visited[i + 1] is not ExpressionStatementSyntax exprStmt)
			{
				continue;
			}

			if (exprStmt.Expression is not AssignmentExpressionSyntax assign || assign.Kind() != SyntaxKind.SimpleAssignmentExpression)
			{
				continue;
			}

			// Ensure left side is the same identifier
			if (assign.Left is not IdentifierNameSyntax idLeft || idLeft.Identifier.ValueText != varId)
			{
				continue;
			}

			var rightExpr = assign.Right;
			var firstToken = rightExpr.GetFirstToken();
			var leading = firstToken.LeadingTrivia;
			var idx = 0;
			while (idx < leading.Count && leading[idx].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				idx++;
			}

			if (idx > 0)
			{
				var newLeading = SyntaxFactory.TriviaList();
				for (var k = idx; k < leading.Count; k++)
				{
					newLeading = newLeading.Add(leading[k]);
				}

				rightExpr = rightExpr.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(newLeading));
			}

			var newVar = decl.Variables[0].WithInitializer(SyntaxFactory.EqualsValueClause(rightExpr));
			var newDecl = decl.WithVariables(SyntaxFactory.SingletonSeparatedList(newVar));
			var newDeclStmt = declStmt.WithDeclaration(newDecl)
				.WithTrailingTrivia(exprStmt.GetTrailingTrivia());

			// Replace declaration and remove the assignment statement
			visited[i] = newDeclStmt;
			visited.RemoveAt(i + 1);
			// Step back to re-evaluate around the modified area
			i = Math.Max(-1, i - 1);
		}
	}

	private static void ApplyControlFlowAndReturnSpacing(List<StatementSyntax> visited)
	{
		for (var i = 0; i < visited.Count; i++)
		{
			var current = visited[i];
			var isLocalFunc = current is LocalFunctionStatementSyntax;
			var isCtrlNonLocal = !isLocalFunc && IsTarget(current);
			var isReturn = current is ReturnStatementSyntax;

			if (!isCtrlNonLocal && !isReturn)
			{
				continue;
			}

			if (i > 0)
			{
				var prev = visited[i - 1];
				if (!HasTrailingCommentForNext(prev) && NeedsBlankLineBefore(prev))
				{
					visited[i - 1] = EnsureTrailingBlankLine(prev);
				}
				visited[i] = TrimLeadingBlankLinesTo(visited[i], 0);
			}

			if (isCtrlNonLocal && i < visited.Count - 1)
			{
				var next = visited[i + 1];
				if (!HasLeadingComment(next))
				{
					visited[i] = EnsureTrailingBlankLine(visited[i]);
				}
			}
		}
	}

	private static void EnsureCommentLineSpacing(List<StatementSyntax> visited)
	{
		for (var i = 0; i < visited.Count; i++)
		{
			if (!HasLeadingComment(visited[i]))
			{
				continue;
			}

			if (i == 0)
			{
				visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
			}
			else
			{
				visited[i - 1] = EnsureTrailingBlankLine(visited[i - 1]);
				visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
			}
		}
	}

	public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
	{
		var visited = (ReturnStatementSyntax)base.VisitReturnStatement(node);

		if (visited.Expression is null)
		{
			return visited;
		}

		// Ensure exactly one space after 'return' when the expression is not on a new line
		var rk = visited.ReturnKeyword;
		var trailing = rk.TrailingTrivia;
		var hasWhitespace = false;
		var hasEol = false;

		foreach (var t in trailing)
		{
			if (t.IsKind(SyntaxKind.WhitespaceTrivia))
			{
				hasWhitespace = true;
			}
			else if (t.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				hasEol = true;
				break;
			}
		}

		if (!hasWhitespace && !hasEol)
		{
			// Add a space
			rk = rk.WithTrailingTrivia(trailing.Add(SyntaxFactory.Space));
			visited = visited.WithReturnKeyword(rk);

			// Remove leading whitespace from the expression's first token (to avoid double spaces)
			var firstToken = visited.Expression.GetFirstToken();
			var leading = firstToken.LeadingTrivia;
			var idx = 0;

			while (idx < leading.Count && leading[idx].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				idx++;
			}

			if (idx > 0)
			{
				var newLeading = SyntaxFactory.TriviaList();

				for (var i = idx; i < leading.Count; i++)
				{
					newLeading = newLeading.Add(leading[i]);
				}

				visited = visited.WithExpression(visited.Expression.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(newLeading)));
			}
		}

		// Special case: collection expression — ensure no extra leading spaces (already handled above)
		if (visited.Expression is CollectionExpressionSyntax)
		{
			// (No extra normalization needed; leading trivia already cleaned.)
		}

		return visited;
	}

	public override SyntaxNode VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		// Rewrite the ?: operator across multiple lines and account for nesting.
		// Desired layout:
		// condition
		// \t? whenTrue
		// \t: whenFalse

		var v = (ConditionalExpressionSyntax)base.VisitConditionalExpression(node);

		// Tokens we will adjust
		var conditionLast = v.Condition.GetLastToken();
		var question = v.QuestionToken;
		var whenTrueFirst = v.WhenTrue.GetFirstToken();
		var whenTrueLast = v.WhenTrue.GetLastToken();
		var colon = v.ColonToken;
		var whenFalseFirst = v.WhenFalse.GetFirstToken();

		// Determine the indent based on the surrounding statement and use the same unit as NormalizeWhitespace (\t)
		var indentUnit = SyntaxFactory.Whitespace("\t");

		// Use the condition node's indent as a base so '?' and ':' are indented
		// one tab relative to the condition line.
		var baseIndent = GetBaseIndentTrivia(v.Condition);

		var lineIndent = BuildLineIndent(baseIndent, indentUnit);

		// 1) After condition: force a new line and indent
		var newConditionLast = ForceNewLineAndIndentAfter(conditionLast, indentUnit);

		// 2) Rewrite '?' leading and trailing trivia
		var newQuestion = WithLeadingIndentPreserveNonWhitespace(question, lineIndent);
		newQuestion = WithTrailingSpaceOnly(newQuestion);

		// 3) whenTrue immediately after '? '
		var wtFirst = TrimLeadingTrivia(whenTrueFirst);

		// 4) Force newline + indent after whenTrue
		var newWhenTrueLast = ForceNewLineAndIndentAfter(whenTrueLast, indentUnit);

		// 5) Rewrite ':' leading and trailing trivia
		var newColon = WithLeadingIndentPreserveNonWhitespace(colon, lineIndent);
		newColon = WithTrailingSpaceOnly(newColon);

		// 6) whenFalse immediately after ': '
		var wfFirst = TrimLeadingTrivia(whenFalseFirst);

		// Replace tokens in a single operation
		var tokens = new[] { conditionLast, question, whenTrueFirst, whenTrueLast, colon, whenFalseFirst };
		var map = new Dictionary<SyntaxToken, SyntaxToken>
		{
			[conditionLast] = newConditionLast,
			[question] = newQuestion,
			[whenTrueFirst] = wtFirst,
			[whenTrueLast] = newWhenTrueLast,
			[colon] = newColon,
			[whenFalseFirst] = wfFirst,
		};

		v = v.ReplaceTokens(tokens, (orig, _) => map.TryGetValue(orig, out var rep) ? rep : orig);

		return v;
	}

	private static SyntaxTriviaList BuildLineIndent(SyntaxTriviaList baseIndent, SyntaxTrivia indentUnit)
	{
		var list = SyntaxFactory.TriviaList();
		for (var i = 0; i < baseIndent.Count; i++)
		{
			list = list.Add(baseIndent[i]);
		}
		list = list.Add(indentUnit);
		return list;
	}

	private static SyntaxToken ForceNewLineAndIndentAfter(SyntaxToken token, SyntaxTrivia indentUnit)
	{
		var trailing = TrimTrailingWhitespaceAndEndOfLines(token.TrailingTrivia)
			.Add(SyntaxFactory.ElasticCarriageReturnLineFeed)
			.Add(indentUnit);
		return token.WithTrailingTrivia(trailing);
	}

	private static SyntaxToken WithLeadingIndentPreserveNonWhitespace(SyntaxToken token, SyntaxTriviaList lineIndent)
	{
		var rest = TrimLeadingWhitespaceAndEndOfLines(token.LeadingTrivia);
		var leading = SyntaxFactory.TriviaList();
		for (var i = 0; i < lineIndent.Count; i++)
		{
			leading = leading.Add(lineIndent[i]);
		}
		foreach (var tr in rest)
		{
			leading = leading.Add(tr);
		}
		return token.WithLeadingTrivia(leading);
	}

	private static SyntaxToken WithTrailingSpaceOnly(SyntaxToken token)
	{
		var trailing = TrimTrailingWhitespaceAndEndOfLines(token.TrailingTrivia).Add(SyntaxFactory.Space);
		return token.WithTrailingTrivia(trailing);
	}

	private static SyntaxToken TrimLeadingTrivia(SyntaxToken token)
	{
		return token.WithLeadingTrivia(TrimLeadingWhitespaceAndEndOfLines(token.LeadingTrivia));
	}

	private static void SurroundContiguousGroup(List<StatementSyntax> visited, ref int i, Func<StatementSyntax, bool> isInGroup)
	{
		var start = i;
		var end = i;

		for (var j = i + 1; j < visited.Count; j++)
		{
			if (isInGroup(visited[j]))
			{
				end = j;
				continue;
			}
			break;
		}

		if (start > 0 && NeedsBlankLineBefore(visited[start - 1]) &&
			!ShouldSkipBlankBeforeGroup(visited, start, isInGroup))
		{
			visited[start - 1] = EnsureTrailingBlankLine(visited[start - 1]);
		}

		visited[start] = TrimLeadingBlankLinesTo(visited[start], 0);

		if (end < visited.Count - 1 && ShouldAddBlankAfterGroup(visited, start, end, isInGroup))
		{
			visited[end] = EnsureTrailingBlankLine(visited[end]);
			visited[end + 1] = TrimLeadingBlankLinesTo(visited[end + 1], 0);
		}

		i = end;
	}

	private static bool ShouldSkipBlankBeforeGroup(List<StatementSyntax> visited, int start, Func<StatementSyntax, bool> isInGroup)
	{
		if (!(isInGroup(visited[start]) && visited[start] is ExpressionStatementSyntax) || start <= 0)
		{
			return false;
		}

		var prev = visited[start - 1];
		if (prev is not LocalDeclarationStatementSyntax)
		{
			return false;
		}

		// Gather declared ids from the contiguous declaration block that ends at prev
		var ids = new HashSet<string>(StringComparer.Ordinal);
		for (var k = start - 1; k >= 0; k--)
		{
			if (visited[k] is LocalDeclarationStatementSyntax ls && ls.Declaration is { } decl)
			{
				foreach (var v in decl.Variables)
				{
					ids.Add(v.Identifier.ValueText);
				}
			}
			else
			{
				break;
			}
		}

		// Check first statement of the group
		if (visited[start] is ExpressionStatementSyntax first &&
			first.Expression is AssignmentExpressionSyntax assign &&
			assign.Kind() == SyntaxKind.SimpleAssignmentExpression &&
			assign.Left is IdentifierNameSyntax leftId &&
			ids.Contains(leftId.Identifier.ValueText))
		{
			return true;
		}

		return false;
	}

	private static bool ShouldAddBlankAfterGroup(List<StatementSyntax> visited, int start, int end, Func<StatementSyntax, bool> isInGroup)
	{
		// If this is a group of local declarations and the next statement is a simple
		// assignment to one of the declared vars, do NOT add a blank line.
		if (isInGroup(visited[start]) && visited[start] is LocalDeclarationStatementSyntax)
		{
			var ids = new HashSet<string>(StringComparer.Ordinal);
			for (var k = start; k <= end; k++)
			{
				if (visited[k] is LocalDeclarationStatementSyntax ls && ls.Declaration is { } decl)
				{
					foreach (var v in decl.Variables)
					{
						ids.Add(v.Identifier.ValueText);
					}
				}
			}

			var next = visited[end + 1];
			if (next is ExpressionStatementSyntax es &&
				es.Expression is AssignmentExpressionSyntax assign &&
				assign.Kind() == SyntaxKind.SimpleAssignmentExpression &&
				assign.Left is IdentifierNameSyntax leftId &&
				ids.Contains(leftId.Identifier.ValueText))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsTarget(StatementSyntax s) => s is IfStatementSyntax
		or ForStatementSyntax
		or ForEachStatementSyntax
		or ForEachVariableStatementSyntax
		or WhileStatementSyntax
		or DoStatementSyntax
		or SwitchStatementSyntax
		or UsingStatementSyntax
		or LockStatementSyntax
		or TryStatementSyntax
		or FixedStatementSyntax
		or LocalFunctionStatementSyntax
		or CheckedStatementSyntax
		or UnsafeStatementSyntax;

	private static bool NeedsBlankLineBefore(StatementSyntax previous)
	{
		var trailing = previous.GetTrailingTrivia();
		return CountTrailingNewLines(trailing) < 2;
	}

	private static StatementSyntax EnsureTrailingBlankLine(StatementSyntax statement)
	{
		var trailing = statement.GetTrailingTrivia();
		var newlineCount = CountTrailingNewLines(trailing);

		if (newlineCount >= 2)
		{
			return statement;
		}

		var list = trailing;

		for (var k = 0; k < 2 - newlineCount; k++)
		{
			list = list.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
		}

		return statement.WithTrailingTrivia(list);
	}

	private static StatementSyntax EnsureTrailingSingleNewLine(StatementSyntax statement)
	{
		var trailing = statement.GetTrailingTrivia();
		var eols = CountTrailingNewLines(trailing);

		if (eols == 1)
		{
			return statement;
		}

		// Verwijder alle trailing whitespace/eols en voeg precies 1 EOL toe
		var idx = trailing.Count - 1;

		while (idx >= 0 && (trailing[idx].IsKind(SyntaxKind.EndOfLineTrivia) || trailing[idx].IsKind(SyntaxKind.WhitespaceTrivia)))
		{
			idx--;
		}
		var newTrailing = SyntaxFactory.TriviaList();

		for (var i = 0; i <= idx; i++)
		{
			newTrailing = newTrailing.Add(trailing[i]);
		}
		newTrailing = newTrailing.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
		return statement.WithTrailingTrivia(newTrailing);
	}

	private static StatementSyntax TrimLeadingBlankLinesTo(StatementSyntax statement, int maxEols)
	{
		var leading = statement.GetLeadingTrivia();
		var idx = 0;
		var eolCount = 0;

		while (idx < leading.Count && leading[idx].IsKind(SyntaxKind.EndOfLineTrivia))
		{
			eolCount++;
			idx++;
		}

		if (eolCount <= maxEols)
		{
			return statement;
		}

		var newLeading = new List<SyntaxTrivia>(maxEols + (leading.Count - idx));

		for (var i = 0; i < maxEols; i++)
		{
			newLeading.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
		}

		for (var i = idx; i < leading.Count; i++)
		{
			newLeading.Add(leading[i]);
		}

		return statement.WithLeadingTrivia(SyntaxFactory.TriviaList(newLeading));
	}

	private static int CountTrailingNewLines(SyntaxTriviaList trailing)
	{
		var count = 0;

		for (var i = trailing.Count - 1; i >= 0; i--)
		{
			if (trailing[i].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				count++;
			}
			else if (trailing[i].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				continue;
			}
			else
			{
				break;
			}
		}
		return count;
	}

	private static BlockSyntax NormalizeOpenBraceTrailing(BlockSyntax block)
	{
		var open = block.OpenBraceToken;
		var trailing = open.TrailingTrivia;
		var lastContentIdx = trailing.Count - 1;

		while (lastContentIdx >= 0 && (trailing[lastContentIdx].IsKind(SyntaxKind.EndOfLineTrivia) || trailing[lastContentIdx].IsKind(SyntaxKind.WhitespaceTrivia)))
		{
			lastContentIdx--;
		}

		if (lastContentIdx >= 0 && lastContentIdx == trailing.Count - 2 && trailing[^1].IsKind(SyntaxKind.EndOfLineTrivia))
		{
			return block;
		}

		var newTrailing = new List<SyntaxTrivia>(lastContentIdx + 2);

		for (var i = 0; i <= lastContentIdx; i++)
		{
			newTrailing.Add(trailing[i]);
		}

		newTrailing.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);

		return block.WithOpenBraceToken(open.WithTrailingTrivia(SyntaxFactory.TriviaList(newTrailing)));
	}

	private static SyntaxTriviaList TrimTrailingWhitespaceAndEndOfLines(SyntaxTriviaList list)
	{
		var idx = list.Count - 1;
		while (idx >= 0 && (list[idx].IsKind(SyntaxKind.EndOfLineTrivia) || list[idx].IsKind(SyntaxKind.WhitespaceTrivia)))
		{
			idx--;
		}
		var result = SyntaxFactory.TriviaList();
		for (var i = 0; i <= idx; i++)
		{
			result = result.Add(list[i]);
		}
		return result;
	}

	private static SyntaxTriviaList TrimLeadingWhitespaceAndEndOfLines(SyntaxTriviaList list)
	{
		var idx = 0;
		while (idx < list.Count && (list[idx].IsKind(SyntaxKind.EndOfLineTrivia) || list[idx].IsKind(SyntaxKind.WhitespaceTrivia)))
		{
			idx++;
		}
		var result = SyntaxFactory.TriviaList();
		for (var i = idx; i < list.Count; i++)
		{
			result = result.Add(list[i]);
		}
		return result;
	}

	private static SyntaxTriviaList GetBaseIndentTrivia(SyntaxNode node)
	{
		// Find the nearest statement and use the indentation of the first token after the last
		// line break. Prefer the node's own leading trivia when it contains an EOL, otherwise
		// fall back to the statement token. This retains indentation of broken expressions.
		var stmt = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();

		// Prefer the node's own first token first
		var token = node.GetFirstToken();
		var leading = token.LeadingTrivia;
		var lastEol = IndexOfLastEol(leading);

		// If the node has no EOL in its leading trivia, fall back to the statement token
		// only when the token has no leading whitespace/EOL at all. This gives priority to
		// indentation directly before the condition when it is on the same line.
		var hasLeadingWhitespaceOrEol = HasWhitespaceOrEol(leading);
		if (!hasLeadingWhitespaceOrEol && lastEol < 0 && stmt is not null)
		{
			token = stmt.GetFirstToken();
			leading = token.LeadingTrivia;
			lastEol = IndexOfLastEol(leading);
		}

		if (lastEol < 0)
		{
			// No EOL: take only initial whitespace
			var result = SyntaxFactory.TriviaList();
			for (var i = 0; i < leading.Count; i++)
			{
				if (leading[i].IsKind(SyntaxKind.WhitespaceTrivia))
				{
					result = result.Add(leading[i]);
				}
				else
				{
					result = SyntaxFactory.TriviaList(); // reset if comments/other trivia appear
				}
			}
			return result;
		}
		else
		{
			return IndentAfterLastEol(leading, lastEol);
		}
	}

	private static int IndexOfLastEol(SyntaxTriviaList list)
	{
		var lastEol = -1;
		for (var i = 0; i < list.Count; i++)
		{
			if (list[i].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				lastEol = i;
			}
		}
		return lastEol;
	}

	private static bool HasWhitespaceOrEol(SyntaxTriviaList list)
	{
		for (var i = 0; i < list.Count; i++)
		{
			if (list[i].IsKind(SyntaxKind.WhitespaceTrivia) || list[i].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				return true;
			}
		}
		return false;
	}

	private static SyntaxTriviaList IndentAfterLastEol(SyntaxTriviaList leading, int lastEol)
	{
		var result = SyntaxFactory.TriviaList();
		for (var i = lastEol + 1; i < leading.Count; i++)
		{
			if (leading[i].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				result = result.Add(leading[i]);
			}
			else
			{
				break;
			}
		}
		return result;
	}

	private static bool HasLeadingComment(StatementSyntax statement)
	{
		var leading = statement.GetLeadingTrivia();
		foreach (var trivia in leading)
		{
			if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasTrailingCommentForNext(StatementSyntax statement)
	{
		var trailing = statement.GetTrailingTrivia();
		var foundEol = false;

		foreach (var trivia in trailing)
		{
			if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				foundEol = true;
			}
			else if (foundEol && (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
														 trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)))
			{
				return true;
			}
		}
		return false;
	}
}

