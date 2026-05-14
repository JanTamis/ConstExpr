using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.ConditionalOptimizers;
using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Statement visitor methods for the ConstExprPartialRewriter.
/// Handles if, for, foreach, while, switch, return, block, and local declaration statements.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is true)
			{
				return Visit(node.Statement);
			}

			return node.Else is not null
				? Visit(node.Else.Statement)
				: null;
		}

		// Save variable state before visiting either branch. The condition is unknown so we
		// cannot tell which branch will execute at runtime. Each branch must be visited with
		// the pre-if state, and afterwards any variable written in either branch must be
		// marked as unknown so downstream code doesn't rely on a specific branch's value.
		var savedState = SaveVariableState();
		var statement = Visit(node.Statement);

		// Restore to pre-if state before visiting the else branch so it doesn't pick up
		// mutations that were made inside the then branch (e.g., h = 0 leaking into else).
		RestoreVariableState(savedState);

		var @else = Visit(node.Else);

		// After both branches: restore original state and invalidate every variable that
		// could have been written in either branch.
		RestoreVariableState(savedState);
		InvalidateAssignedVariables(node.Statement);

		if (node.Else is not null)
		{
			InvalidateAssignedVariables(node.Else.Statement);
		}

		if (@else is null)
		{
			switch (statement)
			{
				case IfStatementSyntax { Else: null } nestedIf:
				{
					condition = Visit(ParenthesizedExpression(LogicalOrExpression(condition as ExpressionSyntax ?? node.Condition, nestedIf.Condition)));
					statement = nestedIf.Statement;

					break;
				}
				case BlockSyntax { Statements: [ IfStatementSyntax { Else: null } nestedBlockIf ] }:
				{
					condition = Visit(ParenthesizedExpression(LogicalOrExpression(condition as ExpressionSyntax ?? node.Condition, nestedBlockIf.Condition)));
					statement = nestedBlockIf.Statement;

					break;
				}
			}
		}

		var result = node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithStatement(statement as StatementSyntax ?? node.Statement)
			.WithElse(@else as ElseClauseSyntax);

		// Try to convert an if-else-if chain to a switch statement / switch expression.
		// Only attempt this when there is at least one else branch (otherwise a single-case
		// switch would be noisier than the original if).
		if (ConvertIfToSwitchCodeRefactoring.TryConvertIfElseChainToSwitch(result, out var switchNode))
		{
			return switchNode;
		}

		switch (statement)
		{
			case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
				when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
				     && assignment.Left is IdentifierNameSyntax assignedIdentifier
				     && @else is ElseClauseSyntax { Statement: ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax elseAssignment } }
				     && elseAssignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
				     && elseAssignment.Left is IdentifierNameSyntax elseAssignedIdentifier
				     && assignedIdentifier.Identifier.Text == elseAssignedIdentifier.Identifier.Text:
			{
				return ExpressionStatement(
					VisitIfElseAssignment(condition, node.Condition, assignment, elseAssignment));
			}
			case BlockSyntax { Statements: [ ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } ] }
				when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
				     && assignment.Left is IdentifierNameSyntax assignedIdentifier
				     && @else is ElseClauseSyntax { Statement: BlockSyntax { Statements: [ ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax elseAssignment } ] } }
				     && elseAssignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
				     && elseAssignment.Left is IdentifierNameSyntax elseAssignedIdentifier
				     && assignedIdentifier.Identifier.Text == elseAssignedIdentifier.Identifier.Text:
			{
				return ExpressionStatement(
					VisitIfElseAssignment(condition, node.Condition, assignment, elseAssignment));
			}
			case ReturnStatementSyntax { Expression: { } ifReturn }
				when @else is ElseClauseSyntax { Statement: ReturnStatementSyntax { Expression: { } elseReturn } }:
			{
				return ReturnStatement(
					Visit(ConditionalExpression(
						condition as ExpressionSyntax ?? node.Condition,
						ifReturn,
						elseReturn)) as ExpressionSyntax);
			}
			case BlockSyntax { Statements: [ ReturnStatementSyntax { Expression: { } ifReturn } ] }
				when @else is ElseClauseSyntax { Statement: BlockSyntax { Statements: [ ReturnStatementSyntax { Expression: { } elseReturn } ] } }:
			{
				return ReturnStatement(
					Visit(ConditionalExpression(
						condition as ExpressionSyntax ?? node.Condition,
						ifReturn,
						elseReturn)) as ExpressionSyntax);
			}
		}

		return result;
	}

	/// <summary>
	/// Converts an if-else where both branches perform a simple assignment to the same variable
	/// into a single assignment. When the condition is a comparison and the operands match the
	/// assigned values (e.g. <c>if (a &gt; b) x = a; else x = b;</c>), the optimizer rewrites the
	/// right-hand side to a call like <c>T.MaxNative(a, b)</c> for any numeric type T. Falls back
	/// to an ordinary conditional expression assignment otherwise.
	/// </summary>
	private ExpressionSyntax VisitIfElseAssignment(
		SyntaxNode condition,
		ExpressionSyntax originalCondition,
		AssignmentExpressionSyntax thenAssignment,
		AssignmentExpressionSyntax elseAssignment)
	{
		var condExpr = ConditionalExpression(
			condition as ExpressionSyntax ?? originalCondition,
			thenAssignment.Right,
			elseAssignment.Right);

		// Resolve the element type from the branch operands — the synthetic conditional itself is
		// not present in the semantic model, but the original source identifiers/expressions are.
		if (!semanticModel.TryGetTypeSymbol(thenAssignment.Right, symbolStore, out var assignType))
		{
			semanticModel.TryGetTypeSymbol(elseAssignment.Right, symbolStore, out assignType);
		}

		if (assignType is not null)
		{
			var optimizer = new ConditionalExpressionOptimizer
			{
				Condition = condExpr.Condition,
				WhenTrue = condExpr.WhenTrue,
				WhenFalse = condExpr.WhenFalse,
				Type = assignType
			};

			if (optimizer.TryOptimize(loader, variables, out var optimizedRhs))
			{
				var visitedRhs = Visit(optimizedRhs) as ExpressionSyntax ?? optimizedRhs as ExpressionSyntax ?? condExpr;
				return thenAssignment.WithRight(visitedRhs);
			}
		}

		return Visit(thenAssignment.WithRight(condExpr)) as ExpressionSyntax
		       ?? thenAssignment.WithRight(condExpr);
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();

		Visit(node.Declaration);

		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
				{
					return null;
				}
				case true:
				{
					return TryUnrollForLoop(node);
				}
			}
		}

		return VisitForStatementWithoutUnroll(node, names);
	}

	/// <summary>
	/// Tries to unroll a for loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollForLoop(ForStatementSyntax node)
	{
		if (attribute.MaxUnrollIterations == 0)
		{
			InvalidateAssignedVariables(node);
			return base.VisitForStatement(node);
		}

		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitForStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			VisitList(node.Incrementors);
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	/// <summary>
	/// Visits a for statement without attempting to unroll it.
	/// </summary>
	private SyntaxNode VisitForStatementWithoutUnroll(ForStatementSyntax node, ImmutableHashSet<string> names)
	{
		// Restore variable states after visiting the loop
		foreach (var name in variables.Keys.Except(names).ToList())
		{
			variables.Remove(name);
		}

		var declaration = Visit(node.Declaration);

		// Visit initializers first so that initial values are set (e.g. i = 0),
		// then invalidate loop variables so the condition and body see them as unknown
		// (the variable changes on every iteration via the incrementors).
		var visitedInitializers = VisitList(node.Initializers);
		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		return node
			.WithInitializers(visitedInitializers)
			.WithCondition(Visit(node.Condition) as ExpressionSyntax ?? node.Condition)
			.WithDeclaration(declaration as VariableDeclarationSyntax ?? node.Declaration)
			.WithStatement(Visit(node.Statement) as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
				{
					return null;
				}
				case true when attribute.MaxUnrollIterations > 0:
				{
					return TryUnrollWhileLoop(node);
				}
			}
		}

		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		var body = Visit(node.Statement);
		var rewrittenCondition = Visit(node.Condition);

		if (body is BlockSyntax { Statements: [ .., BreakStatementSyntax or ReturnStatementSyntax ] items })
		{
			return IfStatement(
				rewrittenCondition as ExpressionSyntax ?? node.Condition,
				Block(items.Take(items.Count - 1)));
		}

		return node
			.WithCondition(rewrittenCondition as ExpressionSyntax ?? node.Condition)
			.WithStatement(body as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		// Do-while always executes at least once
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitDoStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				result.AddRange(block.Statements);
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		// Check final condition
		var finalCondition = Visit(node.Condition);

		if (TryGetLiteralValue(finalCondition, out var finalValue) && finalValue is false)
		{
			return result.Count > 0 ? ToStatementSyntax(result) : null;
		}

		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		return base.VisitDoStatement(node);
	}

	/// <summary>
	/// Tries to unroll a while loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollWhileLoop(WhileStatementSyntax node)
	{
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitWhileStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();
		var collection = Visit(node.Expression);

		var items = GetForEachItems(collection);

		if (items is not null && attribute.MaxUnrollIterations > 0 && items.Count < attribute.MaxUnrollIterations)
		{
			// Save state before attempting unrolling so we can roll back when conditional
			// breaks remain in the unrolled output (i.e., break inside an if whose
			// condition could not be resolved at compile time).
			var savedState = SaveVariableState();
			var unrolled = TryUnrollForEachLoop(node, items);

			// If the unrolled result still contains break statements that are not
			// protected by any inner loop or switch, the breaks are orphaned and
			// would produce invalid C#.  Fall back to a regular foreach, using the
			// constant-folded collection expression so that partial evaluation is
			// still applied where possible.
			if (ContainsOrphanedBreak(unrolled))
			{
				RestoreVariableState(savedState);
				InvalidateAssignedVariablesForForEach(node, names);
				return base.VisitForEachStatement(
					node.WithExpression(collection as ExpressionSyntax ?? node.Expression));
			}

			return unrolled;
		}

		InvalidateAssignedVariablesForForEach(node, names);

		return base.VisitForEachStatement(node);
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="node" /> contains a
	///   <see cref="BreakStatementSyntax" /> that is not enclosed by any inner loop or
	///   switch statement, making it an orphaned break with no valid jump target.
	/// </summary>
	private static bool ContainsOrphanedBreak(SyntaxNode? node)
	{
		if (node is null)
		{
			return false;
		}

		return node
			.DescendantNodes(n =>
				n is not ForStatementSyntax
					and not ForEachStatementSyntax
					and not WhileStatementSyntax
					and not DoStatementSyntax
					and not SwitchStatementSyntax)
			.OfType<BreakStatementSyntax>()
			.Any();
	}

	/// <summary>
	/// Gets the items from a foreach collection expression.
	/// </summary>
	private IReadOnlyList<CSharpSyntaxNode>? GetForEachItems(SyntaxNode? collection)
	{
		return collection switch
		{
			CollectionExpressionSyntax collectionExpression => collectionExpression.Elements,
			LiteralExpressionSyntax { RawKind: (int) SyntaxKind.StringLiteralExpression } stringLiteral =>
				stringLiteral.Token.ValueText
					.Select(s => CreateLiteral(s) as CSharpSyntaxNode)
					.ToList(),
			_ => null
		};
	}

	/// <summary>
	/// Tries to unroll a foreach loop.
	/// </summary>
	private SyntaxNode? TryUnrollForEachLoop(ForEachStatementSyntax node, IReadOnlyList<CSharpSyntaxNode> items)
	{
		var name = node.Identifier.Text;

		if (semanticModel.GetOperation(node) is not IForEachLoopOperation operation)
		{
			return base.VisitForEachStatement(node);
		}

		var variable = new VariableItem(operation.LoopControlVariable.Type, true, null, true);
		variables.Add(name, variable);

		var statements = new List<SyntaxNode>();

		foreach (var item in items)
		{
			if (!TryGetLiteralValue(item, out var val))
			{
				continue;
			}

			variable.Value = val;
			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				statements.Add(statement);
			}

			if (ShouldStopUnrolling(statement, statements))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				statements.Add(block);
			}
		}

		var result = ToStatementSyntax(statements);

		// After unrolling, N consecutive equality-check if-statements with identical bodies
		// may be collapsed into a single combined if by CombineConsecutiveIfStatements
		// (e.g. "if (1==x){return true;} … if (5==x){return true;}" → "if (x is 1 or 5){return true;}").
		// Returning a single IfStatementSyntax (instead of a BlockSyntax wrapping N ifs) means
		// the parent VisitBlock will see the if as a direct sibling of the trailing "return false;"
		// and SimplifyIfReturnPatterns can then fold the pair into "return x is 1 or 5;".
		//
		// Guard: only combine when every if-statement's body unconditionally jumps
		// (return / break / continue / throw).  Without this guard, accumulation patterns like
		// "if (5==x) count++; if (5==x) count++;" (from arrays with duplicate values) would be
		// incorrectly merged into a single conditional that increments count only once.
		if (result is BlockSyntax resultBlock
		    && resultBlock.Statements.All(s => s is not IfStatementSyntax ifStmt
		                                       || ContainsJumpStatement(ifStmt.Statement)))
		{
			var combined = CombineConsecutiveIfStatements(resultBlock.Statements, Visit);
			result = combined.Count == 1 ? combined[0] : Block(combined);
		}

		return result;
	}

	/// <summary>
	/// Invalidates assigned variables for a foreach loop.
	/// </summary>
	private void InvalidateAssignedVariablesForForEach(ForEachStatementSyntax node, ImmutableHashSet<string> names)
	{
		var assignedVariables = AssignedVariables(node);

		foreach (var name in names)
		{
			if (variables.TryGetValue(name, out var variable) && assignedVariables.Contains(name))
			{
				variable.HasValue = false;
			}
		}
	}

	/// <summary>
	/// Checks if loop unrolling should stop based on the current statement.
	/// </summary>
	private static bool ShouldStopUnrolling(SyntaxNode? statement, ICollection<SyntaxNode?> result)
	{
		if (statement is BreakStatementSyntax or ReturnStatementSyntax)
		{
			return true;
		}

		if (statement is not BlockSyntax block || !block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
		{
			return false;
		}

		foreach (var item in block.Statements)
		{
			if (item is BreakStatementSyntax)
			{
				return true;
			}

			result.Add(item);

			if (item is ReturnStatementSyntax)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Invalidates all assigned variables in the given node.
	/// </summary>
	private void InvalidateAssignedVariables(StatementSyntax node)
	{
		foreach (var name in AssignedVariables(node))
		{
			if (variables.TryGetValue(name, out var variable))
			{
				variable.HasValue = false;
			}
		}
	}

	private void InvalidateMutatingReceiverValues(StatementSyntax node)
	{
		foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (invocation.Expression is not MemberAccessExpressionSyntax
			    {
				    Expression: IdentifierNameSyntax { Identifier.Text: var receiverName },
				    Name: IdentifierNameSyntax { Identifier.Text: var methodName }
			    })
			{
				continue;
			}

			if (!variables.TryGetValue(receiverName, out var receiverVariable))
			{
				continue;
			}

			if (!semanticModel.TryGetSymbol(invocation, symbolStore, out IMethodSymbol? targetMethod)
			    && !IsLikelyMutatingMethod(null, methodName))
			{
				continue;
			}

			if (targetMethod is not null && !IsLikelyMutatingMethod(targetMethod, methodName))
			{
				continue;
			}

			receiverVariable.IsAltered = true;
			receiverVariable.HasValue = false;
		}
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = VisitList(node.Statements);
		var mergedForDeclarations = MergeForLoopDeclarations(visited);

		var untilThrown = TakeUntilThrownStatements(mergedForDeclarations);
		var combined = CombineConsecutiveIfStatements(untilThrown, Visit);
		var mergedIfChain = MergeMixedBoolReturnIfs(combined, Visit);
		var simplified = SimplifyIfReturnPatterns(mergedIfChain);

		return node.WithStatements(simplified);
	}

	private SyntaxList<StatementSyntax> MergeForLoopDeclarations(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var comparer = SyntaxNodeComparer.Get<ExpressionSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			if (i + 1 < statements.Count
			    && statements[i] is LocalDeclarationStatementSyntax
			    {
				    Modifiers.Count: 0,
				    Declaration:
				    {
					    Variables: [ { Identifier.Text: var variableName, Initializer: { Value: var declaredValue } } declarator ]
				    } declaration
			    }
			    && statements[i + 1] is ForStatementSyntax
			    {
				    Declaration: null,
				    Initializers:
				    [
					    AssignmentExpressionSyntax
					    {
						    RawKind: (int) SyntaxKind.SimpleAssignmentExpression,
						    Left: IdentifierNameSyntax { Identifier.Text: var initializerName },
						    Right: var initializerValue
					    }
				    ]
			    } forStatement
			    && variableName == initializerName
			    && comparer.Equals(declaredValue.WithoutTrivia(), initializerValue.WithoutTrivia())
			    && !statements.Skip(i + 2).Any(statement => statement.HasIdentifier(variableName)))
			{
				var mergedDeclaration = declaration.WithVariables(
					SeparatedList([
						declarator.WithInitializer(EqualsValueClause(initializerValue))
					]));

				result.Add(forStatement
					.WithDeclaration(mergedDeclaration)
					.WithInitializers(default));
				i++;
				continue;
			}

			result.Add(statements[i]);
		}

		return List(result);
	}

	/// <summary>
	/// Takes statements until a throw or return statement is encountered (inclusive).
	/// Any code after a throw or return statement is unreachable and can be removed.
	/// </summary>
	private static SyntaxList<StatementSyntax> TakeUntilThrownStatements(SyntaxList<StatementSyntax> statements)
	{
		var result = new List<StatementSyntax>();

		foreach (var statement in statements)
		{
			result.Add(statement);

			// Stop after a throw or return statement since code after it is unreachable
			if (statement is ThrowStatementSyntax
			    or ExpressionStatementSyntax { Expression: ThrowExpressionSyntax }
			    or ReturnStatementSyntax)
			{
				break;
			}
		}

		// Strip a trailing void return (return;) — it is redundant at the end of a void block
		// and produces cleaner output (e.g. empty body {} instead of { return; }).
		if (result.Count > 0 && result[^1] is ReturnStatementSyntax { Expression: null })
		{
			result.RemoveAt(result.Count - 1);
		}

		return List(result);
	}

	/// <summary>
	/// Simplifies patterns like:
	/// - if (cond) { return true; } return false; => return cond;
	/// - if (cond) { return false; } return true; => return !cond;
	/// </summary>
	private SyntaxList<StatementSyntax> SimplifyIfReturnPatterns(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			// Check for pattern: if (cond) { return <bool>; } followed by return <opposite bool>;
			if (i + 1 < statements.Count
			    && statements[i] is IfStatementSyntax { Else: null } ifStatement
			    && statements[i + 1] is ReturnStatementSyntax followingReturn)
			{
				if (TryGetIfReturnBoolPattern(ifStatement, followingReturn, out var simplifiedReturn)
				    || TryGetIfReturnPattern(ifStatement, followingReturn, out simplifiedReturn))
				{
					result.Add(simplifiedReturn!);
					i++; // Skip the following return statement
					continue;
				}
			}

			result.Add(statements[i]);
		}

		var resultList = List(result);

		if (statements.Count != resultList.Count)
		{
			return SimplifyIfReturnPatterns(resultList);
		}

		return resultList;
	}

	/// <summary>
	/// Tries to simplify if-return-bool patterns.
	/// </summary>
	private bool TryGetIfReturnBoolPattern(IfStatementSyntax ifStatement, ReturnStatementSyntax followingReturn, out ReturnStatementSyntax? simplified)
	{
		simplified = null;

		// Get the return statement from the if body
		var ifBody = ifStatement.Statement;

		var ifReturn = ifBody switch
		{
			ReturnStatementSyntax ret => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax ret ] } => ret,
			_ => null
		};

		if (ifReturn is null)
		{
			return false;
		}

		// Check if both returns are boolean literals
		if (!TryGetBoolLiteral(ifReturn.Expression, out var ifReturnValue))
		{
			return false;
		}

		// Only simplify if they return opposite values
		if (SyntaxNodeComparer.Get().Equals(ifReturn.Expression, followingReturn.Expression))
		{
			return false;
		}

		// if (cond) { return true; } return false; => return cond;
		// if (cond) { return false; } return true; => return !cond;
		var condition = ifStatement.Condition;

		if (ifReturnValue)
		{
			if (followingReturn.Expression is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.FalseLiteralExpression })
			{
				// return cond;
				simplified = ReturnStatement(condition);
			}
			else
			{
				var newCondition = LogicalAndExpression(condition, followingReturn.Expression);
				var booleanType = semanticModel.Compilation.CreateBoolean();

				if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, condition, booleanType, followingReturn.Expression, booleanType, null, out var result))
				{
					simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
					return true;
				}

				simplified = ReturnStatement(newCondition);
			}
		}
		else if (InvertLogicalRefactoring.TryInvertLogical(condition as BinaryExpressionSyntax, out var inverted))
		{
			var booleanType = semanticModel.Compilation.CreateBoolean();

			if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, inverted, booleanType, followingReturn.Expression, booleanType, null, out var result))
			{
				simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
				return true;
			}

			simplified = ReturnStatement(LogicalAndExpression(inverted, followingReturn.Expression));
		}
		else
		{
			var booleanType = semanticModel.Compilation.CreateBoolean();
			var invertedCondition = NegateExpressionRefactoring.Negate(condition);

			if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, invertedCondition, booleanType, followingReturn.Expression, booleanType, null, out var result))
			{
				simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
				return true;
			}

			simplified = ReturnStatement(LogicalAndExpression(invertedCondition, followingReturn.Expression));
		}

		return simplified is not null;
	}

	/// <summary>
	/// Tries to simplify if-return-bool patterns.
	/// </summary>
	private bool TryGetIfReturnPattern(IfStatementSyntax ifStatement, ReturnStatementSyntax followingReturn, out ReturnStatementSyntax? simplified)
	{
		simplified = null;

		// Get the return statement from the if body
		var ifBody = ifStatement.Statement;

		var ifReturn = ifBody switch
		{
			ReturnStatementSyntax ret => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax ret ] } => ret,
			_ => null
		};

		if (ifReturn is null)
		{
			return false;
		}

		if (!semanticModel.TryGetTypeSymbol(ifReturn.Expression, symbolStore, out var ifReturnType)
		    && !semanticModel.TryGetTypeSymbol(followingReturn.Expression, symbolStore, out ifReturnType))
		{
			simplified = ReturnStatement(Visit(
				ConditionalExpression(
					ifStatement.Condition,
					ifReturn.Expression,
					followingReturn.Expression)) as ExpressionSyntax ?? ifReturn.Expression);
		}
		else
		{
			simplified = ReturnStatement(Visit(
				ConditionalExpression(
					ifStatement.Condition,
					ifReturn.Expression,
					followingReturn.Expression).WithTypeSymbolAnnotation(ifReturnType, symbolStore)) as ExpressionSyntax ?? ifReturn.Expression);
		}

		return true;
	}

	/// <summary>
	/// Tries to extract a boolean literal value from an expression.
	/// </summary>
	private static bool TryGetBoolLiteral(ExpressionSyntax? expression, out bool value)
	{
		value = false;

		if (expression is LiteralExpressionSyntax literal)
		{
			if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
			{
				value = true;
				return true;
			}

			if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
			{
				value = false;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Merges a consecutive run of if-statements (each returning a boolean literal, no else)
	/// that contains a mix of <c>return true</c> and <c>return false</c> into a single
	/// <c>if (...) return false;</c> statement.
	/// <para>
	/// For each if-statement in the run:
	/// <list type="bullet">
	///   <item>If it returns <c>false</c> → its condition is added to the OR chain as-is.</item>
	///   <item>If it returns <c>true</c>  → its condition is negated and added to the OR chain,
	///     because reaching a subsequent <c>return false</c> requires the <c>return true</c>
	///     guard to have been skipped (i.e., its condition was false).</item>
	/// </list>
	/// </para>
	/// Example:
	/// <code>
	/// if (n &lt;= 1) return false;
	/// if (n &lt;= 3) return true;
	/// if (IsEven(n) || n % 3 == 0) return false;
	/// </code>
	/// becomes:
	/// <code>
	/// if (n &lt;= 1 || n &gt; 3 || IsEven(n) || n % 3 == 0) return false;
	/// </code>
	/// </summary>
	private static SyntaxList<StatementSyntax> MergeMixedBoolReturnIfs(SyntaxList<StatementSyntax> statements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			var runStart = i;
			var run = new List<(ExpressionSyntax Condition, bool ReturnValue)>();

			while (i < statements.Count && TryGetIfBoolReturnBody(statements[i], out var cond, out var retVal))
			{
				run.Add((cond!, retVal));
				i++;
			}

			// Only merge when there are at least 2 ifs with mixed true/false returns
			if (run.Count >= 2 && run.Any(r => r.ReturnValue) && run.Any(r => !r.ReturnValue))
			{
				// Build combined OR condition:
				// - "return false" conditions → keep as-is
				// - "return true" conditions → negate (they act as guards; skipping them means we fall through to the false path)
				ExpressionSyntax? combined = null;

				foreach (var (cond, retVal) in run)
				{
					var part = retVal ? NegateCondition(cond) : cond;

					if (combined is null)
					{
						combined = part;
					}
					else
					{
						combined = LogicalOrExpression(
							NeedsParenthesesInOrContext(combined) ? ParenthesizedExpression(combined) : combined,
							NeedsParenthesesInOrContext(part) ? ParenthesizedExpression(part) : part);
					}
				}

				var visitedCombined = visit(combined) as ExpressionSyntax ?? combined!;

				result.Add(IfStatement(
					visitedCombined,
					ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))));
			}
			else if (run.Count > 0)
			{
				// Run is all-same-value — add back unchanged
				for (var k = runStart; k < runStart + run.Count; k++)
				{
					result.Add(statements[k]);
				}
			}
			else
			{
				// Current statement is not a bool-return if — add it as-is and advance
				result.Add(statements[i]);
				i++;
			}
		}

		return List(result);
	}

	/// <summary>
	/// Tries to extract the condition and boolean return value from an if-statement whose body
	/// is a single <c>return true;</c> or <c>return false;</c> (with or without braces) and
	/// that has no else clause.
	/// </summary>
	private static bool TryGetIfBoolReturnBody(
		StatementSyntax statement,
		out ExpressionSyntax? condition,
		out bool returnValue)
	{
		condition = null;
		returnValue = false;

		if (statement is not IfStatementSyntax { Else: null } ifStmt)
		{
			return false;
		}

		var ret = ifStmt.Statement switch
		{
			ReturnStatementSyntax r => r,
			BlockSyntax { Statements: [ ReturnStatementSyntax r ] } => r,
			_ => null
		};

		if (ret?.Expression is not LiteralExpressionSyntax lit)
		{
			return false;
		}

		if (lit.IsKind(SyntaxKind.TrueLiteralExpression))
		{
			condition = ifStmt.Condition;
			returnValue = true;
			return true;
		}

		if (lit.IsKind(SyntaxKind.FalseLiteralExpression))
		{
			condition = ifStmt.Condition;
			returnValue = false;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Negates a condition, preferring a direct inversion (e.g., <c>n &lt;= 3</c> → <c>n &gt; 3</c>)
	/// over wrapping in <c>!(…)</c>.
	/// </summary>
	private static ExpressionSyntax NegateCondition(ExpressionSyntax condition)
	{
		if (InvertLogicalRefactoring.TryInvertLogical(condition as BinaryExpressionSyntax, out var inverted))
		{
			return inverted;
		}

		return NegateExpressionRefactoring.Negate(condition);
	}

	/// <summary>
	/// Combines consecutive if statements that have identical bodies into a single if statement.
	/// Two strategies are applied in order:
	/// 1. Equality pattern: conditions of the form <c>x == literal</c> against the same variable
	///    are combined into <c>if (x is 1 or 5) { … }</c>.
	/// 2. General ||: when the body ends with a jump statement (return / break / continue / throw),
	///    any consecutive if statements with an identical body are combined using <c>||</c>.
	/// Example (strategy 1): if (1 == x) { return true; } if (5 == x) { return true; }
	///                     => if (x is 1 or 5) { return true; }
	/// Example (strategy 2): if (x &gt; 5) { return; } if (y &lt; 3) { return; }
	///                     => if (x &gt; 5 || y &lt; 3) { return; }
	/// </summary>
	internal static SyntaxList<StatementSyntax> CombineConsecutiveIfStatements(SyntaxList<StatementSyntax> statements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			// Only process if statements without an else clause
			if (statements[i] is not IfStatementSyntax { Else: null } currentIf)
			{
				result.Add(statements[i]);
				i++;

				continue;
			}

			// Strategy 1: equality (is or) combination
			if (TryGetEqualityComparisonInfo(currentIf.Condition, out var targetIdentifier, out var firstLiteral))
			{
				var literals = new List<LiteralExpressionSyntax> { firstLiteral! };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (!SyntaxNodeComparer.Get<StatementSyntax>().Equals(nextIf.Statement, currentIf.Statement))
					{
						break;
					}

					if (!TryGetEqualityComparisonInfo(nextIf.Condition, out var nextTarget, out var nextLiteral)
					    || nextTarget != targetIdentifier)
					{
						break;
					}

					literals.Add(nextLiteral!);
					j++;
				}

				if (literals.Count > 1)
				{
					// e.g. target is 1 or 5 or 10
					var combinedCondition = visit(CreateIsOrPattern(targetIdentifier!, literals)) as ExpressionSyntax;
					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			// Strategy 2: general || combination when body ends with a jump statement
			if (ContainsJumpStatement(currentIf.Statement))
			{
				var conditions = new List<ExpressionSyntax> { currentIf.Condition };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (!SyntaxNodeComparer.Get<StatementSyntax>().Equals(nextIf.Statement, currentIf.Statement))

					{
						break;
					}

					conditions.Add(nextIf.Condition);
					j++;
				}

				if (conditions.Count > 1)
				{
					var combinedCondition = conditions[0];

					for (var k = 1; k < conditions.Count; k++)
					{
						combinedCondition = LogicalOrExpression(
							NeedsParenthesesInOrContext(combinedCondition)
								? ParenthesizedExpression(combinedCondition)
								: combinedCondition,
							NeedsParenthesesInOrContext(conditions[k])
								? ParenthesizedExpression(conditions[k])
								: conditions[k]);
					}

					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			result.Add(statements[i]);
			i++;
		}

		return List(result);
	}

	/// <summary>
	/// Returns <see langword="true"/> when a statement unconditionally ends with a jump
	/// (return, break, continue, or throw), making it safe to combine consecutive if
	/// statements with identical bodies using <c>||</c>.
	/// </summary>
	private static bool ContainsJumpStatement(StatementSyntax statement) =>
		statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && ContainsJumpStatement(block.Statements.Last()),
			_ => false
		};

	/// <summary>
	/// Returns <see langword="true"/> when an expression needs parentheses when used as an
	/// operand of <c>||</c> (i.e., its precedence is lower than logical-or).
	/// </summary>
	private static bool NeedsParenthesesInOrContext(ExpressionSyntax expression) =>
		expression is ConditionalExpressionSyntax or AssignmentExpressionSyntax;

	/// <summary>
	/// Creates an 'is' pattern expression with 'or' for multiple values.
	/// Example: target is 1 or 5 or 10
	/// </summary>
	private static ExpressionSyntax CreateIsOrPattern(string targetIdentifier, List<LiteralExpressionSyntax> literals)
	{
		// Build pattern: 1 or 5 or 10 or ...
		PatternSyntax pattern = ConstantPattern(literals[0]);

		for (var k = 1; k < literals.Count; k++)
		{
			pattern = BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				ConstantPattern(literals[k]));
		}

		// Create: target is <pattern>
		return IsPatternExpression(
			IdentifierName(targetIdentifier),
			pattern);
	}

	/// <summary>
	/// Tries to extract the comparison target identifier and literal value from an equality expression.
	/// Handles both 'value == target' and 'target == value' formats.
	/// </summary>
	private static bool TryGetEqualityComparisonInfo(ExpressionSyntax condition, out string? targetIdentifier, out LiteralExpressionSyntax? literal)
	{
		targetIdentifier = null;
		literal = null;

		if (condition is not BinaryExpressionSyntax binary
		    || !binary.IsKind(SyntaxKind.EqualsExpression))
		{
			return false;
		}

		switch (binary)
		{
			// Check if left side is identifier and right side is literal
			case { Left: IdentifierNameSyntax leftId, Right: LiteralExpressionSyntax rightLit }:
			{
				targetIdentifier = leftId.Identifier.Text;
				literal = rightLit;
				return true;
			}
			// Check if right side is identifier and left side is literal
			case { Right: IdentifierNameSyntax rightId, Left: LiteralExpressionSyntax leftLit }:
			{
				targetIdentifier = rightId.Identifier.Text;
				literal = leftLit;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		var visitedExpression = Visit(node.Expression);

		// If the return expression is parenthesized, unwrap it to avoid returning nested parentheses that can interfere with pattern matching and other optimizations.
		if (visitedExpression is ParenthesizedExpressionSyntax parenthesized)
		{
			visitedExpression = parenthesized.Expression;
		}

		if (visitedExpression is ThrowExpressionSyntax throwExpression)
		{
			return ThrowStatement(throwExpression.Expression);
		}

		return node.WithExpression(visitedExpression as ExpressionSyntax);
	}

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		var visited = Visit(node.Declaration);

		return visited switch
		{
			null => null,
			BlockSyntax block => block,
			VariableDeclarationSyntax declaration => node.WithDeclaration(declaration),
			ExpressionStatementSyntax expressionStatement => expressionStatement,
			ThrowStatementSyntax throwStatement => throwStatement,
			_ => node
		};
	}
}