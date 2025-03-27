using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Extensions;

public static class CompilationExtensions
{
	public static INamedTypeSymbol CreateIEnumerable(this Compilation compilation, ITypeSymbol elementType)
	{
		return compilation
			.CreateSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T, elementType);
	}

	public static INamedTypeSymbol CreateBoolean(this Compilation compilation)
	{
		return compilation
			.CreateSpecialType(SpecialType.System_Boolean);
	}

	public static INamedTypeSymbol CreateInt32(this Compilation compilation)
	{
		return compilation
			.CreateSpecialType(SpecialType.System_Int32);
	}

	public static INamedTypeSymbol CreateInt64(this Compilation compilation)
	{
		return compilation
			.CreateSpecialType(SpecialType.System_Int64);
	}

	public static INamedTypeSymbol CreateFunc(this Compilation compilation, params ITypeSymbol[] typeArguments)
	{
		return compilation.GetTypeByMetadataName($"System.Func`{typeArguments.Length}")
			.Construct(typeArguments);
	}

	public static INamedTypeSymbol CreateAction(this Compilation compilation, params ITypeSymbol[] typeArguments)
	{
		return compilation.GetTypeByMetadataName($"System.Action`{typeArguments.Length}")
			.Construct(typeArguments);
	}

	public static INamedTypeSymbol CreateSpecialType(this Compilation compilation, SpecialType specialType, params ITypeSymbol[] typeArguments)
	{
		if (typeArguments.Length == 0)
		{
			return compilation
				.GetSpecialType(specialType);
		}

		return compilation
			.GetSpecialType(specialType)
			.Construct(typeArguments);
	}

	public static bool IsSpecialType(this Compilation compilation, ISymbol symbol, SpecialType specialType)
	{
		if (symbol is ITypeSymbol namedTypeSymbol)
		{
			return namedTypeSymbol.SpecialType == specialType;
		}

		return SymbolEqualityComparer.Default.Equals(symbol, compilation.GetSpecialType(specialType));
	}

	public static bool IsSpanType(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		return typeSymbol is INamedTypeSymbol { Arity: 1 } namedTypeSymbol
		       && namedTypeSymbol.ContainingNamespace.ToString() == "System"
		       && namedTypeSymbol.Name is "Span" or "ReadOnlySpan"
		       && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], elementType);
	}
	
	public static int GetByteSize(this Compilation compilation, MetadataLoader loader, ITypeSymbol type)
	{
		if (type == null)
		{
			return 0;
		}

		// Handle primitive types
		if (type.SpecialType != SpecialType.None)
		{
			return type.SpecialType switch
			{
				SpecialType.System_Boolean => sizeof(bool),
				SpecialType.System_Byte => sizeof(byte),
				SpecialType.System_Char => sizeof(char),
				SpecialType.System_Decimal => sizeof(decimal),
				SpecialType.System_Double => sizeof(double),
				SpecialType.System_Int16 => sizeof(short),
				SpecialType.System_Int32 => sizeof(int),
				SpecialType.System_Int64 => sizeof(long),
				SpecialType.System_SByte => sizeof(sbyte),
				SpecialType.System_Single => sizeof(float),
				SpecialType.System_UInt16 => sizeof(ushort),
				SpecialType.System_UInt32 => sizeof(uint),
				SpecialType.System_UInt64 => sizeof(ulong),
				SpecialType.System_IntPtr => IntPtr.Size,
				SpecialType.System_UIntPtr => UIntPtr.Size,
				_ => 0,
			};
		}

		// Handle pointer types
		if (type.TypeKind == TypeKind.Pointer)
		{
			return IntPtr.Size;
		}

		// Handle enum types - get the size of the underlying type
		// if (type.TypeKind == TypeKind.Enum && type.EnumUnderlyingType != null)
		// {
		// 	return VisitSizeOf(operation.Update(type.EnumUnderlyingType), argument);
		// }

		// Try to use Marshal.SizeOf for structs if available
		if (type.TypeKind == TypeKind.Struct)
		{
			try
			{
				var runtimeType = loader.GetType(type);
				// Use System.Runtime.InteropServices.Marshal.SizeOf
				var method = typeof(System.Runtime.InteropServices.Marshal).GetMethod("SizeOf", [ typeof(Type) ]);
				return (int)method?.Invoke(null, [ runtimeType ]);
			}
			catch
			{
				// Fallback - struct size calculation is complex due to alignment rules
				return 0;
			}
		}

		return 0;
	}
	
	public static bool HasMember<TSymbol>(this Compilation compilation, ITypeSymbol typeSymbol, string name, Func<TSymbol, bool> predicate)
		where TSymbol : ISymbol
	{
		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any(predicate);
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, ITypeSymbol typeSymbol, string name)
		where TSymbol : ISymbol
	{
		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any();
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, Type type, string name)
		where TSymbol : ISymbol
	{
		var fullName = type.FullName;
		var typeSymbol = compilation.GetTypeByMetadataName(fullName);
		
		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any();
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, Type type, string name, Func<TSymbol, bool> predicate)
		where TSymbol : ISymbol
	{
		var fullName = type.FullName;
		var typeSymbol = compilation.GetTypeByMetadataName(fullName);

		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any(predicate);
	}

	public static object? ExecuteMethod(this Compilation compilation, MetadataLoader loader, IMethodSymbol methodSymbol, object? instance, params object?[]? parameters)
	{
		var fullyQualifiedName = methodSymbol.ContainingType.ToDisplayString();
		var methodName = methodSymbol.Name;

		var type = loader.GetType(methodSymbol.ContainingType)
		           ?? throw new InvalidOperationException($"Type '{fullyQualifiedName}' not found");

		var methodInfos = type
			.GetMethods(methodSymbol.IsStatic
				? BindingFlags.Public | BindingFlags.Static
				: BindingFlags.Public | BindingFlags.Instance)
			.Where(f =>
			{
				if (f.Name != methodName)
				{
					return false;
				}

				var methodParameters = f.GetParameters();

				if (methodParameters.Length != methodSymbol.Parameters.Length)
				{
					return false;
				}

				for (var i = 0; i < methodParameters.Length; i++)
				{
					var paramType = methodParameters[i].ParameterType;
					var methodParamType = loader.GetType(methodSymbol.Parameters[i].Type);

					if (paramType.IsGenericType)
					{
						continue;
					}

					if (paramType.Namespace != methodParamType.Namespace || paramType.Name != methodParamType.Name)
					{
						return false;
					}
				}

				return true;
			});

		foreach (var info in methodInfos)
		{
			var methodInfo = info;

			if (methodInfo.IsGenericMethod)
			{
				var types = methodSymbol.TypeArguments
					.Select(loader.GetType)
					.ToArray();

				methodInfo = methodInfo.MakeGenericMethod(types);
			}

			var methodParams = methodInfo.GetParameters();

			for (var i = 0; i < methodParams.Length; i++)
			{
				if (!methodParams[i].ParameterType.IsAssignableFrom(loader.GetType(methodSymbol.Parameters[i].Type)))
				{
					methodInfo = null;
					break;
				}
			}

			if (methodInfo is null)
			{
				continue;
			}

			if (methodInfo.IsStatic)
			{
				return methodInfo.Invoke(null, parameters);
			}

			if (instance == null)
			{
				throw new InvalidOperationException($"Kan geen instantie creëren van type '{fullyQualifiedName}'.");
			}

			return methodInfo.Invoke(instance, parameters);
		}

		throw new InvalidOperationException($"Methode '{methodName}' niet gevonden in type '{fullyQualifiedName}'.");
	}

	public static object? GetPropertyValue(this Compilation compilation, MetadataLoader loader, IPropertySymbol propertySymbol, object? instance)
	{
		var fullyQualifiedTypeName = $"{SyntaxHelpers.GetFullNamespace(propertySymbol.ContainingNamespace)}.{propertySymbol.ContainingType.MetadataName}";
		var type = loader.GetType(propertySymbol.ContainingType);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{fullyQualifiedTypeName}' not found.");
		}

		var propertyInfo = type.GetProperty(propertySymbol.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

		if (propertyInfo == null)
		{
			throw new InvalidOperationException($"Eigenschap '{propertySymbol.Name}' niet gevonden in type '{fullyQualifiedTypeName}'.");
		}

		if (propertyInfo.GetMethod?.IsStatic == true)
		{
			return propertyInfo.GetValue(null);
		}

		if (instance == null)
		{
			throw new ArgumentNullException(nameof(instance), $"Een instantie van '{fullyQualifiedTypeName}' is vereist om de eigenschap '{propertySymbol.Name}' op te halen.");
		}

		if (!type.IsInstanceOfType(instance))
		{
			throw new ArgumentException($"De opgegeven instantie is geen type van '{fullyQualifiedTypeName}'.", nameof(instance));
		}

		return propertyInfo.GetValue(instance);
	}

	public static object? GetFieldValue(this Compilation compilation, MetadataLoader loader, IFieldSymbol fieldSymbol, object? instance)
	{
		var fullyQualifiedTypeName = fieldSymbol.ContainingType.ToDisplayString();
		var type = loader.GetType(fieldSymbol.ContainingType);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{fullyQualifiedTypeName}' not found.");
		}

		var fieldInfo = type.GetField(fieldSymbol.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

		if (fieldInfo == null)
		{
			throw new InvalidOperationException($"Veld '{fieldSymbol.Name}' niet gevonden in type '{fullyQualifiedTypeName}'.");
		}

		if (fieldInfo.IsStatic)
		{
			return fieldInfo.GetValue(null);
		}

		if (instance == null)
		{
			throw new ArgumentNullException(nameof(instance), $"Een instantie van '{fullyQualifiedTypeName}' is vereist om het veld '{fieldSymbol.Name}' op te halen.");
		}

		if (!type.IsInstanceOfType(instance))
		{
			throw new ArgumentException($"De opgegeven instantie is geen type van '{fullyQualifiedTypeName}'.", nameof(instance));
		}

		return fieldInfo.GetValue(instance);
	}

	public static bool TryGetSemanticModel(this Compilation compilation, SyntaxNode? node, out SemanticModel semanticModel)
	{
		var tree = node?.SyntaxTree;

		if (compilation.SyntaxTrees.Contains(tree))
		{
			semanticModel = compilation.GetSemanticModel(tree);
			return true;
		}

		semanticModel = null!;
		return false;
	}

	public static string GetMinimalString(this Compilation compilation, ISymbol symbol)
	{
		if (compilation.IsSpecialType(symbol, SpecialType.System_Void))
		{
			return "void";
		}

		if (compilation.IsSpecialType(symbol.OriginalDefinition, SpecialType.System_Collections_Generic_IEnumerable_T)
		    && symbol is INamedTypeSymbol namedTypeSymbol)
		{
			return $"IEnumerable<{String.Join(", ", namedTypeSymbol.TypeArguments.Select(s => GetMinimalString(compilation, s)))}>";
		}

		var node = SyntaxHelpers.GetSyntaxNode(symbol);

		if (!compilation.TryGetSemanticModel(node, out var model))
		{
			return symbol.ToDisplayString();
		}

		return symbol.ToMinimalDisplayString(model, node.Span.Start);
	}

	public static bool IsInterface(this Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model) && model.GetSymbolInfo(typeSymbol, token).Symbol is ITypeSymbol { TypeKind: TypeKind.Interface };
	}

	public static ITypeSymbol GetTypeByType(this Compilation compilation, Type type, params ITypeSymbol[] typeArguments)
	{
		return GetTypeByType(compilation, type.FullName, typeArguments);
	}

	public static ITypeSymbol GetTypeByType(this Compilation compilation, string typeName, params ITypeSymbol[] typeArguments)
	{
		var typeSymbol = compilation.GetTypeByMetadataName(typeName);

		if (typeArguments.Length > 0)
		{
			typeSymbol = typeSymbol.Construct(typeArguments);
		}

		return typeSymbol;
	}
}