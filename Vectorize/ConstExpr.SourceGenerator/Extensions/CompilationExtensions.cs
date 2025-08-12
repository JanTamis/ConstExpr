using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Extensions;

public static class CompilationExtensions
{
	public static INamedTypeSymbol? CreateIEnumerable(this Compilation compilation, ITypeSymbol? elementType)
	{
		if (elementType == null)
		{
			return null;
		}
		
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

	public static INamedTypeSymbol? CreateFunc(this Compilation compilation, params ITypeSymbol[] typeArguments)
	{
		return compilation.GetTypeByMetadataName($"System.Func`{typeArguments.Length}")?
			.Construct(typeArguments);
	}

	public static INamedTypeSymbol? CreateAction(this Compilation compilation, params ITypeSymbol[] typeArguments)
	{
		return compilation.GetTypeByMetadataName($"System.Action`{typeArguments.Length}")?
			.Construct(typeArguments);
	}

	public static INamedTypeSymbol? CreateKeyValuePair(this Compilation compilation, ITypeSymbol keyType, ITypeSymbol valueType)
	{
		return compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2")?
			.Construct(keyType, valueType);
	}

	public static INamedTypeSymbol? CreateEqualityComparer(this Compilation compilation, ITypeSymbol keyType)
	{
		return compilation.GetTypeByMetadataName("System.Collections.Generic.IEqualityComparer`1")?
			.Construct(keyType);
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

	public static bool IsLiteralType(this ITypeSymbol typeSymbol)
	{
		return typeSymbol.SpecialType is SpecialType.System_Boolean
			or SpecialType.System_Byte
			or SpecialType.System_SByte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64
			or SpecialType.System_Decimal
			or SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Char
			or SpecialType.System_String;
	}

	public static bool IsSpanLikeType(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		return typeSymbol is INamedTypeSymbol { Arity: 1 } namedTypeSymbol
		       && namedTypeSymbol.ContainingNamespace.ToString() == "System"
		       && namedTypeSymbol.Name is "Span" or "ReadOnlySpan"
		       && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], elementType);
	}

	public static bool IsSpanType(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		return typeSymbol is INamedTypeSymbol { Arity: 1 } namedTypeSymbol
		       && namedTypeSymbol.ContainingNamespace.ToString() == "System"
		       && namedTypeSymbol.Name is "Span"
		       && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], elementType);
	}

	public static bool IsReadonlySpanType(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		return typeSymbol is INamedTypeSymbol { Arity: 1 } namedTypeSymbol
		       && namedTypeSymbol.ContainingNamespace.ToString() == "System"
		       && namedTypeSymbol.Name is "Span" or "ReadOnlySpan"
		       && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], elementType);
	}

	public static bool IsEnumerableType(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		return compilation.IsSpanLikeType(typeSymbol, elementType)
		       || typeSymbol.EqualsType(compilation.CreateIEnumerable(elementType))
		       || typeSymbol.EqualsType(compilation.CreateArrayTypeSymbol(elementType));
	}

	public static bool TryGetUnsignedType(this Compilation compilation, ITypeSymbol typeSymbol, [NotNullWhen(true)] out ITypeSymbol? unsignedType)
	{
		switch (typeSymbol.SpecialType)
		{
			case SpecialType.System_SByte:
			case SpecialType.System_Byte:
				unsignedType = compilation.GetSpecialType(SpecialType.System_Byte);
				return true;
			case SpecialType.System_Int16:
			case SpecialType.System_UInt16:
				unsignedType = compilation.GetSpecialType(SpecialType.System_UInt16);
				return true;
			case SpecialType.System_Int32:
			case SpecialType.System_UInt32:
				unsignedType = compilation.GetSpecialType(SpecialType.System_UInt32);
				return true;
			case SpecialType.System_Int64:
			case SpecialType.System_UInt64:
				unsignedType = compilation.GetSpecialType(SpecialType.System_UInt64);
				return true;
			default:
				unsignedType = null;
				return false;
		}
	}

	public static int GetByteSize(this Compilation compilation, MetadataLoader loader, ITypeSymbol? type)
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
		// 	return compilation.GetByteSize(type.)
		// }

		// Try to use Marshal.SizeOf for structs if available
		if (type.TypeKind == TypeKind.Struct)
		{
			try
			{
				var runtimeType = loader.GetType(type);
				
				// Use System.Runtime.InteropServices.Marshal.SizeOf
				var method = typeof(System.Runtime.InteropServices.Marshal).GetMethod("SizeOf", [ typeof(Type) ]);
				return (int) method?.Invoke(null, [ runtimeType ]);
			}
			catch
			{
				// Fallback - struct size calculation is complex due to alignment rules
				return 0;
			}
		}

		return 0;
	}

	public static bool HasMember<TSymbol>(this ITypeSymbol typeSymbol, string name)
		where TSymbol : ISymbol
	{
		if (typeSymbol is ITypeParameterSymbol parameter)
		{
			return parameter.ConstraintTypes.Any(a => a.HasMember<TSymbol>(name));
		}

		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any();
	}

	public static bool HasMember<TSymbol>(this ITypeSymbol typeSymbol, string name, Func<TSymbol, bool> predicate)
		where TSymbol : ISymbol
	{
		if (typeSymbol is ITypeParameterSymbol parameter)
		{
			return parameter.ConstraintTypes.Any(a => a.HasMember(name, predicate));
		}

		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any(predicate);
	}

	public static bool HasMethod(this ITypeSymbol? typeSymbol, string name)
	{
		if (typeSymbol is ITypeParameterSymbol parameter)
		{
			return parameter.ConstraintTypes.Any(a => a.HasMethod(name));
		}

		return typeSymbol?.GetMembers(name).OfType<IMethodSymbol>().Any() == true;
	}

	public static bool HasMethod(this ITypeSymbol typeSymbol, string name, Func<IMethodSymbol, bool> predicate)
	{
		if (typeSymbol is ITypeParameterSymbol parameter)
		{
			return parameter.ConstraintTypes.Any(a => a.HasMethod(name, predicate));
		}

		return typeSymbol.GetMembers(name).OfType<IMethodSymbol>().Any(predicate);
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, ITypeSymbol? typeSymbol, string name)
		where TSymbol : ISymbol
	{
		return typeSymbol switch
		{
			null => false,
			ITypeParameterSymbol parameter => parameter.ConstraintTypes.Any(a => compilation.HasMember<TSymbol>(a, name)),
			_ => typeSymbol.GetMembers(name).OfType<TSymbol>().Any()
		};
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, Type type, string name)
		where TSymbol : ISymbol
	{
		var fullName = type.FullName;
		var typeSymbol = compilation.GetTypeByMetadataName(fullName);

		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any();
	}

	public static bool HasMethod(this Compilation compilation, Type type, string name)
	{
		var fullName = type.FullName;
		var typeSymbol = compilation.GetTypeByMetadataName(fullName);

		return typeSymbol.GetMembers(name).OfType<IMethodSymbol>().Any();
	}

	public static bool HasMember<TSymbol>(this Compilation compilation, Type type, string name, Func<TSymbol, bool> predicate)
		where TSymbol : ISymbol
	{
		var fullName = type.FullName;
		var typeSymbol = compilation.GetTypeByMetadataName(fullName);

		return typeSymbol.GetMembers(name).OfType<TSymbol>().Any(predicate);
	}

	public static object? ExecuteMethod(this Compilation compilation, MetadataLoader loader, IMethodSymbol methodSymbol, object? instance, IDictionary<string, object?>? arguments, params object?[]? parameters)
	{
		var fullyQualifiedName = methodSymbol.ContainingType.ToDisplayString();
		var methodName = methodSymbol.Name;

		var type = loader.GetType(methodSymbol.ContainingType)
		           ?? throw new InvalidOperationException($"Type '{fullyQualifiedName}' not found");

		var methodInfos = type
			.GetMethods(methodSymbol.IsStatic
				? BindingFlags.Public | BindingFlags.Static
				: BindingFlags.Public | BindingFlags.Instance).OfType<MethodBase?>()
			.Concat(type.GetConstructors(methodSymbol.IsStatic
				? BindingFlags.Public | BindingFlags.Static
				: BindingFlags.Public | BindingFlags.Instance).OfType<MethodBase?>())
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
					.Select(symbol =>
					{
						if (symbol is ITypeParameterSymbol parameter && arguments?.TryGetValue(parameter.Name, out var type) == true)
						{
							return type as Type;
						}
						return loader.GetType(symbol);
					})
					.ToArray();

				if (methodInfo is MethodInfo method)
				{
					methodInfo = method.MakeGenericMethod(types);
				}
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

			if (methodInfo.IsConstructor)
			{
				return Activator.CreateInstance(type, parameters);
			}

			if (instance == null)
			{
				throw new InvalidOperationException($"Kan geen instantie creëren van type '{fullyQualifiedName}'.");
			}

			return methodInfo.Invoke(instance, parameters);
		}

		throw new InvalidOperationException($"Methode '{methodName}' niet gevonden in type '{fullyQualifiedName}'.");
	}

	public static object? GetPropertyValue(this Compilation compilation, MetadataLoader loader, ISymbol propertySymbol, object? instance)
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

	public static object? GetFieldValue(this Compilation compilation, MetadataLoader loader, ISymbol fieldSymbol, object? instance)
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

	// public static bool TryGetSemanticModel(this Compilation compilation, SyntaxNode? node, out SemanticModel semanticModel)
	// {
	// 	var tree = node?.SyntaxTree;
	//
	// 	if (compilation.SyntaxTrees.Contains(tree))
	// 	{
	// 		semanticModel = compilation.GetSemanticModel(tree);
	// 		return true;
	// 	}
	//
	// 	semanticModel = null!;
	// 	return false;
	// }

	public static bool IsInterface(this Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model) && ModelExtensions.GetSymbolInfo(model, typeSymbol, token).Symbol is ITypeSymbol { TypeKind: TypeKind.Interface };
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

	public static string GetCreateVector<T>(this Compilation compilation, VectorTypes vectorType, ITypeSymbol elementType, MetadataLoader loader, bool isRepeating, ReadOnlySpan<T> items)
	{
		var byteSize = vectorType switch
		{
			VectorTypes.Vector64 => 8,
			VectorTypes.Vector128 => 16,
			VectorTypes.Vector256 => 32,
			VectorTypes.Vector512 => 64,
		};

		var staticType = compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}");
		var fullType = compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}`1").Construct(elementType);
		// var vectorType = loader.GetType(fullType);
		var vectorElementType = loader.GetType(elementType);
		var elementByteSize = compilation.GetByteSize(loader, elementType);

		var elementCount = byteSize / elementByteSize; // vectorType.GetProperty("Count")?.GetValue(null);

		items = items.Slice(0, Math.Min(items.Length, elementCount));

		if (items.IsZero())
		{
			return $"{vectorType}<{elementType}>.Zero";
		}

		if (items.IsOne())
		{
			return $"{vectorType}<{elementType}>.One";
		}

		if (!items.IsEmpty && items.IsSame(items[0]))
		{
			return elementType.NeedsCast()
				? $"{vectorType}.Create(({elementType}){SyntaxHelpers.CreateLiteral(items[0])})"
				: $"{vectorType}.Create({SyntaxHelpers.CreateLiteral(items[0])})";
		}

		if (items.Length == elementCount)
		{
			// Check if all items match their indices (0,1,2,...)
			if (items.IsSequence(vectorElementType) && fullType.HasMember<IPropertySymbol>("Indices"))
			{
				return $"{compilation.GetMinimalString(fullType)}.Indices";
			}

			// Check if items form an arithmetic sequence
			if (items.Length >= 2 && staticType.HasMethod("CreateSequence", m => m.Parameters.Length == 2))
			{
				var isSequence = true;
				var step = default(object);

				for (var i = 1; i < items.Length; i++)
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
					return elementType.NeedsCast()
						? $"{vectorType}.CreateSequence(({elementType}){SyntaxHelpers.CreateLiteral(items[0])}, ({elementType}){SyntaxHelpers.CreateLiteral(step)})"
						: $"{vectorType}.CreateSequence({SyntaxHelpers.CreateLiteral(items[0])}, {SyntaxHelpers.CreateLiteral(step)})";
				}
			}
			
			return elementType.NeedsCast()
				? $"{vectorType}.Create({items.Join<T, object?>(", ", s => s is string ? s : $"({compilation.GetMinimalString(elementType)}){SyntaxHelpers.CreateLiteral(s)}")})"
				: $"{vectorType}.Create({items.Join<T, object?>(", ", s => s is string ? s : SyntaxHelpers.CreateLiteral(s))})";
		}

		if (isRepeating)
		{
			if (elementType.NeedsCast())
			{
				return $"{vectorType}.Create({items.Join<T, object?>(", ", elementCount, s => s is string ? s : $"({compilation.GetMinimalString(elementType)}){SyntaxHelpers.CreateLiteral(s)}")})";
			}
			
			return $"{vectorType}.Create({items.Join<T, object?>(", ", elementCount, s => s is string ? s : SyntaxHelpers.CreateLiteral(s))})";
		}

		if (elementType.NeedsCast())
		{
			return $"{vectorType}.Create({items.JoinWithPadding<T, object?>(", ", elementCount, (T) 0.ToSpecialType(elementType.SpecialType), s => s is string ? s : $"({compilation.GetMinimalString(elementType)}){SyntaxHelpers.CreateLiteral(s)}")})";
		}

		return $"{vectorType}.Create({items.JoinWithPadding<T, object?>(", ", elementCount, (T) 0.ToSpecialType(elementType.SpecialType), s => s is string ? s : SyntaxHelpers.CreateLiteral(s))})";
	}

	public static VectorTypes GetVector<T>(this Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, ReadOnlySpan<T> items, bool isRepeating, out string vector, out int vectorSize)
	{
		var elementSize = compilation.GetByteSize(loader, elementType);
		var size = elementSize * items.Length;

		switch (size)
		{
			case 0:
				vector = String.Empty;
				vectorSize = 0;
				return VectorTypes.None;
			case <= 8:
				vector = GetCreateVector(compilation, VectorTypes.Vector64, elementType, loader, isRepeating, items);
				vectorSize = 8 / elementSize;

				return VectorTypes.Vector64;
			case <= 16:
				vector = GetCreateVector(compilation, VectorTypes.Vector128, elementType, loader, isRepeating, items);
				vectorSize = 16 / elementSize;

				return VectorTypes.Vector128;
			case <= 32:
				vector = GetCreateVector(compilation, VectorTypes.Vector256, elementType, loader, isRepeating, items);
				vectorSize = 32 / elementSize;

				return VectorTypes.Vector256;
			case <= 64:
				vector = GetCreateVector(compilation, VectorTypes.Vector512, elementType, loader, isRepeating, items);
				vectorSize = 64 / elementSize;

				return VectorTypes.Vector512;
			default:
				vector = null;
				vectorSize = 0;

				return VectorTypes.None;
		}
	}

	public static VectorTypes GetVector<T>(this Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, ReadOnlySpan<T> items, VectorTypes limit, out string vector, out int vectorSize)
	{
		var elementSize = compilation.GetByteSize(loader, elementType);
		var size = elementSize * items.Length;

		switch (size)
		{
			case >= 64 when limit is VectorTypes.Vector512:
				vector = GetCreateVector(compilation, VectorTypes.Vector512, elementType, loader, false, items);
				vectorSize = 64 / elementSize;

				return VectorTypes.Vector512;
			case >= 32 when limit is VectorTypes.Vector512 or VectorTypes.Vector256:
				vector = GetCreateVector(compilation, VectorTypes.Vector256, elementType, loader, false, items);
				vectorSize = 32 / elementSize;

				return VectorTypes.Vector256;
			case >= 16 when limit is VectorTypes.Vector512 or VectorTypes.Vector256 or VectorTypes.Vector128:
				vector = GetCreateVector(compilation, VectorTypes.Vector128, elementType, loader, false, items);
				vectorSize = 16 / elementSize;

				return VectorTypes.Vector128;
			case >= 8 when limit is VectorTypes.Vector512 or VectorTypes.Vector256 or VectorTypes.Vector128 or VectorTypes.Vector64:
				vector = GetCreateVector(compilation, VectorTypes.Vector64, elementType, loader, false, items);
				vectorSize = 8 / elementSize;

				return VectorTypes.Vector64;
			default:
				vector = null;
				vectorSize = 0;

				return VectorTypes.None;
		}
	}

	public static string GetVector<T>(this Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, ReadOnlySpan<T> items, VectorTypes limit)
	{
		var elementSize = compilation.GetByteSize(loader, elementType);
		var size = elementSize * items.Length;

		return size switch
		{
			>= 64 when limit is VectorTypes.Vector512 => GetCreateVector(compilation, VectorTypes.Vector512, elementType, loader, false, items),
			>= 32 when limit is VectorTypes.Vector512 or VectorTypes.Vector256 => GetCreateVector(compilation, VectorTypes.Vector256, elementType, loader, false, items),
			>= 16 when limit is VectorTypes.Vector512 or VectorTypes.Vector256 or VectorTypes.Vector128 => GetCreateVector(compilation, VectorTypes.Vector128, elementType, loader, false, items),
			>= 8 when limit is VectorTypes.Vector512 or VectorTypes.Vector256 or VectorTypes.Vector128 or VectorTypes.Vector64 => GetCreateVector(compilation, VectorTypes.Vector64, elementType, loader, false, items),
			_ => String.Empty
		};
	}

	public static INamedTypeSymbol? GetVectorType(this Compilation compilation, VectorTypes vectorType)
	{
		return compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}");
	}

	public static INamedTypeSymbol? GetVectorType(this Compilation compilation, VectorTypes vectorType, ITypeSymbol elementType)
	{
		return compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}`1")?.Construct(elementType);
	}

	public static bool IsVectorSupported(this ITypeSymbol elementType)
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

	public static bool EqualsTypes(this ReadOnlySpan<IParameterSymbol> parameters, ReadOnlySpan<ITypeSymbol?> typeSymbols)
	{
		if (parameters.Length != typeSymbols.Length)
		{
			return false;
		}

		for (var i = 0; i < parameters.Length; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, typeSymbols[i]))
			{
				return false;
			}
		}

		return true;
	}

	public static bool HasContainsMethod(this Compilation compilation, ITypeSymbol typeSymbol, ITypeSymbol elementType)
	{
		var result = typeSymbol.HasMethod("Contains", m => m is { ReturnType.SpecialType: SpecialType.System_Boolean } && m.Parameters.AsSpan().EqualsTypes(elementType));

		return result || compilation.IsSpanLikeType(typeSymbol, elementType)
			&& (compilation.GetTypeByMetadataName("System.MemoryExtensions")?.HasMethod("Contains", m => m.ReturnType.SpecialType == SpecialType.System_Boolean && m.Parameters.AsSpan().EqualsTypes(elementType)) ?? false);
	}

	public static bool IsInterger(this ITypeSymbol typeSymbol)
	{
		return typeSymbol.SpecialType is SpecialType.System_SByte
			or SpecialType.System_Byte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64;
	}

	public static bool HasComparison(this ITypeSymbol typeSymbol)
	{
		return typeSymbol.SpecialType is SpecialType.System_SByte
			or SpecialType.System_Byte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64
			or SpecialType.System_Boolean
			or SpecialType.System_Char
			or SpecialType.System_Decimal
			or SpecialType.System_Double
			or SpecialType.System_Single;
	}


	public static bool EqualsType(this ITypeSymbol type, ITypeSymbol? otherType)
	{
		if (otherType == null)
		{
			return false;
		}

		if (SymbolEqualityComparer.Default.Equals(type, otherType))
		{
			return true;
		}

		if (type is ITypeParameterSymbol typeParameter)
		{
			return typeParameter.ConstraintTypes.All(a => a.EqualsType(otherType));
		}

		return type.AllInterfaces.Any(a => SymbolEqualityComparer.Default.Equals(a, otherType));
	}

	public static ExpressionSyntax GetDefaultValue(this ITypeSymbol type)
	{
		if (type.IsReferenceType)
		{
			return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
		}

		return type.SpecialType switch
		{
			SpecialType.System_Boolean => SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),
			SpecialType.System_Byte => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
			SpecialType.System_SByte => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
			SpecialType.System_Int16 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
			SpecialType.System_UInt16 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
			SpecialType.System_Int32 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
			SpecialType.System_UInt32 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0U)),
			SpecialType.System_Int64 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0L)),
			SpecialType.System_UInt64 => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0UL)),
			SpecialType.System_Single => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0f)),
			SpecialType.System_Double => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0d)),
			SpecialType.System_Decimal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0m)),
			_ => SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(type.ToDisplayString())),
		};
	}

	public static VectorTypes GetBestVectorType(this Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, int elementCount, bool isRepeating)
	{
		var elementSize = compilation.GetByteSize(loader, elementType);
		var size = elementSize * elementCount;

		var vectorTypes = new[]
		{
			(VectorTypes.Vector64, 8),
			(VectorTypes.Vector128, 16),
			(VectorTypes.Vector256, 32),
			(VectorTypes.Vector512, 64)
		};

		if (isRepeating)
		{
			return vectorTypes
				.Where(w => w.Item2 >= size)
				.Select(s => s.Item1)
				.DefaultIfEmpty(VectorTypes.Vector512)
				.First();
		}

		var best = vectorTypes
			.Select(vt => (Type: vt.Item1, Mod: Math.Abs(size % vt.Item2)))
			.OrderBy(v => v.Mod)
			.ThenByDescending(v => v.Type)
			.Select(s => s.Type)
			.First();

		return best;
	}

	public static bool TryGetIEnumerableType(this Compilation compilation, ITypeSymbol? typeSymbol, bool recursive, out ITypeSymbol? elementType)
	{
		if (typeSymbol == null)
		{
			elementType = null;
			return false;
		}
		
		if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
		{
			if (SymbolEqualityComparer.Default.Equals(namedTypeSymbol, compilation.CreateIEnumerable(namedTypeSymbol.TypeArguments[0])))
			{
				elementType = namedTypeSymbol.TypeArguments[0];
				return true;
			}
		}

		foreach (var @interface in typeSymbol.AllInterfaces)
		{
			if (@interface is { Arity: 1 }
			    && SymbolEqualityComparer.Default.Equals(@interface, compilation.CreateIEnumerable(@interface.TypeArguments[0]))
			    && recursive)
			{
				elementType = @interface.TypeArguments[0];
				return true;
			}
		}

		elementType = null;
		return false;
	}

	public static bool TryGetFuncType(this Compilation compilation, ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? elementType, [NotNullWhen(true)] out ITypeSymbol? returnType)
	{
		if (type is INamedTypeSymbol { Arity: 2 } namedTypeSymbol
		    && SymbolEqualityComparer.Default.Equals(type, compilation.CreateFunc(namedTypeSymbol.TypeArguments[0], namedTypeSymbol.TypeArguments[1])))
		{
			elementType = namedTypeSymbol.TypeArguments[0];
			returnType = namedTypeSymbol.TypeArguments[1];
			return true;
		}

		elementType = null;
		returnType = null;
		return false;
	}

	public static bool NeedsCast(this ITypeSymbol type)
	{
		return type.SpecialType is SpecialType.System_Byte
			or SpecialType.System_SByte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Decimal;
	}

	public static INamedTypeSymbol? GetTypeByName(this Compilation compilation, string? fullyQualifiedMetadataName, ReadOnlySpan<ITypeSymbol> typeArguments)
	{
		if (String.IsNullOrEmpty(fullyQualifiedMetadataName))
		{
			return null;
		}
		
		if (typeArguments.IsEmpty)
		{
			return compilation.GetTypeByMetadataName(fullyQualifiedMetadataName!);
		}

		if (fullyQualifiedMetadataName!.EndsWith($"`{typeArguments.Length}"))
		{
			return compilation.GetTypeByMetadataName(fullyQualifiedMetadataName)?.Construct(typeArguments.ToArray());
		}

		return compilation.GetTypeByMetadataName($"{fullyQualifiedMetadataName}`{typeArguments.Length}")?.Construct(typeArguments.ToArray());
	}
}