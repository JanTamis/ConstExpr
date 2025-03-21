using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
/// Represents a metadata loader for retrieving types from loaded assemblies in a metadata load context.
/// </summary>
public class MetadataLoader
{
	private readonly MetadataLoadContext _metadataLoadContext;

	/// <summary>
	/// Gets all assemblies loaded in the current metadata context.
	/// </summary>
	public IEnumerable<Assembly> Assemblies => _metadataLoadContext.GetAssemblies();

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataLoader"/> class.
	/// </summary>
	/// <param name="compilation">The compilation containing references to load.</param>
	public MetadataLoader(Compilation compilation)
	{
		var assemblies = compilation.References
			.OfType<PortableExecutableReference>()
			.Select(reference => reference.FilePath)
			.Where(path => !String.IsNullOrEmpty(path));

		var resolver = new PathAssemblyResolver(assemblies);

		_metadataLoadContext = new MetadataLoadContext(resolver);
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
		var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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
		foreach (var assembly in _metadataLoadContext.GetAssemblies())
		{
			var type = assembly.GetType(typeName);

			if (type == null)
			{
				continue;
			}

			return type;
		}

		return null;
	}

	/// <summary>
	/// Releases all resources used by the <see cref="MetadataLoader"/> instance.
	/// </summary>
	public void Dispose()
	{
		_metadataLoadContext.Dispose();
	}
}