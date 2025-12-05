using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
/// Represents a metadata loader for retrieving types from loaded assemblies in a metadata load context.
/// </summary>
public class MetadataLoader
{
	private static readonly ConditionalWeakTable<Compilation, MetadataLoader> _loaderCache = new();

	// Lazy-loaded assemblies - only load when needed
	private readonly HashSet<string> _assemblyPaths;
	private readonly Lazy<HashSet<Assembly>> _preloadedAssemblies;
	private readonly ConcurrentDictionary<string, Assembly?> _assemblyCache = new();
	private readonly ConcurrentDictionary<string, Type?> _typeCache = new();

	/// <summary>
	/// Gets all assemblies loaded in the current metadata context.
	/// Note: This triggers loading of all assemblies (use sparingly).
	/// </summary>
	public IEnumerable<Assembly> Assemblies => GetAllAssemblies();

	public static MetadataLoader GetLoader(Compilation compilation)
	{
		// Cache per-compilation to avoid repeatedly creating MetadataLoadContext and loading assemblies
		return _loaderCache.GetValue(compilation, static comp => CreateLoader(comp));
	}

	private static MetadataLoader CreateLoader(Compilation compilation)
	{
		// Get all assembly references from compilation (but don't load them yet!)
		var assemblyPaths = new HashSet<string>(
			compilation.References
				.OfType<PortableExecutableReference>()
				.Select(r => r.FilePath!)
				.Where(path => !String.IsNullOrEmpty(path)),
			StringComparer.OrdinalIgnoreCase);

		return new MetadataLoader(assemblyPaths);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataLoader"/> class with lazy loading.
	/// </summary>
	/// <param name="assemblyPaths">The paths to assemblies (loaded on-demand).</param>
	private MetadataLoader(HashSet<string> assemblyPaths)
	{
		_assemblyPaths = assemblyPaths;

		// Lazy-load AppDomain assemblies only when first accessed
		_preloadedAssemblies = new Lazy<HashSet<Assembly>>(() =>
		{
			var assemblies = new HashSet<Assembly>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				assemblies.Add(assembly);
			}
			return assemblies;
		});
	}

	/// <summary>
	/// Gets all assemblies (triggers loading if not already loaded).
	/// </summary>
	private IEnumerable<Assembly> GetAllAssemblies()
	{
		// Return preloaded assemblies
		foreach (var assembly in _preloadedAssemblies.Value)
		{
			yield return assembly;
		}

		// Load remaining assemblies from paths
		var resolver = new PathAssemblyResolver(_assemblyPaths);
		using var metadataContext = new MetadataLoadContext(resolver);

		foreach (var assembly in metadataContext.GetAssemblies())
		{
			var assemblyName = assembly.GetName();
			var loadedAssembly = LoadAssemblyByName(assemblyName);

			if (loadedAssembly != null)
			{
				yield return loadedAssembly;
			}
		}
	}

	/// <summary>
	/// Loads an assembly by name (cached for performance).
	/// </summary>
	private Assembly? LoadAssemblyByName(AssemblyName assemblyName)
	{
		var fullName = assemblyName.FullName;

		return _assemblyCache.GetOrAdd(fullName, _ =>
		{
			// Check if already in preloaded assemblies
			if (_preloadedAssemblies.IsValueCreated)
			{
				var existing = _preloadedAssemblies.Value
					.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), assemblyName));

				if (existing != null)
				{
					return existing;
				}
			}

			// Try to load the assembly
			try
			{
#pragma warning disable RS1035
				return Assembly.Load(assemblyName);
#pragma warning restore RS1035
			}
			catch
			{
				// Ignore load issues; best-effort
				return null;
			}
		});
	}

	/// <summary>
	/// Retrieves a <see cref="Type"/> from the loaded assemblies that corresponds to the provided type symbol.
	/// </summary>
	/// <param name="typeSymbol">The type symbol to find the corresponding Type for.</param>
	/// <returns>
	/// The <see cref="Type"/> that corresponds to the provided type symbol, or null if no matching type is found.
	/// </returns>
	public Type? GetType(ITypeSymbol? typeSymbol)
	{
		switch (typeSymbol)
		{
			case null:
			{
				return null;
			}
			case INamedTypeSymbol { Arity: > 0 } namedType when !SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom):
			{
				var constructedFrom = GetType(namedType.ConstructedFrom);

				return constructedFrom?.MakeGenericType(namedType.TypeArguments.Select(GetType).ToArray());
			}
			case IArrayTypeSymbol arrayType:
			{
				return GetType(arrayType.ElementType)?.MakeArrayType();
			}
		}

		var containingNamespace = typeSymbol.ContainingNamespace.ToString();
		var name = typeSymbol.Name;

		var fullTypeName = $"{containingNamespace}.{name}";

		if (typeSymbol is INamedTypeSymbol { Arity: > 0 } named)
		{
			fullTypeName += $"`{named.Arity}";
		}

		return GetType(fullTypeName);
	}

	/// <summary>
	/// Retrieves a <see cref="Type"/> from the loaded assemblies that corresponds to the provided type name.
	/// Uses lazy loading: searches preloaded assemblies first, then loads others on-demand.
	/// </summary>
	/// <param name="typeName">The fully qualified name of the type to find.</param>
	/// <returns>
	/// The <see cref="Type"/> that corresponds to the provided type name, or null if no matching type is found.
	/// </returns>
	public Type? GetType(string typeName)
	{
		return _typeCache.GetOrAdd(typeName, SearchForType);
	}

	/// <summary>
	/// Searches for a type across assemblies using a smart lazy-loading strategy.
	/// </summary>
	private Type? SearchForType(string typeName)
	{
		// Step 1: Search in preloaded assemblies first (fast path - no loading needed)
		if (_preloadedAssemblies.IsValueCreated || ShouldCheckPreloadedAssemblies(typeName))
		{
			foreach (var assembly in _preloadedAssemblies.Value)
			{
				var type = TryGetTypeFromAssembly(assembly, typeName);

				if (type != null)
				{
					return type;
				}
			}
		}

		// Step 2: If not found, search in assemblies from compilation references
		// Only load assemblies on-demand as we search
		var resolver = new PathAssemblyResolver(_assemblyPaths);
		using var metadataContext = new MetadataLoadContext(resolver);

		foreach (var metadataAssembly in metadataContext.GetAssemblies())
		{
			// Quick check: does the type name match the assembly's namespace patterns?
			if (!CouldContainType(metadataAssembly, typeName))
			{
				continue;
			}

			// Load the actual assembly only if it might contain our type
			var assembly = LoadAssemblyByName(metadataAssembly.GetName());

			if (assembly != null)
			{
				var type = TryGetTypeFromAssembly(assembly, typeName);

				if (type != null)
				{
					return type;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Determines if we should check preloaded assemblies for common types.
	/// </summary>
	private static bool ShouldCheckPreloadedAssemblies(string typeName)
	{
		// Common namespaces that are usually in AppDomain
		return typeName.StartsWith("System.")
		       || typeName.StartsWith("Microsoft.")
		       || typeName.StartsWith("ConstExpr.");
	}

	/// <summary>
	/// Quick heuristic check if an assembly could contain a type.
	/// </summary>
	private static bool CouldContainType(Assembly assembly, string typeName)
	{
		// Simple heuristic: check if assembly name prefix matches type namespace
		var assemblyName = assembly.GetName().Name;

		if (string.IsNullOrEmpty(assemblyName))
		{
			return true;
		}

		// Extract namespace from type name
		var lastDot = typeName.LastIndexOf('.');

		if (lastDot <= 0)
		{
			return true; // No namespace, could be anywhere
		}

		var typeNamespace = typeName.Substring(0, lastDot);

		// Check if assembly name is related to type namespace
		return typeNamespace.StartsWith(assemblyName, StringComparison.OrdinalIgnoreCase)
		       || assemblyName.StartsWith(typeNamespace, StringComparison.OrdinalIgnoreCase)
		       || assemblyName.Contains("mscorlib", StringComparison.OrdinalIgnoreCase) // System types
		       || assemblyName.Contains("System", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Safely tries to get a type from an assembly.
	/// </summary>
	private static Type? TryGetTypeFromAssembly(Assembly assembly, string typeName)
	{
		try
		{
			return assembly.GetType(typeName, throwOnError: false);
		}
		catch
		{
			// Skip problematic assemblies
			return null;
		}
	}

	public Type GetTupleType(int tupleTypesLength)
	{
		if (tupleTypesLength is < 1 or > 8)
		{
			throw new ArgumentOutOfRangeException(nameof(tupleTypesLength), "Tuple length must be between 1 and 8.");
		}

		var tupleTypeName = $"System.ValueTuple`{tupleTypesLength}";
		var tupleType = GetType(tupleTypeName);

		if (tupleType == null)
		{
			throw new InvalidOperationException($"Could not find type '{tupleTypeName}'.");
		}

		return tupleType;
	}
}