using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Comparers;

public class SyntaxNodeComparer<TNode> : IEqualityComparer<TNode?> where TNode : SyntaxNode
{
	public static SyntaxNodeComparer<TNode> Instance { get; } = new SyntaxNodeComparer<TNode>();
	
	public bool Equals(TNode? x, TNode? y)
	{
		return AreEquivalent(x, y);
	}

	public int GetHashCode(TNode? obj)
	{
		return obj?.ToString().GetHashCode() ?? 0;
	}
}