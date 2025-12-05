using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
/// Analyzes the call graph to determine which methods are actually invoked in a compilation.
/// Results are cached for performance. Uses recursive analysis to trace through call chains.
/// </summary>
public sealed class CallGraphAnalyzer
{
	private readonly ConcurrentDictionary<IMethodSymbol, bool> _invocationCache;
	private readonly Compilation _compilation;

	public CallGraphAnalyzer(Compilation compilation)
	{
		_compilation = compilation;
		_invocationCache = new ConcurrentDictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default);
	}

	/// <summary>
	/// Checks if a method is invoked anywhere in the compilation (recursively through call chain), with caching.
	/// </summary>
	public bool IsMethodInvoked(IMethodSymbol method, CancellationToken token = default)
	{
		if (method == null)
		{
			return false;
		}

		// Check cache first
		if (_invocationCache.TryGetValue(method, out var cached))
		{
			return cached;
		}

		// Public API methods are considered invoked
		if (IsPublicApiMethod(method))
		{
			_invocationCache.TryAdd(method, true);
			return true;
		}

		// Use recursive search with visited set to prevent infinite loops
		var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var result = IsMethodInvokedRecursive(method, visited, token);
		_invocationCache.TryAdd(method, result);
		return result;
	}

	/// <summary>
	/// Checks if the method containing the given invocation is actually invoked.
	/// Supports methods, local functions, and global statements.
	/// </summary>
	public bool IsContainingMethodInvoked(InvocationExpressionSyntax invocation, CancellationToken token = default)
	{
		// Check for global statement - always considered invoked (it's top-level code)
		var globalStatement = invocation.Ancestors().OfType<GlobalStatementSyntax>().FirstOrDefault();
		if (globalStatement != null)
		{
			return true;
		}

		// Find the containing method (could be a regular method, local function, etc.)
		var containingMethod = invocation.Ancestors()
			.OfType<BaseMethodDeclarationSyntax>()
			.FirstOrDefault();

		// Also check for LocalFunctionStatementSyntax
		var localFunction = containingMethod == null 
			? invocation.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault()
			: null;

		if (containingMethod == null && localFunction == null)
		{
			// Not in a method or local function (e.g., field initializer) - consider invoked
			return true;
		}

		SyntaxNode nodeToCheck = (SyntaxNode?)containingMethod ?? localFunction!;
		var tree = nodeToCheck.SyntaxTree;

		if (!_compilation.SyntaxTrees.Contains(tree))
		{
			return true;
		}

		var semanticModel = _compilation.GetSemanticModel(tree);
		var methodSymbol = semanticModel.GetDeclaredSymbol(nodeToCheck, token) as IMethodSymbol;
		if (methodSymbol == null)
		{
			return true;
		}

		return IsMethodInvoked(methodSymbol, token);
	}

	/// <summary>
	/// Clears the cache. Call this when compilation changes.
	/// </summary>
	public void ClearCache()
	{
		_invocationCache.Clear();
	}

	/// <summary>
	/// Recursively checks if a method is invoked, following the call chain up to entry points.
	/// </summary>
	private bool IsMethodInvokedRecursive(IMethodSymbol targetMethod, HashSet<IMethodSymbol> visited, CancellationToken token)
	{
		// Prevent infinite recursion
		if (!visited.Add(targetMethod))
		{
			return false;
		}

		// Find all methods that call the target method
		var callers = FindCallingMethods(targetMethod, token);

		foreach (var caller in callers)
		{
			if (token.IsCancellationRequested)
			{
				return true; // Assume invoked if cancelled
			}

			// If the caller is a public API method, the target is invoked
			if (IsPublicApiMethod(caller))
			{
				return true;
			}

			// Recursively check if the caller is invoked
			if (IsMethodInvokedRecursive(caller, visited, token))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Finds all methods in the compilation that invoke the target method.
	/// Checks regular methods, local functions, and global statements.
	/// </summary>
	private IEnumerable<IMethodSymbol> FindCallingMethods(IMethodSymbol targetMethod, CancellationToken token)
	{
		var callers = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

		foreach (var tree in _compilation.SyntaxTrees)
		{
			if (token.IsCancellationRequested)
			{
				yield break;
			}

			if (!_compilation.SyntaxTrees.Contains(tree))
			{
				continue;
			}

			var semanticModel = _compilation.GetSemanticModel(tree);
			var root = tree.GetRoot(token);
			
			// Find all invocations in this tree
			var invocations = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>();

			foreach (var invocation in invocations)
			{
				if (token.IsCancellationRequested)
				{
					yield break;
				}

				var symbol = semanticModel.GetSymbolInfo(invocation, token).Symbol as IMethodSymbol;
				
				// Check if this invocation calls our target method
				if (symbol != null && 
				    (SymbolEqualityComparer.Default.Equals(symbol, targetMethod) ||
				     (symbol.OriginalDefinition != null && 
				      SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, targetMethod.OriginalDefinition))))
				{
					// Check if invocation is in a global statement - if so, it's always invoked
					var globalStatement = invocation.Ancestors().OfType<GlobalStatementSyntax>().FirstOrDefault();
					if (globalStatement != null)
					{
						// Global statements are top-level code, so we treat them as entry points
						// Return the entry point method to represent the global scope
						var entryPoint = _compilation.GetEntryPoint(token);
						if (entryPoint != null && callers.Add(entryPoint))
						{
							yield return entryPoint;
						}
						continue;
					}

					// Find the containing method or local function of this invocation
					var containingMethod = invocation.Ancestors()
						.OfType<BaseMethodDeclarationSyntax>()
						.FirstOrDefault();

					var localFunction = containingMethod == null
						? invocation.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault()
						: null;

					var nodeToCheck = (SyntaxNode?)containingMethod ?? localFunction;

					if (nodeToCheck != null)
					{
						var callingMethod = semanticModel.GetDeclaredSymbol(nodeToCheck, token) as IMethodSymbol;
						if (callingMethod != null && callers.Add(callingMethod))
						{
							yield return callingMethod;
						}
					}
				}
			}
		}
	}

	private bool IsPublicApiMethod(IMethodSymbol method)
	{
		// Public methods in public types
		if (method.DeclaredAccessibility == Accessibility.Public)
		{
			var containingType = method.ContainingType;
			while (containingType != null)
			{
				if (containingType.DeclaredAccessibility != Accessibility.Public)
				{
					return false;
				}
				containingType = containingType.ContainingType;
			}
			return true;
		}

		// Entry point methods
		if (method.Name == "Main" && method.IsStatic)
		{
			return true;
		}

		// Entry point for global statements (synthesized Main method)
		if (method.Name == "<Main>$")
		{
			return true;
		}

		// Test methods
		var testAttributes = new[] { "Test", "TestMethod", "Fact", "Theory", "TestCase" };
		if (method.GetAttributes().Any(attr => 
			testAttributes.Any(ta => attr.AttributeClass?.Name.Contains(ta) == true)))
		{
			return true;
		}

		// Methods with special attributes that indicate they're entry points
		var entryPointAttributes = new[] { "EntryPoint", "WebMethod", "Action", "HttpGet", "HttpPost" };
		if (method.GetAttributes().Any(attr => 
			entryPointAttributes.Any(ea => attr.AttributeClass?.Name.Contains(ea) == true)))
		{
			return true;
		}

		return false;
	}
}
