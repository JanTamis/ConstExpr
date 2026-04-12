using System.Collections.Generic;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Comparers;

file class SyntaxNodeComparer<TNode> : IEqualityComparer<TNode?> where TNode : SyntaxNode
{
	public static SyntaxNodeComparer<TNode> Instance { get; } = new SyntaxNodeComparer<TNode>();
	
	public bool Equals(TNode? x, TNode? y)
	{
		return DeteministicHashVisitor.Instance.Visit(x) == DeteministicHashVisitor.Instance.Visit(y);
	}

	public int GetHashCode(TNode? obj)
	{
		return (int)DeteministicHashVisitor.Instance.Visit(obj);
	}
}

public static class SyntaxNodeComparer
{
	public static IEqualityComparer<TNode> Get<TNode>() where TNode : SyntaxNode => SyntaxNodeComparer<TNode>.Instance;
	public static IEqualityComparer<SyntaxNode> Get() => SyntaxNodeComparer<SyntaxNode>.Instance;
}