using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Lightweight syntax walker that collects all variable read and write locations in a single pass.
/// This is used for the Mark-and-Sweep pruning pattern.
/// </summary>
public sealed class VariableUsageCollector(IEnumerable<string> trackedVariables) : CSharpSyntaxWalker
{
	private readonly HashSet<string> _trackedVariables = new(trackedVariables);

	/// <summary>
	/// Variables that are read (used as values).
	/// </summary>
	public HashSet<string> ReadVariables { get; } = [];

	/// <summary>
	/// Variables that are written to (assigned, incremented, etc.).
	/// </summary>
	public HashSet<string> WrittenVariables { get; } = [];

	/// <summary>
	/// Variables that are passed by ref or out.
	/// </summary>
	public HashSet<string> RefVariables { get; } = [];

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		var name = node.Identifier.Text;

		if (!_trackedVariables.Contains(name))
		{
			base.VisitIdentifierName(node);
			return;
		}

		if (IsWriteContext(node))
		{
			WrittenVariables.Add(name);
		}
		else
		{
			ReadVariables.Add(name);
		}

		base.VisitIdentifierName(node);
	}

	public override void VisitArgument(ArgumentSyntax node)
	{
		// Check for ref/out arguments
		if (node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword) || 
		    node.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
		{
			if (node.Expression is IdentifierNameSyntax id && _trackedVariables.Contains(id.Identifier.Text))
			{
				RefVariables.Add(id.Identifier.Text);
				WrittenVariables.Add(id.Identifier.Text);
			}
		}

		base.VisitArgument(node);
	}

	/// <summary>
	/// Determines if the identifier is being written to based on its syntactic context.
	/// </summary>
	private static bool IsWriteContext(IdentifierNameSyntax node)
	{
		var parent = node.Parent;

		return parent switch
		{
			// Direct assignment: x = value
			AssignmentExpressionSyntax { Left: var left } when left == node => true,

			// Compound assignment: x += value, x -= value, etc.
			AssignmentExpressionSyntax { Left: var left } assignment 
				when left == node && !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => true,

			// Postfix: x++, x--
			PostfixUnaryExpressionSyntax { Operand: var operand } when operand == node => true,

			// Prefix: ++x, --x
			PrefixUnaryExpressionSyntax prefix when prefix.Operand == node &&
				(prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) => true,

			// Tuple deconstruction: (x, y) = tuple
			ArgumentSyntax { Parent: TupleExpressionSyntax { Parent: AssignmentExpressionSyntax { Left: var left } } }
				when left.Contains(node) => true,

			// Declaration: var x = value (this is initialization, not really a "write" for pruning purposes)
			// We don't count this as a write since declarations are handled separately

			_ => false
		};
	}

	public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
	{
		// dont test the name as a read
		Visit(node.Expression);
	}

	/// <summary>
	/// Checks if a variable is only written to and never read (dead store).
	/// </summary>
	public bool IsDeadStore(string variableName)
	{
		return WrittenVariables.Contains(variableName) && !ReadVariables.Contains(variableName);
	}

	/// <summary>
	/// Checks if a variable is never used at all after declaration.
	/// </summary>
	public bool IsUnused(string variableName)
	{
		return !ReadVariables.Contains(variableName) && !WrittenVariables.Contains(variableName);
	}

	/// <summary>
	/// Checks if a variable can be safely pruned (never read).
	/// </summary>
	public bool CanBePruned(string variableName)
	{
		// Can prune if never read and not passed by ref/out
		return !ReadVariables.Contains(variableName) && !RefVariables.Contains(variableName);
	}
}

