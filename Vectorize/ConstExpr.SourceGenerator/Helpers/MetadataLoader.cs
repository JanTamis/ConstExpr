using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

/// <summary>
/// Represents a metadata loader for retrieving types from loaded assemblies in a metadata load context.
/// </summary>
public class MetadataLoader : IDisposable
{
	private static Dictionary<Compilation, AppDomain> _loaders = new Dictionary<Compilation, AppDomain>();

	private readonly AppDomain _appDomain;

	/// <summary>
	/// Gets all assemblies loaded in the current metadata context.
	/// </summary>
	public IEnumerable<Assembly> Assemblies => _appDomain.GetAssemblies();

	public static MetadataLoader GetLoader(Compilation compilation)
	{
		if (!_loaders.TryGetValue(compilation, out var domain))
		{
			var assemblies = compilation.References
				.OfType<PortableExecutableReference>()
				.Select(s => s.FilePath)
				.Where(w => !String.IsNullOrEmpty(w));

		 //var resolver = new PathAssemblyResolver(assemblies);

			//using (var metadataContext = new MetadataLoadContext(resolver))
			//{
				domain = AppDomain.CreateDomain(compilation.AssemblyName);

				foreach (var assembly in assemblies)
				{
					try
					{
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
					var data = File.ReadAllBytes(assembly);
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
					domain.Load(data);
					}
					catch (Exception e)
					{

					}
				}

				_loaders.Add(compilation, domain);
			//}
		}

		return new MetadataLoader(domain);
	}

	public static void RemoveLoader(Compilation compilation)
	{
		if (_loaders.TryGetValue(compilation, out var loader))
		{
			AppDomain.Unload(loader);
			_loaders.Remove(compilation);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataLoader"/> class.
	/// </summary>
	/// <param name="compilation">The compilation containing references to load.</param>
	private MetadataLoader(AppDomain domain)
	{
		_appDomain = domain;
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
		foreach (var assembly in _appDomain.GetAssemblies())
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

	public void Dispose()
	{
		AppDomain.Unload(_appDomain);

		var key = _loaders
			.Where(w => w.Value == _appDomain)
			.Select(s => s.Key)
			.First();

		_loaders.Remove(key);
	}
}