using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Comparers;

public class SyntaxNodeComparer<TNode> : IEqualityComparer<TNode?> where TNode : SyntaxNode
{
	public static SyntaxNodeComparer<TNode> Instance { get; } = new SyntaxNodeComparer<TNode>();
	
	public bool Equals(TNode? x, TNode? y)
	{
		return SyntaxFactory.AreEquivalent(x, y);
	}

	public int GetHashCode(TNode? obj)
	{
		return obj?.ToString().GetHashCode() ?? 0;
	}
}