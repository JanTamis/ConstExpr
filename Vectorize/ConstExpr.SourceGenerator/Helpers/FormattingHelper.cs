using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Helpers;

internal static class FormattingHelper
{
	public static SyntaxNode Format(SyntaxNode node)
	{
		var rewriter = new BlockFormattingRewriter();
		return rewriter.Visit(node.NormalizeWhitespace("\t"));
	}

	public static string Render(SyntaxNode node) => Format(node).ToFullString();

	private sealed class BlockFormattingRewriter : CSharpSyntaxRewriter
	{
		// Lege regel vóór en na elke control-flow statement; geen extra na laatste; ook lege regel vóór return (indien niet eerste en nodig).
		public override SyntaxNode VisitBlock(BlockSyntax node)
		{
			var visited = new List<StatementSyntax>(node.Statements.Count);

			foreach (var stmt in node.Statements)
			{
				visited.Add((StatementSyntax) Visit(stmt)!);
			}

			if (visited.Count == 0)
			{
				return node;
			}

			for (var i = 0; i < visited.Count; i++)
			{
				var current = visited[i];
				var isCtrl = IsTarget(current);
				var isReturn = current is ReturnStatementSyntax;

				if (!isCtrl && !isReturn)
				{
					continue;
				}

				// Voor: lege regel na vorige statement (dus vóór huidige) tenzij eerste.
				if (i > 0)
				{
					if (NeedsBlankLineBefore(visited[i - 1]))
					{
						visited[i - 1] = EnsureTrailingBlankLine(visited[i - 1]);
					}
					
					// Trim overmaat leading op huidige
					visited[i] = TrimLeadingBlankLines(visited[i]);
				}

				// Na alleen voor control-flow (niet voor return) en niet als laatste.
				if (isCtrl && i < visited.Count - 1)
				{
					visited[i] = EnsureTrailingBlankLine(visited[i]);
				}
			}

			return node.WithStatements(SyntaxFactory.List(visited));
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
			or FixedStatementSyntax;

		private static bool NeedsBlankLineBefore(StatementSyntax previous)
		{
			var trailing = previous.GetTrailingTrivia();
			return CountTrailingNewLines(trailing) < 2; // minder dan twee newlines => geen lege regel
		}

		private static StatementSyntax EnsureTrailingBlankLine(StatementSyntax statement)
		{
			var trailing = statement.GetTrailingTrivia();
			var newlineCount = CountTrailingNewLines(trailing);
			
			if (newlineCount >= 2)
			{
				return statement;
			}
			
			var needed = 2 - newlineCount;
			var list = trailing;
			
			for (var i = 0; i < needed; i++)
			{
				list = list.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
			}
			
			return statement.WithTrailingTrivia(list);
		}

		private static StatementSyntax TrimLeadingBlankLines(StatementSyntax statement)
		{
			var leading = statement.GetLeadingTrivia();
			var eolCount = 0;
			var idx = 0;

			while (idx < leading.Count && leading[idx].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				eolCount++;
				idx++;
			}
			
			if (eolCount <= 2)
			{
				return statement;
			}
			
			var newLeading = new List<SyntaxTrivia>(leading.Count - (eolCount - 2))
			{
				SyntaxFactory.ElasticCarriageReturnLineFeed,
				SyntaxFactory.ElasticCarriageReturnLineFeed
			};

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
	}
}