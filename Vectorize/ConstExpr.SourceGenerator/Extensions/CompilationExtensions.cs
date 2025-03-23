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