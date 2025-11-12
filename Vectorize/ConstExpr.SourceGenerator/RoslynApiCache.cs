using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Threading;
using ConstExpr.SourceGenerator.Comparers;

namespace ConstExpr.SourceGenerator;

/// <summary>
/// Thread-safe cache for Roslyn API results to avoid expensive repeated calls
/// </summary>
public sealed class RoslynApiCache
{
	private readonly ConcurrentDictionary<SyntaxNode, SymbolInfo> _symbolInfoCache = new(SyntaxNodeComparer<SyntaxNode>.Instance);
	private readonly ConcurrentDictionary<SyntaxNode, IOperation?> _operationCache = new(SyntaxNodeComparer<SyntaxNode>.Instance);

	public SymbolInfo GetOrAddSymbolInfo(SyntaxNode node, SemanticModel semanticModel, CancellationToken token)
	{
		return _symbolInfoCache.GetOrAdd(node, n => semanticModel.GetSymbolInfo(n, token));
	}

	public IOperation? GetOrAddOperation(SyntaxNode node, SemanticModel semanticModel, CancellationToken token)
	{
		return _operationCache.GetOrAdd(node, n => semanticModel.GetOperation(n, token));
	}

	public void Clear()
	{
		_symbolInfoCache.Clear();
		_operationCache.Clear();
	}
}
