using System.Collections.Generic;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Comparers;

file class SyntaxNodeComparer<TNode> : IEqualityComparer<TNode?> where TNode : SyntaxNode
{
	public static SyntaxNodeComparer<TNode> Instance { get; } = new();

	public bool Equals(TNode? x, TNode? y)
	{
		return SyntaxNodeComparer.Equals(x, y);
	}

	public int GetHashCode(TNode? obj)
	{
		return SyntaxNodeComparer.GetHashCode(obj);
	}
}

public static class SyntaxNodeComparer
{
	public static IEqualityComparer<TNode> Get<TNode>() where TNode : SyntaxNode
	{
		return SyntaxNodeComparer<TNode>.Instance;
	}

	public static IEqualityComparer<SyntaxNode> Get()
	{
		return SyntaxNodeComparer<SyntaxNode>.Instance;
	}

	public static bool Equals<TNode>(TNode? x, TNode? y) where TNode : SyntaxNode
	{
		return DeteministicHashVisitor.Instance.Visit(x) == DeteministicHashVisitor.Instance.Visit(y);
	}

	public static int GetHashCode<TNode>(TNode? obj) where TNode : SyntaxNode
	{
		return DeteministicHashVisitor.Instance.Visit(obj).GetHashCode();
	}
}