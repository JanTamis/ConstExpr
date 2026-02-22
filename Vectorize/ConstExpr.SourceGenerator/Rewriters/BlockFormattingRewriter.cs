using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class BlockFormattingRewriter : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		if (node.Expression is LiteralExpressionSyntax)
		{
			return null;
		}

		var result = Visit(node.Expression);

		if (result is null)
		{
			return null;
		}

		return node.WithExpression(result as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (node.Left is LiteralExpressionSyntax)
		{
			return null;
		}

		return base.VisitAssignmentExpression(node);
	}

	public override SyntaxNode? VisitIsPatternExpression(IsPatternExpressionSyntax node)
	{
		var visited = (IsPatternExpressionSyntax?) base.VisitIsPatternExpression(node);

		if (visited is null)
		{
			return visited;
		}

		// Ensure there's a trailing space after the 'is' keyword
		var isKeyword = visited.IsKeyword;

		if (!isKeyword.TrailingTrivia.Any(SyntaxKind.WhitespaceTrivia))
		{
			// Add a single space after 'is'
			visited = visited.WithIsKeyword(isKeyword.WithTrailingTrivia(SyntaxFactory.Space));
		}

		return visited;
	}

	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		var visited = (SimpleLambdaExpressionSyntax?) base.VisitSimpleLambdaExpression(node);

		if (visited is null)
		{
			return visited;
		}

		// Check if the lambda body is a simple invocation where the parameter is passed as the only argument
		// Pattern: w => Method(w) -> Method
		if (visited.ExpressionBody is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation)
		{
			// Check if there's exactly one argument and it matches the lambda parameter
			var arg = invocation.ArgumentList.Arguments[0];

			if (arg.Expression is IdentifierNameSyntax identifier &&
			    identifier.Identifier.ValueText == visited.Parameter.Identifier.ValueText &&
			    arg.NameColon is null &&
			    arg.RefKindKeyword.IsKind(SyntaxKind.None))
			{
				// Return just the method expression (e.g., Double.IsOddInteger)
				return invocation.Expression
					.WithLeadingTrivia(visited.GetLeadingTrivia())
					.WithTrailingTrivia(visited.GetTrailingTrivia());
			}
		}

		return visited;
	}

	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (node.IsKind(SyntaxKind.DefaultLiteralExpression))
		{
			return node;
		}

		if (SyntaxHelpers.TryGetLiteral(node.Token.Value, out var expression))
		{
			return (node.Token.Value switch
			{
				Math.PI => SyntaxFactory.ParseExpression("Double.Pi"),
				Math.PI * 2 => SyntaxFactory.ParseExpression("Double.Tau"),
				Math.E => SyntaxFactory.ParseExpression("Double.E"),
				MathF.PI => SyntaxFactory.ParseExpression("Single.Pi"),
				MathF.PI * 2 => SyntaxFactory.ParseExpression("Single.Tau"),
				MathF.E => SyntaxFactory.ParseExpression("Single.E"),
				Double.Epsilon => SyntaxFactory.ParseExpression("Double.Epsilon"),
				Single.Epsilon => SyntaxFactory.ParseExpression("Single.Epsilon"),
				_ => IsHexOrBinaryLiteral(node.Token) ? node : expression,
			}).WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());
		}

		return node;
	}

	private static bool IsHexOrBinaryLiteral(SyntaxToken token)
	{
		var text = token.Text;
		return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || 
		       text.StartsWith("0X", StringComparison.OrdinalIgnoreCase) ||
		       text.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ||
		       text.StartsWith("0B", StringComparison.OrdinalIgnoreCase);
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		// Visit child nodes first
		var visitedNode = base.VisitIfStatement(node);

		if (visitedNode is not IfStatementSyntax visited)
		{
			return visitedNode;
		}

		// Check if the if body is empty
		var ifBodyIsEmpty = IsStatementEmpty(visited.Statement);

		// Check if else clause exists and is empty
		var elseClauseEmpty = false;

		if (visited.Else is not null)
		{
			elseClauseEmpty = IsStatementEmpty(visited.Else.Statement);
		}

		switch (ifBodyIsEmpty)
		{
			// If the entire if statement is empty (if body empty and no else, or if body empty and else empty)
			case true when (visited.Else is null || elseClauseEmpty):
				// Remove the entire if statement
				return null;
			// If if body is empty but else exists and is not empty
			case true when visited.Else is not null && !elseClauseEmpty:
			{
				// Transform: if (condition) { } else { statements } -> if (!condition) { statements }
				var negatedCondition = NegateCondition(visited.Condition);

				return visited
					.WithCondition(negatedCondition)
					.WithStatement(visited.Else.Statement)
					.WithElse(null);
			}
		}

		// If else clause is empty, just remove it
		if (elseClauseEmpty && visited.Else is not null)
		{
			visited = visited.WithElse(null);
		}

		return visited;
	}

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		return base.VisitLocalFunctionStatement(node)?.WithoutLeadingTrivia();
	}

	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		return base.VisitMethodDeclaration(node)
			.WithoutLeadingTrivia();
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = new List<StatementSyntax>(node.Statements.Count);

		visited.AddRange(node.Statements
			.Select(Visit)
			.OfType<StatementSyntax>());

		if (visited.Count == 0)
		{
			if (node.Parent is LocalFunctionStatementSyntax or MethodDeclarationSyntax)
			{
				return node.WithStatements(SyntaxFactory.List(visited));
			}

			return null;
		}

		// Combine simple patterns: single-variable local declaration without initializer
		// immediately followed by a simple assignment to that variable, e.g.
		// "int x; x = expr;" -> "int x = expr;"
		for (var i = 0; i < visited.Count - 1; i++)
		{
			if (visited[i] is LocalDeclarationStatementSyntax declStmt)
			{
				var decl = declStmt.Declaration;

				// Only handle single-variable declarations without initializer
				if (decl?.Variables is [ { Initializer: null } ])
				{
					var varId = decl.Variables[0].Identifier.ValueText;

					if (!String.IsNullOrEmpty(varId))
					{
						if (visited[i + 1] is ExpressionStatementSyntax exprStmt)
						{
							if (exprStmt.Expression is AssignmentExpressionSyntax assign &&
							    assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
							{
								// Check left side is the same identifier (allow parentheses/trivia by getting IdentifierName)
								if (assign.Left is IdentifierNameSyntax idLeft && idLeft.Identifier.ValueText == varId)
								{
									// Create a new variable declarator with initializer from assignment.Right
									// Preserve leading trivia on the right-hand expression's first token by attaching it to the initializer expression
									var rightExpr = assign.Right;

									// If the RHS's first token has leading whitespace we will keep it; remove an initial space if it would duplicate formatting
									var firstToken = rightExpr.GetFirstToken();
									var newFirstToken = firstToken; // default

									// Trim leading whitespace-only trivia to avoid double spaces after the '='
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

										newFirstToken = firstToken.WithLeadingTrivia(newLeading);
										rightExpr = rightExpr.ReplaceToken(firstToken, newFirstToken);
									}

									var newVar = decl.Variables[0].WithInitializer(SyntaxFactory.EqualsValueClause(rightExpr));
									var newDecl = decl.WithVariables(SyntaxFactory.SingletonSeparatedList(newVar));

									// Preserve any trivia from the assignment's statement (e.g. trailing comments) by moving trailing trivia
									// from the exprStmt to the new declaration's semicolon/trailing trivia.
									var newDeclStmt = declStmt.WithDeclaration(newDecl)
										.WithTrailingTrivia(exprStmt.GetTrailingTrivia());

									// Replace declStmt and remove the assignment statement
									visited[i] = newDeclStmt;
									visited.RemoveAt(i + 1);
									// Step back one position to re-evaluate around the modified area
									i = Math.Max(-1, i - 1);
								}
							}
						}
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
			if (visited[i] is YieldStatementSyntax ys && ys.IsKind(SyntaxKind.YieldReturnStatement))
			{
				SurroundContiguousGroup(visited, ref i, static s => s is YieldStatementSyntax ys2 && ys2.IsKind(SyntaxKind.YieldReturnStatement));
			}
		}

		// Groepeer expression statements (zoals FMA chains)
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is ExpressionStatementSyntax)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is ExpressionStatementSyntax);
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

		// Zorg voor precies één lege regel vóór commentaarregels (zodat er een witregel boven commentaar komt)
		for (var i = 0; i < visited.Count; i++)
		{
			if (HasLeadingComment(visited[i]))
			{
				if (i == 0)
				{
					// Eerste statement in blok: beperk eventueel meerdere leidende lege regels tot maximaal 1
					visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
				}
				else
				{
					// Zorg dat er een volledige blanco regel (2 EOLs) komt vóór de commentaarregel
					visited[i - 1] = EnsureTrailingBlankLine(visited[i - 1]);
					visited[i] = TrimLeadingBlankLinesTo(visited[i], 1);
				}
			}
		}

		// Geen blanco regel voor de eerste statement
		visited[0] = TrimLeadingBlankLinesTo(visited[0], 0);

		var newNode = node.WithStatements(SyntaxFactory.List(visited));
		newNode = NormalizeOpenBraceTrailing(newNode);

		return newNode;
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		var visited = base.VisitReturnStatement(node) as ReturnStatementSyntax;

		if (visited?.Expression is null)
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
			var firstToken = visited.Expression!.GetFirstToken();
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

	// New: normalize object creation spacing so `new Type (` -> `new Type(`
	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		var result = base.VisitObjectCreationExpression(node);

		if (result is ObjectCreationExpressionSyntax)
		{
			return node.WithType(node.Type.WithTrailingTrivia());
		}

		return result;
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		var result = base.VisitInvocationExpression(node) as InvocationExpressionSyntax ?? node;

		if (result.Expression is MemberAccessExpressionSyntax { Expression: PredefinedTypeSyntax predefinedTypeSyntax } memberAccess)
		{
			var fullTypeName = predefinedTypeSyntax.Keyword.Kind() switch
			{
				SyntaxKind.VoidKeyword => "Void",
				SyntaxKind.BoolKeyword => "Boolean",
				SyntaxKind.ByteKeyword => "Byte",
				SyntaxKind.SByteKeyword => "SByte",
				SyntaxKind.ShortKeyword => "Int16",
				SyntaxKind.UShortKeyword => "UInt16",
				SyntaxKind.IntKeyword => "Int32",
				SyntaxKind.UIntKeyword => "UInt32",
				SyntaxKind.LongKeyword => "Int64",
				SyntaxKind.ULongKeyword => "UInt64",
				SyntaxKind.FloatKeyword => "Single",
				SyntaxKind.DoubleKeyword => "Double",
				SyntaxKind.DecimalKeyword => "Decimal",
				SyntaxKind.CharKeyword => "Char",
				SyntaxKind.StringKeyword => "String",
				SyntaxKind.ObjectKeyword => "Object",
				_ => null,
			};

			if (fullTypeName is not null)
			{
				return result.WithExpression(memberAccess.WithExpression(SyntaxFactory.ParseTypeName(fullTypeName)));
			}
		}

		return result;
	}

	/// <summary>
	/// Surrounds a contiguous group of statements in the list with appropriate blank lines, based on grouping logic and
	/// statement context.
	/// </summary>
	/// <remarks>This method ensures that blank lines are inserted or removed around groups of related statements,
	/// such as local declarations or expression statements, to improve code readability. Special handling is applied to
	/// avoid unnecessary blank lines between declarations and immediate assignments to declared variables.</remarks>
	/// <param name="visited">The list of statements to process and modify by adding or trimming blank lines around contiguous groups.</param>
	/// <param name="i">The index in the list at which to start searching for a contiguous group. Updated to the last index of the
	/// processed group.</param>
	/// <param name="isInGroup">A predicate that determines whether a given statement belongs to the group to be surrounded.</param>
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
			var prev = visited[start - 1];
			var skipAddingBlankBefore = false;

			// If the group we're surrounding is an expression-statement group and the
			// previous contiguous statements are local declarations, do not insert a
			// blank line if the first expression in the group is a simple assignment
			// to one of the declared variables. This prevents a blank line between
			// a declaration and an immediate assignment to that variable.
			if (isInGroup(visited[start]) && visited[start] is ExpressionStatementSyntax && prev is LocalDeclarationStatementSyntax)
			{
				// gather declared ids from the contiguous declaration block that ends at prev
				var ids = new HashSet<string>(StringComparer.Ordinal);

				for (var k = start - 1; k >= 0; k--)
				{
					if (visited[k] is LocalDeclarationStatementSyntax { Declaration: { } decl })
					{
						foreach (var v in decl.Variables)
						{
							ids.Add(v.Identifier.ValueText);
						}
					}
					else
					{
						break; // stop at first non-declaration
					}
				}

				// check the group's first statement
				var first = visited[start] as ExpressionStatementSyntax;

				if (first?.Expression is AssignmentExpressionSyntax assign && assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
				{
					if (assign.Left is IdentifierNameSyntax leftId)
					{
						if (ids.Contains(leftId.Identifier.ValueText))
						{
							skipAddingBlankBefore = true;
						}
					}
				}
			}

			if (!skipAddingBlankBefore)
			{
				visited[start - 1] = EnsureTrailingBlankLine(prev);
			}
		}

		visited[start] = TrimLeadingBlankLinesTo(visited[start], 0);

		if (end < visited.Count - 1)
		{
			// If this group is a group of local declarations, and the next statement
			// is a direct assignment to one of the variables declared here, do not
			// insert a blank line between the group and that assignment.
			var shouldAddBlank = true;

			if (isInGroup(visited[start]) && visited[start] is LocalDeclarationStatementSyntax)
			{
				// gather all declared identifiers in the group
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

				// check next statement
				var next = visited[end + 1];

				if (next is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign } && assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
				{
					if (assign.Left is IdentifierNameSyntax leftId)
					{
						if (ids.Contains(leftId.Identifier.ValueText))
						{
							shouldAddBlank = false;
						}
					}
				}
			}

			if (shouldAddBlank)
			{
				visited[end] = EnsureTrailingBlankLine(visited[end]);
				visited[end + 1] = TrimLeadingBlankLinesTo(visited[end + 1], 0);
			}
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

		while (lastContentIdx >= 0 && trailing[lastContentIdx].IsKind(SyntaxKind.EndOfLineTrivia, SyntaxKind.WhitespaceTrivia))
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

	private static bool HasLeadingComment(StatementSyntax statement)
	{
		var leading = statement.GetLeadingTrivia();

		foreach (var trivia in leading)
		{
			if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia, 
				    SyntaxKind.MultiLineCommentTrivia, 
				    SyntaxKind.SingleLineDocumentationCommentTrivia,
				    SyntaxKind.MultiLineDocumentationCommentTrivia))
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
			else if (foundEol && trivia.IsKind(SyntaxKind.SingleLineCommentTrivia, SyntaxKind.MultiLineCommentTrivia))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsStatementEmpty(StatementSyntax statement)
	{
		return statement is BlockSyntax { Statements.Count: 0 };
	}

	private static ExpressionSyntax NegateCondition(ExpressionSyntax condition)
	{
		switch (condition)
		{
			// Handle logical NOT: !x -> x
			case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
				return prefix.Operand;
			// Handle binary expressions (comparisons and logical operators)
			case BinaryExpressionSyntax binary:
			{
				var negatedKind = GetNegatedBinaryOperator(binary.Kind());

				if (negatedKind.HasValue)
				{
					// For comparisons: invert the operator (e.g., > becomes <=)
					if (IsComparisonOperator(binary.Kind()))
					{
						return SyntaxFactory.BinaryExpression(
							negatedKind.Value,
							binary.Left,
							binary.Right
						);
					}

					// For logical operators (&&, ||): apply De Morgan's law
					// !(a && b) -> !a || !b
					// !(a || b) -> !a && !b
					if (binary.IsKind(SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression))
					{
						return SyntaxFactory.BinaryExpression(
							negatedKind.Value,
							NegateCondition(binary.Left),
							NegateCondition(binary.Right)
						);
					}
				}

				break;
			}
			// Handle parenthesized expressions: move negation inside
			case ParenthesizedExpressionSyntax paren:
				return NegateCondition(paren.Expression);
			// Handle method calls that return bool (like IsNullOrEmpty, Any, etc.)
			// Keep them simple with ! prefix
			case InvocationExpressionSyntax:
			case IdentifierNameSyntax:
			case MemberAccessExpressionSyntax:
				return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, condition);
		}

		// Default: wrap in parentheses and add !
		var parenthesizedCondition = SyntaxFactory.ParenthesizedExpression(condition);
		return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, parenthesizedCondition);
	}

	private static bool IsComparisonOperator(SyntaxKind kind)
	{
		return kind is SyntaxKind.GreaterThanExpression
			or SyntaxKind.GreaterThanOrEqualExpression
			or SyntaxKind.LessThanExpression
			or SyntaxKind.LessThanOrEqualExpression
			or SyntaxKind.EqualsExpression
			or SyntaxKind.NotEqualsExpression;
	}

	private static SyntaxKind? GetNegatedBinaryOperator(SyntaxKind kind)
	{
		return kind switch
		{
			// Comparison operators
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanExpression,
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanExpression,
			SyntaxKind.EqualsExpression => SyntaxKind.NotEqualsExpression,
			SyntaxKind.NotEqualsExpression => SyntaxKind.EqualsExpression,

			// Logical operators (De Morgan's law)
			SyntaxKind.LogicalAndExpression => SyntaxKind.LogicalOrExpression,
			SyntaxKind.LogicalOrExpression => SyntaxKind.LogicalAndExpression,

			_ => null
		};
	}

	// New: normalize cast expression spacing so `(int? )` -> `(int?)`
	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		var result = base.VisitCastExpression(node);

		if (result is CastExpressionSyntax castExpr)
		{
			// Remove trailing whitespace from the type inside the cast
			// This ensures casts like (int? ) become (int?)
			var type = castExpr.Type;
			
			// Strip all trailing trivia from the type
			var lastToken = type.GetLastToken();
			var newLastToken = lastToken.WithTrailingTrivia(SyntaxFactory.TriviaList());
			type = type.ReplaceToken(lastToken, newLastToken);
			
			return castExpr.WithType(type);
		}

		return result;
	}
}