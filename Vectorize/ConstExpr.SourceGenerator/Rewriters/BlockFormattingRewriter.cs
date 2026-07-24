using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class BlockFormattingRewriter : CSharpSyntaxRewriter
{
	// Strips explanatory "// ..." comments (e.g. carried over from the original source, like
	// "// Optimize by using smaller k") from generated code. XML doc comments are untouched.
	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		var visited = base.VisitToken(token);

		return visited.HasLeadingTrivia
			? visited.WithLeadingTrivia(StripLeadingComments(visited.LeadingTrivia))
			: visited;
	}

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

		var visited = base.VisitAssignmentExpression(node) as AssignmentExpressionSyntax;

		if (visited is null || !visited.IsKind(SyntaxKind.SimpleAssignmentExpression) || visited.Left is not IdentifierNameSyntax leftId)
		{
			return visited;
		}

		if (TryRewriteToCompoundAssignment(visited, leftId.Identifier.ValueText, out var rewritten))
		{
			return rewritten?.WithTriviaFrom(visited) ?? visited;
		}

		return visited;
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
			visited = visited.WithIsKeyword(isKeyword.WithTrailingTrivia(Space));
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

	public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
	{
		var visited = (ParenthesizedLambdaExpressionSyntax?) base.VisitParenthesizedLambdaExpression(node);

		if (visited is null)
		{
			return visited;
		}

		// Check if the lambda body is a simple invocation where all parameters are passed as arguments in the same order
		// Pattern: (x) => Method(x) -> Method
		// Pattern: (x, y) => Method(x, y) -> Method
		if (visited.ExpressionBody is InvocationExpressionSyntax invocation &&
		    invocation.ArgumentList.Arguments.Count == visited.ParameterList.Parameters.Count)
		{
			var allMatch = true;

			for (var i = 0; i < visited.ParameterList.Parameters.Count; i++)
			{
				var param = visited.ParameterList.Parameters[i];
				var arg = invocation.ArgumentList.Arguments[i];

				if (arg.Expression is not IdentifierNameSyntax identifier ||
				    identifier.Identifier.ValueText != param.Identifier.ValueText ||
				    arg.NameColon is not null ||
				    !arg.RefKindKeyword.IsKind(SyntaxKind.None))
				{
					allMatch = false;
					break;
				}
			}

			if (allMatch)
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

		if (TryCreateLiteral(node.Token.Value, out var expression))
		{
			return (node.Token.Value switch
			{
				Math.PI => ParseExpression("Double.Pi"),
				Math.PI * 2 => ParseExpression("Double.Tau"),
				Math.E => ParseExpression("Double.E"),
				MathF.PI => ParseExpression("Single.Pi"),
				MathF.PI * 2 => ParseExpression("Single.Tau"),
				MathF.E => ParseExpression("Single.E"),
				Double.Epsilon => ParseExpression("Double.Epsilon"),
				Single.Epsilon => ParseExpression("Single.Epsilon"),
				Byte.MaxValue => ParseExpression("Byte.MaxValue"),
				Int16.MaxValue => ParseExpression("Int16.MaxValue"),
				Int32.MaxValue => ParseExpression("Int32.MaxValue"),
				Int64.MaxValue => ParseExpression("Int64.MaxValue"),
				// int/long MinValue are emitted as a single negative literal token (TryCreateLiteral),
				// so they never reach VisitPrefixUnaryExpression — handle them here.
				Int32.MinValue => ParseExpression("Int32.MinValue"),
				Int64.MinValue => ParseExpression("Int64.MinValue"),
				UInt16.MaxValue => ParseExpression("UInt16.MaxValue"),
				UInt32.MaxValue => ParseExpression("UInt32.MaxValue"),
				UInt64.MaxValue => ParseExpression("UInt64.MaxValue"),
				Decimal.MaxValue => ParseExpression("Decimal.MaxValue"),
				Double.MaxValue => ParseExpression("Double.MaxValue"),
				Single.MaxValue => ParseExpression("Single.MaxValue"),
				_ => IsHexOrBinaryLiteral(node.Token) ? node : UseScientificNotationIfAwkward(expression, node.Token.Value)
			}).WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());
		}

		return node;
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		if (node.IsKind(SyntaxKind.UnaryMinusExpression)
		    && node.Operand is LiteralExpressionSyntax operand)
		{
			var replacement = operand.Token.Value switch
			{
				// Only floating-point/decimal are symmetric (-MaxValue == MinValue).
				// Integers are NOT: -Int32.MaxValue is -2147483647, not Int32.MinValue.
				// int/long MinValue arrive as single literal tokens and are handled in
				// VisitLiteralExpression, so no integer arms belong here.
				Decimal.MaxValue => ParseExpression("Decimal.MinValue"),
				Double.MaxValue => ParseExpression("Double.MinValue"),
				Single.MaxValue => ParseExpression("Single.MinValue"),
				_ => null
			};

			if (replacement is not null)
			{
				return replacement.WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());
			}
		}

		return base.VisitPrefixUnaryExpression(node);
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		// Visit child nodes first
		var visitedNode = base.VisitIfStatement(node);

		if (visitedNode is not IfStatementSyntax visited)
		{
			return visitedNode;
		}

		// Ensure the if body is always a BlockSyntax
		visited = visited.WithStatement(visited.Statement);

		// Ensure the else body is always a BlockSyntax (but leave else-if chains as-is)
		if (visited.Else is { Statement: not IfStatementSyntax } elseClause)
		{
			visited = visited.WithElse(elseClause.WithStatement(visited.Else.Statement));
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
			case true when visited.Else is null || elseClauseEmpty:
			{
				// Remove the entire if statement
				return null;
			}
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

		if (visited.Else is null && visited.Statement is BlockSyntax { Statements.Count: 1 } block
		                         && node.Parent is not ElseClauseSyntax
		                         && !IsConditionalSyntax(block.Statements[0]))
		{
			visited = visited.WithStatement(block.Statements[0]);
		}

		return visited;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		if (node.Statement is BlockSyntax { Statements.Count: 1 } block
		    && !IsConditionalSyntax(block.Statements[0]))
		{
			node = node.WithStatement(block.Statements[0]);
		}

		return base.VisitForStatement(node);
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		if (node.Statement is BlockSyntax { Statements.Count: 1 } block
		    && !IsConditionalSyntax(block.Statements[0]))
		{
			node = node.WithStatement(block.Statements[0]);
		}

		return base.VisitForEachStatement(node);
	}

	public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
	{
		if (node.Statement is BlockSyntax { Statements.Count: 1 } block
		    && !IsConditionalSyntax(block.Statements[0]))
		{
			node = node.WithStatement(block.Statements[0]);
		}

		return base.VisitForEachVariableStatement(node);
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		if (node.Statement is BlockSyntax { Statements.Count: 1 } block
		    && !IsConditionalSyntax(block.Statements[0]))
		{
			node = node.WithStatement(block.Statements[0]);
		}

		return base.VisitWhileStatement(node);
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		if (node.Statement is BlockSyntax { Statements.Count: 1 } block
		    && !IsConditionalSyntax(block.Statements[0]))
		{
			node = node.WithStatement(block.Statements[0]);
		}

		return base.VisitDoStatement(node);
	}

	// private static BlockSyntax EnsureBlock(StatementSyntax statement)
	// {
	// 	if (statement is BlockSyntax block)
	// 	{
	// 		return block;
	// 	}
	//
	// 	return Block(SingletonList(statement));
	// }

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		return base.VisitLocalFunctionStatement(node)?.WithoutLeadingTrivia();
	}

	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var visited = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;

		if (visited is null)
		{
			return null;
		}

		if (visited.ConstraintClauses.Count > 0)
		{
			var newClauses = visited.ConstraintClauses
				.Select(c => c.WithLeadingTrivia(ElasticCarriageReturnLineFeed, Whitespace("\t")));
			visited = visited.WithConstraintClauses(List(newClauses));

			// Strip trailing EOL from ) so there's no blank line before the first where
			var closeParen = visited.ParameterList.CloseParenToken;
			var cleanedTrivia = closeParen.TrailingTrivia
				.Where(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia));
			visited = visited.WithParameterList(
				visited.ParameterList.WithCloseParenToken(closeParen.WithTrailingTrivia(TriviaList(cleanedTrivia))));
		}

		// Only strip leading trivia for top-level methods; preserve indentation for methods inside type declarations
		if (node.Parent is not TypeDeclarationSyntax)
		{
			visited = visited.WithoutLeadingTrivia();
		}

		return visited;
	}

	public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
	{
		var visited = base.VisitPropertyDeclaration(node);

		// Only strip leading trivia for top-level properties; preserve indentation inside type declarations
		if (node.Parent is not TypeDeclarationSyntax)
		{
			visited = visited?.WithoutLeadingTrivia();
		}

		return visited;
	}

	public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
	{
		var visited = base.VisitIndexerDeclaration(node);

		// Only strip leading trivia for top-level indexers; preserve indentation inside type declarations
		if (node.Parent is not TypeDeclarationSyntax)
		{
			visited = visited?.WithoutLeadingTrivia();
		}

		return visited;
	}

	public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
	{
		var visited = base.VisitOperatorDeclaration(node);

		// Only strip leading trivia for top-level operators; preserve indentation inside type declarations
		if (node.Parent is not TypeDeclarationSyntax)
		{
			visited = visited?.WithoutLeadingTrivia();
		}

		return visited;
	}

	public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
	{
		var visited = base.VisitConversionOperatorDeclaration(node);

		// Only strip leading trivia for top-level conversion operators; preserve indentation inside type declarations
		if (node.Parent is not TypeDeclarationSyntax)
		{
			visited = visited?.WithoutLeadingTrivia();
		}

		return visited;
	}

	public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
	{
		var visited = (StructDeclarationSyntax?) base.VisitStructDeclaration(node);

		if (visited is null || visited.Members.Count <= 1)
		{
			return visited;
		}

		return visited.WithMembers(NormalizeMemberSpacing(visited.Members));
	}

	public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
	{
		var visited = (RecordDeclarationSyntax?) base.VisitRecordDeclaration(node);

		if (visited is null || visited.Members.Count <= 1)
		{
			return visited;
		}

		return visited.WithMembers(NormalizeMemberSpacing(visited.Members));
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
				return node.WithStatements(List(visited));
			}

			return node;
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
										var newLeading = TriviaList();

										for (var k = idx; k < leading.Count; k++)
										{
											newLeading = newLeading.Add(leading[k]);
										}

										newFirstToken = firstToken.WithLeadingTrivia(newLeading);
										rightExpr = rightExpr.ReplaceToken(firstToken, newFirstToken);
									}

									var newVar = decl.Variables[0].WithInitializer(EqualsValueClause(rightExpr));
									var newDecl = decl.WithVariables(SingletonSeparatedList(newVar));

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

		// Fold a run of consecutive "x++;" for the same local into one "x += n;"
		// (e.g. the two index++ left behind when a redundant unrolled break guard is dropped).
		for (var i = 0; i < visited.Count; i++)
		{
			if (!TryGetPostIncrementTarget(visited[i], out var name))
			{
				continue;
			}

			var run = 1;

			while (i + run < visited.Count
			       && TryGetPostIncrementTarget(visited[i + run], out var next)
			       && next == name)
			{
				run++;
			}

			if (run < 2)
			{
				continue;
			}

			visited[i] = ParseStatement($"{name} += {run};")
				.WithLeadingTrivia(visited[i].GetLeadingTrivia())
				.WithTrailingTrivia(visited[i + run - 1].GetTrailingTrivia());
			visited.RemoveRange(i + 1, run - 1);
		}

		// Attach the following statement to a label that only carries an empty statement:
		// "L: ; return x;" -> "L: return x;". Skipped when the label is the block's last
		// statement, since a label must always be followed by a statement.
		for (var i = 0; i < visited.Count - 1; i++)
		{
			if (visited[i] is LabeledStatementSyntax { Statement: EmptyStatementSyntax } labeled)
			{
				visited[i] = labeled
					.WithColonToken(labeled.ColonToken.WithTrailingTrivia(Space))
					.WithStatement(visited[i + 1].WithoutLeadingTrivia());
				visited.RemoveAt(i + 1);
			}
		}

		// Groepeer lokale declaraties en omring met lege regels
		// (een declaratie die meteen erna wordt herschreven, zoals "var p = ...; p = ...;", start een
		// nieuwe groep zodat een FMA/accumulator-keten niet aan voorgaande setup-declaraties vastplakt)
		for (var i = 0; i < visited.Count; i++)
		{
			if (visited[i] is LocalDeclarationStatementSyntax)
			{
				SurroundContiguousGroup(visited, ref i, static s => s is LocalDeclarationStatementSyntax, j => IsAccumulatorHeadDeclaration(visited, j));
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

		var newNode = node.WithStatements(List(visited));
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
			rk = rk.WithTrailingTrivia(trailing.Add(Space));
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
				var newLeading = TriviaList();

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

	public override SyntaxNode? VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
	{
		var visited = base.VisitImplicitArrayCreationExpression(node) as ImplicitArrayCreationExpressionSyntax ?? node;

		if (visited.Initializer.Expressions.Count > 0)
		{
			// Strip trailing trivia from `]` so there's no newline before `{`
			visited = visited
				.WithCloseBracketToken(visited.CloseBracketToken.WithTrailingTrivia(TriviaList()))
				.WithInitializer(FlattenArrayInitializer(visited.Initializer));
		}

		return visited;
	}

	public override SyntaxNode? VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
	{
		var visited = base.VisitArrayCreationExpression(node) as ArrayCreationExpressionSyntax ?? node;

		if (visited.Initializer?.Expressions.Count > 0)
		{
			// Strip trailing trivia from the last `]` of the type so there's no newline before `{`
			var lastTypeToken = visited.Type.GetLastToken();
			var cleanedType = visited.Type.ReplaceToken(
				lastTypeToken,
				lastTypeToken.WithTrailingTrivia(TriviaList()));
			visited = visited
				.WithType(cleanedType)
				.WithInitializer(FlattenArrayInitializer(visited.Initializer));
		}

		return visited;
	}

	public override SyntaxNode? VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
	{
		var visited = base.VisitStackAllocArrayCreationExpression(node) as StackAllocArrayCreationExpressionSyntax ?? node;

		if (visited.Initializer?.Expressions.Count > 0)
		{
			// Strip trailing trivia from the last `]` of the type so there's no newline before `{`
			var lastTypeToken = visited.Type.GetLastToken();
			var cleanedType = visited.Type.ReplaceToken(
				lastTypeToken,
				lastTypeToken.WithTrailingTrivia(TriviaList()));
			visited = visited
				.WithType(cleanedType)
				.WithInitializer(FlattenArrayInitializer(visited.Initializer));
		}

		return visited;
	}

	private static InitializerExpressionSyntax FlattenArrayInitializer(InitializerExpressionSyntax initializer)
	{
		var flatElements = initializer.Expressions.Select(expr => expr.WithoutTrivia()).ToArray();
		var commas = Enumerable.Repeat(Token(SyntaxKind.CommaToken).WithTrailingTrivia(Space), Math.Max(0, flatElements.Length - 1)).ToArray();

		return initializer
			.WithOpenBraceToken(
				Token(SyntaxKind.OpenBraceToken)
					.WithLeadingTrivia(Space)
					.WithTrailingTrivia(Space))
			.WithCloseBraceToken(
				Token(SyntaxKind.CloseBraceToken)
					.WithLeadingTrivia(Space)
					.WithTrailingTrivia(TriviaList()))
			.WithExpressions(SeparatedList(flatElements, commas));
	}

	// Normalize object creation spacing so `new Type (` -> `new Type(`
	// and flatten collection initializers onto a single line.
	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		var result = base.VisitObjectCreationExpression(node) as ObjectCreationExpressionSyntax ?? node;

		// Remove trailing trivia from the type name (e.g. `new Dictionary<int, int> (` -> `new Dictionary<int, int>(`)
		result = result.WithType(result.Type.WithTrailingTrivia());

		// Flatten collection initializer onto a single line: { { k, v }, ... }
		if (result.Initializer is { RawKind: (int) SyntaxKind.CollectionInitializerExpression } initializer)
		{
			var flat = FlattenInitializer(initializer);

			// Ensure no newline between the closing ) and the opening {
			result = result
				.WithInitializer(flat.WithLeadingTrivia());

			if (result.ArgumentList is not null)
			{
				result = result.WithArgumentList(result.ArgumentList.WithTrailingTrivia(Space));
			}
		}

		return result;
	}

	private static InitializerExpressionSyntax FlattenInitializer(InitializerExpressionSyntax initializer)
	{
		var flatElements = initializer.Expressions
			.Select(expr =>
			{
				ExpressionSyntax flat;

				if (expr is InitializerExpressionSyntax innerInit)
				{
					var innerComma = Token(SyntaxKind.CommaToken).WithTrailingTrivia(Space);
					var innerCommas = Enumerable.Repeat(innerComma, Math.Max(0, innerInit.Expressions.Count - 1)).ToArray();
					var innerItems = innerInit.Expressions.Select(e => e.WithoutTrivia()).ToArray();

					flat = innerInit
						.WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken)
							.WithLeadingTrivia(Space))
						.WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
						.WithExpressions(SeparatedList(innerItems, innerCommas));
				}
				else
				{
					flat = expr.WithoutTrivia();
				}

				return flat.WithLeadingTrivia(Space);
			})
			.ToArray();

		var commas = Enumerable.Repeat(
			Token(SyntaxKind.CommaToken).WithTrailingTrivia(),
			Math.Max(0, flatElements.Length - 1)).ToArray();

		return initializer
			.WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia())
			.WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)
				.WithLeadingTrivia(Space))
			.WithExpressions(SeparatedList(flatElements, commas));
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
				_ => null
			};

			if (fullTypeName is not null)
			{
				return result.WithExpression(memberAccess.WithExpression(ParseTypeName(fullTypeName)));
			}
		}

		return result;
	}

	/// <summary>
	///   Surrounds a contiguous group of statements in the list with appropriate blank lines, based on grouping logic and
	///   statement context.
	/// </summary>
	/// <remarks>
	///   This method ensures that blank lines are inserted or removed around groups of related statements,
	///   such as local declarations or expression statements, to improve code readability. Special handling is applied to
	///   avoid unnecessary blank lines between declarations and immediate assignments to declared variables.
	/// </remarks>
	/// <param name="visited">
	///   The list of statements to process and modify by adding or trimming blank lines around contiguous
	///   groups.
	/// </param>
	/// <param name="i">
	///   The index in the list at which to start searching for a contiguous group. Updated to the last index of the
	///   processed group.
	/// </param>
	/// <param name="isInGroup">A predicate that determines whether a given statement belongs to the group to be surrounded.</param>
	private static void SurroundContiguousGroup(List<StatementSyntax> visited, ref int i, Func<StatementSyntax, bool> isInGroup, Func<int, bool>? stopBefore = null)
	{
		var start = i;
		var end = i;

		for (var j = i + 1; j < visited.Count; j++)
		{
			if (isInGroup(visited[j]) && (stopBefore is null || !stopBefore(j)))
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

	private static bool IsTarget(StatementSyntax s)
	{
		return s is IfStatementSyntax
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
	}

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
			list = list.Add(ElasticCarriageReturnLineFeed);
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
			newLeading.Add(ElasticCarriageReturnLineFeed);
		}

		for (var i = idx; i < leading.Count; i++)
		{
			newLeading.Add(leading[i]);
		}

		return statement.WithLeadingTrivia(TriviaList(newLeading));
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

		newTrailing.Add(ElasticCarriageReturnLineFeed);

		return block.WithOpenBraceToken(open.WithTrailingTrivia(TriviaList(newTrailing)));
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

	private static SyntaxTriviaList StripLeadingComments(SyntaxTriviaList trivia)
	{
		var result = new List<SyntaxTrivia>(trivia.Count);

		for (var i = 0; i < trivia.Count; i++)
		{
			var current = trivia[i];

			if (!current.IsKind(SyntaxKind.SingleLineCommentTrivia, SyntaxKind.MultiLineCommentTrivia))
			{
				result.Add(current);
				continue;
			}

			// Drop the indentation that only existed to line up this comment.
			if (result.Count > 0 && result[^1].IsKind(SyntaxKind.WhitespaceTrivia)
			                     && (result.Count == 1 || result[^2].IsKind(SyntaxKind.EndOfLineTrivia)))
			{
				result.RemoveAt(result.Count - 1);
			}

			// Drop the comment's own line terminator so no blank line is left behind.
			if (i + 1 < trivia.Count && trivia[i + 1].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				i++;
			}
		}

		return TriviaList(result);
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
			{
				return prefix.Operand;
			}
			// Handle binary expressions (comparisons and logical operators)
			case BinaryExpressionSyntax binary:
			{
				var negatedKind = GetNegatedBinaryOperator(binary.Kind());

				if (negatedKind.HasValue)
				{
					// For comparisons: invert the operator (e.g., > becomes <=)
					if (IsComparisonOperator(binary.Kind()))
					{
						return BinaryExpression(
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
						return BinaryExpression(
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
			{
				return NegateCondition(paren.Expression);
			}
			// Handle method calls that return bool (like IsNullOrEmpty, Any, etc.)
			// Keep them simple with ! prefix
			case InvocationExpressionSyntax:
			case IdentifierNameSyntax:
			case MemberAccessExpressionSyntax:
			{
				return LogicalNotExpression(condition);
			}
		}

		// Default: wrap in parentheses and add !
		var parenthesizedCondition = ParenthesizedExpression(condition);
		return LogicalNotExpression(parenthesizedCondition);
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

	public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
	{
		var visited = (SwitchSectionSyntax?) base.VisitSwitchSection(node);

		if (visited is null)
		{
			return null;
		}

		// Already wrapped in a single block — nothing to do.
		if (visited.Statements is [ BlockSyntax ])
		{
			return visited;
		}

		// A single statement reads fine directly under the case label; only multi-statement
		// bodies need braces to visually group them.
		if (visited.Statements.Count <= 1)
		{
			return visited;
		}

		// Derive indentation from the case label so braces align with the case keyword.
		var labelIndent = TriviaList();

		if (visited.Labels.Count > 0)
		{
			labelIndent = labelIndent.AddRange(visited.Labels[0].GetLeadingTrivia().Where(w => w.IsKind(SyntaxKind.WhitespaceTrivia)));
		}

		// Build open brace: same indentation as case label (the case label's colon
		// token already carries a trailing newline, so we only need whitespace here).
		var openBraceLeading = TriviaList();

		foreach (var t in labelIndent)
		{
			openBraceLeading = openBraceLeading.Add(t);
		}

		var openBrace = Token(SyntaxKind.OpenBraceToken)
			.WithLeadingTrivia(openBraceLeading)
			.WithTrailingTrivia(LineFeed);

		// Build close brace: newline + same indentation as case label
		var closeBraceLeading = TriviaList();

		foreach (var t in labelIndent)
		{
			closeBraceLeading = closeBraceLeading.Add(t);
		}

		var closeBrace = Token(SyntaxKind.CloseBraceToken)
			.WithLeadingTrivia(closeBraceLeading)
			.WithTrailingTrivia(LineFeed);

		var block = VisitBlock(Block(openBrace, List(visited.Statements), closeBrace));

		return visited.WithStatements(SingletonList<StatementSyntax>(block as BlockSyntax));
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
			var newLastToken = lastToken.WithTrailingTrivia(TriviaList());
			type = type.ReplaceToken(lastToken, newLastToken);

			return castExpr.WithType(type);
		}

		return result;
	}

	private static bool TryRewriteToCompoundAssignment(AssignmentExpressionSyntax visited, string leftName, out AssignmentExpressionSyntax rewritten)
	{
		rewritten = visited;

		if (visited.Right is not BinaryExpressionSyntax binary)
		{
			return false;
		}

		if (!TryExtractCompoundBinary(binary, leftName, out var compoundKind, out var rightExpression))
		{
			return false;
		}

		var compoundOperator = GetCompoundAssignmentOperator(compoundKind);

		if (compoundOperator is null)
		{
			return false;
		}

		var rewrittenText = $"{visited.Left.WithoutTrivia()} {compoundOperator} {rightExpression.WithoutTrivia()}";
		rewritten = ParseExpression(rewrittenText).NormalizeWhitespace() as AssignmentExpressionSyntax ?? visited;
		return rewritten != visited;
	}

	private static bool TryExtractCompoundBinary(BinaryExpressionSyntax binary, string leftName, out SyntaxKind compoundKind, out ExpressionSyntax rightExpression)
	{
		if (binary.Left is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == leftName)
		{
			compoundKind = binary.Kind();
			rightExpression = binary.Right;
			return true;
		}

		if (binary.Left is BinaryExpressionSyntax nested
		    && TryExtractCompoundBinary(nested, leftName, out var nestedKind, out var nestedRight)
		    && CanFoldNestedCompound(nestedKind, binary.Kind()))
		{
			compoundKind = nestedKind;
			rightExpression = BinaryExpression(binary.Kind(), nestedRight, binary.Right);
			return true;
		}


		compoundKind = default;
		rightExpression = binary;
		return false;
	}

	private static bool CanFoldNestedCompound(SyntaxKind nestedKind, SyntaxKind outerKind)
	{
		if (nestedKind != outerKind)
		{
			return false;
		}

		return nestedKind is SyntaxKind.BitwiseAndExpression
			or SyntaxKind.BitwiseOrExpression
			or SyntaxKind.ExclusiveOrExpression;
	}

	private static string? GetCompoundAssignmentOperator(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.AddExpression => "+=",
			SyntaxKind.SubtractExpression => "-=",
			SyntaxKind.MultiplyExpression => "*=",
			SyntaxKind.DivideExpression => "/=",
			SyntaxKind.ModuloExpression => "%=",
			SyntaxKind.BitwiseAndExpression => "&=",
			SyntaxKind.BitwiseOrExpression => "|=",
			SyntaxKind.ExclusiveOrExpression => "^=",
			SyntaxKind.LeftShiftExpression => "<<=",
			SyntaxKind.RightShiftExpression => ">>=",
			SyntaxKind.UnsignedRightShiftExpression => ">>>=",
			_ => null
		};
	}

	// Rewrites a double/float literal to scientific notation when the plain decimal form
	// would bury the value under leading/trailing zeros (e.g. -0.00019841269836761127,
	// 0.009618129107628477). Values in a "normal" range (roughly 0.01 .. 10 million) are
	// left as-is since fixed notation is easier to read there.
	private static ExpressionSyntax UseScientificNotationIfAwkward(ExpressionSyntax expression, object? tokenValue)
	{
		return tokenValue switch
		{
			double d when IsAwkwardMagnitude(d) => LiteralExpression(SyntaxKind.NumericLiteralExpression,
				Literal(ToScientificText(d.ToString(CultureInfo.InvariantCulture)), d)),
			float f when IsAwkwardMagnitude(f) => LiteralExpression(SyntaxKind.NumericLiteralExpression,
				Literal(ToScientificText(f.ToString(CultureInfo.InvariantCulture)) + "F", f)),
			_ => expression
		};
	}

	// ponytail: fixed cutoffs (0.01 / 1e7), tune here if a project wants a different "looks awkward" range.
	private static bool IsAwkwardMagnitude(double value)
	{
		var abs = Math.Abs(value);
		return abs is not 0 and (< 0.01 or >= 1e7);
	}

	// Reformats a culture-invariant plain-decimal double/float string (e.g. "0.00019841269836761127"
	// or "123456789012") into C# scientific notation (e.g. "1.9841269836761127E-4"), keeping every
	// significant digit so the value round-trips exactly. No-ops if already scientific (contains 'E').
	private static string ToScientificText(string plain)
	{
		if (plain.IndexOf('E') >= 0)
		{
			return plain;
		}

		var dotIndex = plain.IndexOf('.');
		var intPart = dotIndex < 0 ? plain : plain[..dotIndex];
		var fracPart = dotIndex < 0 ? "" : plain[(dotIndex + 1)..];
		var digits = intPart + fracPart;

		var firstSignificant = 0;

		while (firstSignificant < digits.Length - 1 && digits[firstSignificant] == '0')
		{
			firstSignificant++;
		}

		var exponent = intPart.Length - firstSignificant - 1;
		var mantissaDigits = digits[firstSignificant..].TrimEnd('0');

		if (mantissaDigits.Length == 0)
		{
			mantissaDigits = "0";
		}

		var mantissa = mantissaDigits.Length == 1 ? mantissaDigits : $"{mantissaDigits[0]}.{mantissaDigits[1..]}";

		return $"{mantissa}E{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent)}";
	}

	private static bool IsHexOrBinaryLiteral(SyntaxToken token)
	{
		var text = token.Text;

		return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
		       || text.StartsWith("0X", StringComparison.OrdinalIgnoreCase)
		       || text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)
		       || text.StartsWith("0B", StringComparison.OrdinalIgnoreCase);
	}

	private static SyntaxList<MemberDeclarationSyntax> NormalizeMemberSpacing(SyntaxList<MemberDeclarationSyntax> members)
	{
		if (members.Count <= 1)
		{
			return members;
		}

		var newMembers = new SyntaxList<MemberDeclarationSyntax>();

		for (var i = 0; i < members.Count; i++)
		{
			var member = members[i];

			if (i > 0)
			{
				// Normalize: strip all leading EOLs from this member, then add exactly 1
				var leading = member.GetLeadingTrivia();
				var nonEolStart = 0;

				while (nonEolStart < leading.Count && leading[nonEolStart].IsKind(SyntaxKind.EndOfLineTrivia))
				{
					nonEolStart++;
				}

				// Build new leading trivia: exactly 1 EOL + rest (whitespace/indentation)
				var newLeading = TriviaList(LineFeed);

				for (var j = nonEolStart; j < leading.Count; j++)
				{
					newLeading = newLeading.Add(leading[j]);
				}

				member = member.WithLeadingTrivia(newLeading);

				// Also normalize trailing trivia of the *previous* member to have exactly 1 EOL
				var prev = newMembers[i - 1];
				var trailing = prev.GetTrailingTrivia();
				var newTrailing = TriviaList();
				var foundEol = false;

				foreach (var trivia in trailing)
				{
					if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
					{
						if (!foundEol)
						{
							newTrailing = newTrailing.Add(trivia);
							foundEol = true;
						}

						// Skip additional EOLs
					}
					else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) && foundEol)
					{
						// Skip trailing whitespace after EOL
					}
					else
					{
						newTrailing = newTrailing.Add(trivia);
					}
				}

				if (!foundEol)
				{
					newTrailing = newTrailing.Add(LineFeed);
				}

				newMembers = newMembers.Replace(newMembers[i - 1], prev.WithTrailingTrivia(newTrailing));
			}

			newMembers = newMembers.Add(member);
		}

		return newMembers;
	}

	/// <summary>
	///   Matches a bare <c>x++;</c> statement on a simple local and returns the variable name.
	///   Restricted to plain identifiers so folding never changes the side effects of an indexer
	///   or property access.
	/// </summary>
	private static bool TryGetPostIncrementTarget(StatementSyntax statement, out string name)
	{
		if (statement is ExpressionStatementSyntax
		    {
			    Expression: PostfixUnaryExpressionSyntax
			    {
				    RawKind: (int) SyntaxKind.PostIncrementExpression,
				    Operand: IdentifierNameSyntax identifier
			    }
		    })
		{
			name = identifier.Identifier.ValueText;
			return true;
		}

		name = "";
		return false;
	}

	// Een declaratie waarvan de enige variabele meteen in de volgende statement wordt herschreven
	// (bv. "var p = ...; p = ...;") is de kop van een accumulator-keten, geen losse setup-declaratie.
	private static bool IsAccumulatorHeadDeclaration(List<StatementSyntax> statements, int index)
	{
		if (statements[index] is not LocalDeclarationStatementSyntax { Declaration.Variables: [ { Initializer: not null } variable ] })
		{
			return false;
		}

		return index + 1 < statements.Count
		       && statements[index + 1] is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
		       && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
		       && assignment.Left is IdentifierNameSyntax identifier
		       && identifier.Identifier.ValueText == variable.Identifier.ValueText;
	}

	private static bool IsConditionalSyntax(StatementSyntax statement)
	{
		return statement is IfStatementSyntax
			or SwitchStatementSyntax
			or ForStatementSyntax
			or ForEachStatementSyntax
			or ForEachVariableStatementSyntax
			or WhileStatementSyntax
			or DoStatementSyntax
			or UsingStatementSyntax
			or LockStatementSyntax
			or TryStatementSyntax
			or FixedStatementSyntax
			or CheckedStatementSyntax
			or UnsafeStatementSyntax;
	}
}