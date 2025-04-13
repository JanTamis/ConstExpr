using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Enums;

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
	
	public static INamedTypeSymbol CreateString(this Compilation compilation)
	{
		return compilation
			.CreateSpecialType(SpecialType.System_String);
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

	public static ITypeSymbol GetUnsignedType(this Compilation compilation, ITypeSymbol typeSymbol)
	{
		return typeSymbol.SpecialType switch
		{
			SpecialType.System_SByte => compilation.GetSpecialType(SpecialType.System_Byte),
			SpecialType.System_Int16 => compilation.GetSpecialType(SpecialType.System_UInt16),
			SpecialType.System_Int32 => compilation.GetSpecialType(SpecialType.System_UInt32),
			SpecialType.System_Int64 => compilation.GetSpecialType(SpecialType.System_UInt64),
			_ => typeSymbol,
		};
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
				var method = typeof(System.Runtime.InteropServices.Marshal).GetMethod("SizeOf", [typeof(Type)]);
				return (int)method?.Invoke(null, [runtimeType]);
			}
			catch
			{
				// Fallback - struct size calculation is complex due to alignment rules
				return 0;
			}
		}

		return 0;
	}

	public static bool HasMember<TSymbol>(this ITypeSymbol typeSymbol, string name, Func<TSymbol, bool> predicate)
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
		// Check for built-in type keywords
		if (symbol is ITypeSymbol typeSymbol)
		{
			switch (typeSymbol.SpecialType)
			{
				case SpecialType.System_Void: return "void";
				case SpecialType.System_Boolean: return "bool";
				case SpecialType.System_Byte: return "byte";
				case SpecialType.System_SByte: return "sbyte";
				case SpecialType.System_Int16: return "short";
				case SpecialType.System_UInt16: return "ushort";
				case SpecialType.System_Int32: return "int";
				case SpecialType.System_UInt32: return "uint";
				case SpecialType.System_Int64: return "long";
				case SpecialType.System_UInt64: return "ulong";
				case SpecialType.System_Single: return "float";
				case SpecialType.System_Double: return "double";
				case SpecialType.System_Decimal: return "decimal";
				case SpecialType.System_Char: return "char";
				case SpecialType.System_String: return "string";
				case SpecialType.System_Object: return "object";
			}
		}

		if (compilation.IsSpecialType(symbol.OriginalDefinition, SpecialType.System_Collections_Generic_IEnumerable_T)
				&& symbol is INamedTypeSymbol namedTypeSymbol)
		{
			return $"IEnumerable<{String.Join(", ", namedTypeSymbol.TypeArguments.Select(s => GetMinimalString(compilation, s)))}>";
		}

		if (symbol is INamedTypeSymbol { Arity: > 0 } namedSymbol)
		{
			return $"{namedSymbol.Name}<{String.Join(", ", namedSymbol.TypeArguments.Select(s => GetMinimalString(compilation, s)))}>";
		}

		var node = SyntaxHelpers.GetSyntaxNode(symbol);

		if (!compilation.TryGetSemanticModel(node, out var model))
		{
			return symbol.Name;
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

	public static string GetCreateVector(this Compilation compilation, string vectorName, int byteSize, ITypeSymbol elementType, MetadataLoader loader, bool isRepeating, params IList<object?> items)
	{
		var staticType = compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorName}");
		var fullType = compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorName}`1").Construct(elementType);
		// var vectorType = loader.GetType(fullType);
		var vectorElementType = loader.GetType(elementType);
		var elementByteSize = compilation.GetByteSize(loader, elementType);

		var elementCount = byteSize / elementByteSize; // vectorType.GetProperty("Count")?.GetValue(null);

		if (items.All(item => item is 0 or 0L or 0U or 0UL or 0f or 0d or (byte)0 or (short)0 or (sbyte)0 or (ushort)0))
		{
			return $"{compilation.GetMinimalString(fullType)}.Zero";
		}

		if (items.All(item => item is 1 or 1L or 1U or 1UL or 1f or 1d or (byte)1 or (short)1 or (sbyte)1 or (ushort)1))
		{
			return $"{compilation.GetMinimalString(fullType)}.One";
		}

		if (items.Count == elementCount)
		{
			// Check if all items match their indices (0,1,2,...)
			if (fullType.GetMembers("Indices").OfType<IPropertySymbol>().Any()
					&& Enumerable.Range(0, items.Count).All(i => Convert.ChangeType(i, vectorElementType).Equals(items[i])))
			{
				return $"{compilation.GetMinimalString(fullType)}.Indices";
			}

			// Check if items form an arithmetic sequence
			if (items.Count >= 2 && staticType.GetMembers("CreateSequence").OfType<IMethodSymbol>().Any())
			{
				var isSequence = true;
				var step = default(object);

				for (var i = 1; i < items.Count; i++)
				{
					var currentStep = ConstExprOperationVisitor.Subtract(items[i], items[i - 1]);

					if (i == 1)
					{
						step = currentStep;
					}
					else if (!Equals(currentStep, step))
					{
						isSequence = false;
						break;
					}
				}

				if (isSequence && step != null)
				{
					return $"{vectorName}.CreateSequence({SyntaxHelpers.CreateLiteral(items[0])}, {SyntaxHelpers.CreateLiteral(step)})";
				}
			}

			if (items.All(i => i == items[0]))
			{
				return $"{vectorName}.Create<{compilation.GetMinimalString(elementType)}>({SyntaxHelpers.CreateLiteral(items[0])})";
			}

			return $"{vectorName}.Create({String.Join(", ", items.Select(s => s is string ? s : SyntaxHelpers.CreateLiteral(s)))})";
		}

		if (items.Count == 1)
		{
			return $"{vectorName}.Create({SyntaxHelpers.CreateLiteral(items[0])})";
		}
		
		if (isRepeating)
		{
			return $"{vectorName}.Create({String.Join(", ", items.Repeat(elementCount).Select(s => s is string ? s : SyntaxHelpers.CreateLiteral(s)))})";
		}

		return $"{vectorName}.Create({String.Join(", ", items.Concat(Enumerable.Repeat(0, items.Count - elementCount).Cast<object?>()).Select(s => s is string ? s : SyntaxHelpers.CreateLiteral(s)))})";
	}

	public static VectorTypes GetVector(this Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, IList<object?> items, bool isRepeating, out string vector, out int vectorSize)
	{
		var elementSize = compilation.GetByteSize(loader, elementType);
		var size = elementSize * items.Count;

		switch (size)
		{
			case 0:
				vector = String.Empty;
				vectorSize = 0;
				return VectorTypes.None;
			case <= 8:
				vector = GetCreateVector(compilation, nameof(VectorTypes.Vector64), 8, elementType, loader, isRepeating, items);
				vectorSize = 8 / elementSize;
				
				return VectorTypes.Vector64;
			case <= 16:
				vector = GetCreateVector(compilation, nameof(VectorTypes.Vector128), 16, elementType, loader, isRepeating, items);
				vectorSize = 16 / elementSize;
				
				return VectorTypes.Vector128;
			case <= 32:
				vector = GetCreateVector(compilation, nameof(VectorTypes.Vector256), 32, elementType, loader, isRepeating, items);
				vectorSize = 32 / elementSize;

				return VectorTypes.Vector256;
			case <= 64:
				vector = GetCreateVector(compilation, nameof(VectorTypes.Vector512), 64, elementType, loader, isRepeating, items);
				vectorSize = 64 / elementSize;

				return VectorTypes.Vector512;
			default:
				vector = String.Empty;
				vectorSize = 0;
				
				return VectorTypes.None;
		}
	}

	public static INamedTypeSymbol GetVectorType(this Compilation compilation, VectorTypes vectorType)
	{
		return compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}");
	}

	public static INamedTypeSymbol GetVectorType(this Compilation compilation, VectorTypes vectorType, ITypeSymbol elementType)
	{
		return compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}`1").Construct(elementType);
	}
	
	public static bool IsVectorSupported(this Compilation compilation, ITypeSymbol elementType)
	{
		return elementType.SpecialType is SpecialType.System_Byte
			or SpecialType.System_Double
			or SpecialType.System_Int16
			or SpecialType.System_Int32
			or SpecialType.System_Int64
			or SpecialType.System_IntPtr
			or SpecialType.System_SByte
			or SpecialType.System_Single
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64
			or SpecialType.System_UIntPtr;
	}
}