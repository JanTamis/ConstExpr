using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Helpers;

public sealed class BlockFormattingRewriter : CSharpSyntaxRewriter
{
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

		// Verplaats lokale functies naar het einde van de functie-/method-body
		// en verwijder ongebruikte lokale functies via een eenvoudige syntactische analyse.
		// (alleen voor top-level blokken van methoden/constructors/operators of lokale functies, niet voor willekeurige geneste blokken)
		if (node.Parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
		{
			var nonLocal = new List<StatementSyntax>(visited.Count);
			var locals = new List<LocalFunctionStatementSyntax>();

			foreach (var s in visited)
			{
				if (s is LocalFunctionStatementSyntax lfs)
				{
					locals.Add(lfs);
				}
				else
				{
					nonLocal.Add(s);
				}
			}

			if (locals.Count > 0)
			{
				// Bepaal welke lokale functies gebruikt worden
				var localNames = new HashSet<string>(StringComparer.Ordinal);

				foreach (var l in locals)
				{
					localNames.Add(l.Identifier.ValueText);
				}

				// Verzamel naam-gebruik vanuit niet-lokale statements (aanroepen en method groups)
				var usedFromNonLocal = new HashSet<string>(StringComparer.Ordinal);

				foreach (var st in nonLocal)
				{
					foreach (var id in st.DescendantNodes().OfType<IdentifierNameSyntax>())
					{
						var name = id.Identifier.ValueText;

						if (localNames.Contains(name))
						{
							usedFromNonLocal.Add(name);
						}
					}
				}

				// Bouw eenvoudige call graph tussen lokale functies (op basis van identifier-namen)
				var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

				foreach (var l in locals)
				{
					var from = l.Identifier.ValueText;
					var targets = new HashSet<string>(StringComparer.Ordinal);

					foreach (var id in l.DescendantNodes().OfType<IdentifierNameSyntax>())
					{
						var name = id.Identifier.ValueText;

						if (localNames.Contains(name) && !string.Equals(name, from, StringComparison.Ordinal))
						{
							targets.Add(name);
						}
					}
					edges[from] = targets;
				}

				// Bereken bereikbare lokale functies vanuit niet-lokale gebruiksplaatsen
				var used = new HashSet<string>(usedFromNonLocal, StringComparer.Ordinal);
				var stack = new Stack<string>(usedFromNonLocal);

				while (stack.Count > 0)
				{
					var u = stack.Pop();
					if (!edges.TryGetValue(u, out var targets) || targets is null)
						continue;

					foreach (var v in targets)
					{
						if (used.Add(v))
						{
							stack.Push(v);
						}
					}
				}

				// Houd alleen de gebruikte lokale functies over (in oorspronkelijke volgorde)
				var keptLocals = new List<LocalFunctionStatementSyntax>(locals.Count);

				foreach (var l in locals)
				{
					if (used.Contains(l.Identifier.ValueText))
					{
						keptLocals.Add(l);
					}
				}

				visited = nonLocal;

				if (keptLocals.Count > 0)
				{
					// Voeg locals aaneengesloten toe, en zorg voor spacing: 1 lege regel vóór de groep, geen lege regels tussen of na functies
					var firstLocalIdx = visited.Count;
					
					visited.AddRange(keptLocals);

					if (firstLocalIdx > 0)
					{
						visited[firstLocalIdx - 1] = EnsureTrailingBlankLine(visited[firstLocalIdx - 1]);
					}

					for (var j = firstLocalIdx; j < visited.Count; j++)
					{
						visited[j] = TrimLeadingBlankLinesTo(visited[j], 0);
						visited[j] = EnsureTrailingSingleNewLine(visited[j]);
					}
				}
			}
		}

		// Groepeer lokale declaraties en omring met lege regels
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is LocalDeclarationStatementSyntax)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is LocalDeclarationStatementSyntax);
			}
		}

		// Groepeer yield return statements
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is YieldStatementSyntax ys && ys.Kind() == SyntaxKind.YieldReturnStatement)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is YieldStatementSyntax ys2 && ys2.Kind() == SyntaxKind.YieldReturnStatement);
			}
		}

		// Control-flow en return spacing (lege regel voor/na waar passend)
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
				if (NeedsBlankLineBefore(visited[i - 1]))
				{
					visited[i - 1] = EnsureTrailingBlankLine(visited[i - 1]);
				}

				visited[i] = TrimLeadingBlankLinesTo(visited[i], 0);
			}

			if (isCtrlNonLocal && i < visited.Count - 1)
			{
				visited[i] = EnsureTrailingBlankLine(visited[i]);
			}
		}

		// Geen blanco regel voor de eerste statement
		visited[0] = TrimLeadingBlankLinesTo(visited[0], 0);

		var newNode = node.WithStatements(SyntaxFactory.List(visited));
		newNode = NormalizeOpenBraceTrailing(newNode);

		return newNode;
	}

	public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
	{
		var visited = (ReturnStatementSyntax) base.VisitReturnStatement(node);

		if (visited.Expression is null)
		{
			return visited;
		}

		// Zorg voor precies één spatie na 'return' als de expressie niet op een nieuwe regel staat
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
			// Voeg spatie toe
			rk = rk.WithTrailingTrivia(trailing.Add(SyntaxFactory.Space));
			visited = visited.WithReturnKeyword(rk);

			// Verwijder leading whitespace van eerste token van de expressie (anders dubbele spatie)
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

		// Speciaal geval: collectie-expressie – zorg dat er geen voorafgaande extra spaties zijn (al afgevangen door bovenstaande)
		if (visited.Expression is CollectionExpressionSyntax)
		{
			// (Extra normalisatie niet nodig; leading is al opgeschoond.)
		}

		return visited;
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

		if (start > 0 && NeedsBlankLineBefore(visited[start - 1]))
		{
			visited[start - 1] = EnsureTrailingBlankLine(visited[start - 1]);
		}

		visited[start] = TrimLeadingBlankLinesTo(visited[start], 0);

		if (end < visited.Count - 1)
		{
			visited[end] = EnsureTrailingBlankLine(visited[end]);
			visited[end + 1] = TrimLeadingBlankLinesTo(visited[end + 1], 0);
		}

		i = end;
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
}