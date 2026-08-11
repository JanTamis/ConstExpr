using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Lightweight syntax walker that collects all variable read and write locations in a single pass.
///   This is used for the Mark-and-Sweep pruning pattern.
/// </summary>
public sealed class VariableUsageCollector(IEnumerable<string> trackedVariables) : CSharpSyntaxWalker
{
	private readonly HashSet<string> _trackedVariables = new(trackedVariables);

	/// <summary>
	///   Variables that are read (used as values) with their read counts.
	/// </summary>
	public Dictionary<string, int> ReadVariables { get; } = [ ];

	/// <summary>
	///   Variables that are written to (assigned, incremented, etc.) with their write counts.
	/// </summary>
	public Dictionary<string, int> WrittenVariables { get; } = [ ];

	/// <summary>
	///   Variables that are passed by ref or out with their counts.
	/// </summary>
	public Dictionary<string, int> RefVariables { get; } = [ ];

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		var name = node.Identifier.Text;

		if (!_trackedVariables.Contains(name))
		{
			return;
		}

		// A compound assignment (`x += value`) both reads the prior value of x and writes the
		// result — unlike a simple assignment (`x = value`), which only writes. Counting it as a
		// pure write would let a pruner conclude the variable is "never read" from a compound-only
		// usage site, wrongly treating a real read-modify-write as a removable dead store.
		if (IsCompoundAssignmentTarget(node))
		{
			ReadVariables.TryGetValue(name, out var compoundReadCount);
			ReadVariables[name] = compoundReadCount + 1;

			WrittenVariables.TryGetValue(name, out var compoundWriteCount);
			WrittenVariables[name] = compoundWriteCount + 1;
			return;
		}

		if (IsWriteContext(node))
		{
			WrittenVariables.TryGetValue(name, out var writeCount);
			WrittenVariables[name] = writeCount + 1;
		}
		else
		{
			ReadVariables.TryGetValue(name, out var readCount);
			ReadVariables[name] = readCount + 1;
		}
	}

	private static bool IsCompoundAssignmentTarget(IdentifierNameSyntax node)
	{
		return node.Parent is AssignmentExpressionSyntax { Left: var left } assignment
		       && left == node
		       && !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
	}

	public override void VisitArgument(ArgumentSyntax node)
	{
		// Check for ref/out arguments
		if (node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword, SyntaxKind.OutKeyword)
		    && node.Expression is IdentifierNameSyntax id
		    && _trackedVariables.Contains(id.Identifier.Text))
		{
			var name = id.Identifier.Text;

			RefVariables.TryGetValue(name, out var refCount);
			RefVariables[name] = refCount + 1;

			WrittenVariables.TryGetValue(name, out var writeCount);
			WrittenVariables[name] = writeCount + 1;
		}

		base.VisitArgument(node);
	}

	/// <summary>
	///   Determines if the identifier is being written to (and, unlike <see cref="IsCompoundAssignmentTarget" />,
	///   only written to — not also read) based on its syntactic context. Compound assignment targets are
	///   handled separately in <see cref="VisitIdentifierName" /> before this is ever consulted.
	/// </summary>
	private static bool IsWriteContext(IdentifierNameSyntax node)
	{
		var parent = node.Parent;

		return parent switch
		{
			// Direct assignment: x = value
			AssignmentExpressionSyntax { Left: var left } when left == node => true,

			// Postfix: x++, x--
			PostfixUnaryExpressionSyntax { Operand: var operand } when operand == node => true,

			// Prefix: ++x, --x
			PrefixUnaryExpressionSyntax prefix when prefix.Operand == node &&
			                                        (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) => true,

			// Tuple deconstruction: (x, y) = tuple
			ArgumentSyntax { Parent: TupleExpressionSyntax { Parent: AssignmentExpressionSyntax { Left: var left } } }
				when left.Contains(node) => true,

			// A mutating call's receiver (`outliers.Add(x)`) mutates the variable's contents without
			// reading its current value as an operand anywhere - closer to a write than a read. Without
			// this, a local built up purely via calls like Add (never otherwise read) looks perpetually
			// "read" by its own mutating calls, so DeadCodePruner can never prune it even once every
			// other use of it has folded away. Matched by method name, not symbol - this is a syntax-only
			// walker with no semantic model - mirroring ConstExprPartialRewriter.IsLikelyMutatingMethod's
			// own name-based fallback list.
			MemberAccessExpressionSyntax { Expression: var receiver, Name.Identifier.Text: var mutatingMemberName } member
				when receiver == node && member.Parent is InvocationExpressionSyntax && IsLikelyMutatingMethodName(mutatingMemberName) => true,

			// Declaration: var x = value (this is initialization, not really a "write" for pruning purposes)
			// We don't count this as a write since declarations are handled separately

			_ => false
		};
	}

	private static bool IsLikelyMutatingMethodName(string methodName)
	{
		return methodName is "Add" or "AddRange" or "Insert" or "InsertRange" or "Remove" or "RemoveAt" or "RemoveAll"
			or "RemoveRange" or "Clear" or "Sort" or "Enqueue" or "Dequeue" or "Push" or "Pop"
			or "TryAdd" or "TryTake" or "UnionWith" or "IntersectWith" or "ExceptWith" or "SymmetricExceptWith"
			or "Append" or "AppendLine" or "AppendFormat" or "AppendJoin" or "AppendLiteral"
			or "Write" or "WriteLine" or "Flush";
	}

	public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
	{
		// dont test the name as a read
		Visit(node.Expression);
	}

	/// <summary>
	///   Checks if a variable is only written to and never read (dead store).
	/// </summary>
	public bool IsDeadStore(string variableName)
	{
		return WrittenVariables.ContainsKey(variableName) && !ReadVariables.ContainsKey(variableName);
	}

	/// <summary>
	///   Checks if a variable is never used at all after declaration.
	/// </summary>
	public bool IsUnused(string variableName)
	{
		return !ReadVariables.ContainsKey(variableName) && !WrittenVariables.ContainsKey(variableName);
	}

	/// <summary>
	///   Checks if a variable can be safely pruned (never read).
	/// </summary>
	public bool CanBePruned(string variableName)
	{
		// Can prune if never read and not passed by ref/out
		return !ReadVariables.ContainsKey(variableName) && !RefVariables.ContainsKey(variableName);
	}

	/// <summary>
	///   Gets the number of times a variable was read.
	/// </summary>
	public int GetReadCount(string variableName)
	{
		return ReadVariables.TryGetValue(variableName, out var count) ? count : 0;
	}

	/// <summary>
	///   Gets the number of times a variable was written to.
	/// </summary>
	public int GetWriteCount(string variableName)
	{
		return WrittenVariables.TryGetValue(variableName, out var count) ? count : 0;
	}

	/// <summary>
	///   Gets the number of times a variable was passed by ref or out.
	/// </summary>
	public int GetRefCount(string variableName)
	{
		return RefVariables.TryGetValue(variableName, out var count) ? count : 0;
	}
}