using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
/// Represents a metadata loader for retrieving types from loaded assemblies in a metadata load context.
/// </summary>
public class MetadataLoader
{
	// private static readonly ConcurrentDictionary<Compilation, IList<Assembly>> _loaders = new();

	private readonly IList<Assembly> _assemblies;
	private readonly ConcurrentDictionary<string, Type?> _typeCache = new();

	/// <summary>
	/// Gets all assemblies loaded in the current metadata context.
	/// </summary>
	public IEnumerable<Assembly> Assemblies => _assemblies;

	public static MetadataLoader GetLoader(Compilation compilation)
	{
		var assemblies = compilation.References
			.OfType<PortableExecutableReference>()
			.Select(s => s.FilePath)
			.Where(w => !String.IsNullOrEmpty(w))
			.ToList();

		var resolver = new PathAssemblyResolver(assemblies);
		var resultAssemblies = new List<Assembly>();

		using (var metadataContext = new MetadataLoadContext(resolver))
		{
			foreach (var assembly in metadataContext.GetAssemblies())
			{
				try
				{
#pragma warning disable RS1035
					var loadedAssembly = Assembly.Load(assembly.GetName());
					resultAssemblies.Add(loadedAssembly);
#pragma warning restore RS1035
				}
				catch (Exception e)
				{
					// Could add logging or diagnostics here if needed
				}
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!resultAssemblies.Contains(assembly))
				{
					resultAssemblies.Add(assembly);
				}
			}

			return new MetadataLoader(resultAssemblies);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataLoader"/> class.
	/// </summary>
	/// <param name="assemblies">The assemblies to load.</param>
	private MetadataLoader(IList<Assembly> assemblies)
	{
		_assemblies = assemblies;
	}

	/// <summary>
	/// Retrieves a <see cref="Type"/> from the loaded assemblies that corresponds to the provided type symbol.
	/// </summary>
	/// <param name="typeSymbol">The type symbol to find the corresponding Type for.</param>
	/// <returns>
	/// The <see cref="Type"/> that corresponds to the provided type symbol, or null if no matching type is found.
	/// </returns>
	public Type? GetType(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is INamedTypeSymbol { Arity: > 0 } namedType && !SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom))
		{
			var constuctedFrom = GetType(namedType.ConstructedFrom);

			return constuctedFrom.MakeGenericType(namedType.TypeArguments.Select(GetType).ToArray());
		}

		if (typeSymbol is IArrayTypeSymbol arrayType)
		{
			var type = GetType(arrayType.ElementType)
				.MakeArrayType();

			return type;
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
	/// </summary>
	/// <param name="typeName">The fully qualified name of the type to find.</param>
	/// <returns>
	/// The <see cref="Type"/> that corresponds to the provided type name, or null if no matching type is found.
	/// </returns>
	public Type? GetType(string typeName)
	{
		return _typeCache.GetOrAdd(typeName, _ =>
		{
			foreach (var assembly in _assemblies)
			{
				try
				{
					var type = assembly.GetType(typeName, false);

					if (type != null)
					{
						return type;
					}
				}
				catch
				{
					// Skip problematic assemblies
				}
			}
			return null;
		});
	}
}