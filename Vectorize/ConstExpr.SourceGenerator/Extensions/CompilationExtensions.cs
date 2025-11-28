using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

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

	public static bool TryExecuteMethod(this MetadataLoader loader, [NotNullWhen(true)] IMethodSymbol? methodSymbol, object? instance, IDictionary<string, object?>? arguments, IEnumerable<object?> parameters, out object? value)
	{
		if (methodSymbol is null)
		{
			value = null;
			return false;
		}

		var isExtension = methodSymbol.IsExtensionMethod;

		var originalParameterTypes = methodSymbol.Parameters
			.Select(s => s.Type)
			.Prepend(methodSymbol.IsExtensionMethod ? methodSymbol.ReceiverType : null)
			.Where(w => w != null)
			.ToImmutableArray();

		if (isExtension)
		{
			parameters = parameters.Prepend(instance);
		}

		var paramArray = parameters as object?[] ?? parameters.ToArray();
		var expectedParameterLength = originalParameterTypes.Length;

		if (paramArray.Length != expectedParameterLength)
		{
			value = null; // quick mismatch
			return false;
		}

		var methodName = methodSymbol.Name;
		var type = loader.GetType(methodSymbol.ContainingType) ?? instance?.GetType();

		if (type == null)
		{
			value = null;
			return false;
		}

		var methods = methodSymbol.MethodKind switch
		{
			MethodKind.Constructor => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
			MethodKind.StaticConstructor => type.GetConstructors(BindingFlags.Public | BindingFlags.Static),
			MethodKind.PropertyGet or MethodKind.PropertySet => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
				.Select(p => methodSymbol.MethodKind == MethodKind.PropertyGet ? p.GetMethod : p.SetMethod),
			_ => type.GetMethods(methodSymbol.IsStatic || isExtension
				? BindingFlags.Public | BindingFlags.Static
				: BindingFlags.Public | BindingFlags.Instance).Cast<MethodBase?>(),
		};

		// Filter candidates
		var candidates = methods
			.Where(m => m != null && m.Name == methodName && m.GetParameters().Length == expectedParameterLength);

		foreach (var candidate in candidates)
		{
			var methodInfo = candidate! as MethodInfo;
			var invokeBase = candidate!;

			// Bind generics early so parameter types are closed (e.g. IEnumerable<double>)
			if (methodInfo != null && methodInfo.IsGenericMethodDefinition)
			{
				var typeArgs = methodSymbol.TypeArguments.Select(symbol =>
				{
					if (arguments.TryGetValue($"#{symbol.Name}", out var type) && type is Type resultType)
					{
						return resultType;
					}

					return loader.GetType(symbol);
				}).ToArray();

				if (typeArgs.Any(a => a == null))
				{
					continue;
				}

				methodInfo = methodInfo.MakeGenericMethod(typeArgs!);
				invokeBase = methodInfo;
			}

			// Parameter type validation (stricter than namespace+name if possible)
			var reflParams = invokeBase.GetParameters();
			var compatible = true;

			for (var i = 0; i < reflParams.Length; i++)
			{
				var reflParam = reflParams[i];
				var symbolParam = originalParameterTypes[i];
				var symbolParamType = loader.GetType(symbolParam);

				if (symbolParamType == null)
				{
					compatible = false;
					break;
				}

				if (isExtension && i == 0)
				{
					if (instance == null)
					{
						compatible = false;
						break;
					}

					var instType = instance.GetType();
					var pType = reflParam.ParameterType;

					// Accept if assignable, or if pType is an open generic interface implemented by the instance
					var ok = pType.IsAssignableFrom(instType)
					         || pType is { IsGenericType: true, ContainsGenericParameters: true }
					         && instType.GetInterfaces().Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == pType.GetGenericTypeDefinition());

					if (!ok)
					{
						compatible = false;
						break;
					}

					continue;
				}

				if (!reflParam.ParameterType.IsGenericParameter && !reflParam.ParameterType.IsAssignableFrom(symbolParamType))
				{
					compatible = false;
					break;
				}

				// Also validate provided runtime value if present
				var providedVal = paramArray[i];

				if (providedVal != null && !reflParam.ParameterType.IsInstanceOfType(providedVal))
				{
					// Try implicit conversion for numeric primitives
					if (!TryChangeNumericType(providedVal, reflParam.ParameterType, out var converted))
					{
						compatible = false;
						break;
					}

					paramArray[i] = converted; // replace with converted value
				}
			}

			if (!compatible)
			{
				continue;
			}

			// Final safety checks to avoid needing try/catch
			if (invokeBase.IsConstructor)
			{
				if (invokeBase is ConstructorInfo ctor)
				{
					value = ctor.Invoke(paramArray);
					return true;
				}
			}
			else if (invokeBase.IsStatic)
			{
				value = invokeBase.Invoke(null, paramArray);
				return true;
			}
			else
			{
				if (instance == null || !type.IsInstanceOfType(instance))
				{
					continue;
				}

				value = invokeBase.Invoke(instance, paramArray);
				return true;
			}
		}

		value = null;
		return false;
	}

	private static bool TryChangeNumericType(object value, Type targetType, out object? converted)
	{
		converted = null;

		if (value == null)
		{
			return false;
		}

		var srcType = value.GetType();

		if (!IsNumeric(srcType) || !IsNumeric(targetType))
		{
			return false;
		}

		try
		{
			converted = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsNumeric(Type t)
	{
		if (t.IsEnum)
		{
			return false;
		}

		var nt = Nullable.GetUnderlyingType(t) ?? t;
		return nt == typeof(byte) || nt == typeof(sbyte) || nt == typeof(short) || nt == typeof(ushort) || nt == typeof(int) || nt == typeof(uint) || nt == typeof(long) || nt == typeof(ulong) || nt == typeof(float) || nt == typeof(double) || nt == typeof(decimal);
	}

	public static object? GetPropertyValue(this MetadataLoader loader, ISymbol propertySymbol, object? instance)
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

	public static object? GetFieldValue(this MetadataLoader loader, ISymbol fieldSymbol, object? instance)
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

	public static bool TryGetFieldValue(this MetadataLoader loader, ISymbol fieldSymbol, object? instance, [NotNullWhen(true)] out object? value)
	{
		var type = loader.GetType(fieldSymbol.ContainingType);

		if (type == null)
		{
			value = null;
			return false;
		}

		var fieldInfo = type.GetField(fieldSymbol.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

		if (fieldInfo == null)
		{
			value = null;
			return false;
		}

		if (fieldInfo.IsStatic)
		{
			value = fieldInfo.GetValue(null);
			return true;
		}

		if (instance == null || !type.IsInstanceOfType(instance))
		{
			value = null;
			return false;
		}

		value = fieldInfo.GetValue(instance);
		return true;
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

	public static string GetCreateVector<T>(this Compilation compilation, VectorTypes vectorType, ITypeSymbol elementType, MetadataLoader loader, bool isRepeating, params ReadOnlySpan<T> items)
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
					var currentStep = items[i].Subtract(items[i - 1]);

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

	public static bool EqualsTypes(this ReadOnlySpan<IParameterSymbol> parameters, params ReadOnlySpan<ITypeSymbol?> typeSymbols)
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

	public static bool IsInteger(this ITypeSymbol typeSymbol)
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


	public static bool EqualsType(this ITypeSymbol? type, ITypeSymbol? otherType)
	{
		if (otherType == null || type == null)
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

	public static INamedTypeSymbol? GetTypeByName(this Compilation compilation, string? fullyQualifiedMetadataName, params ReadOnlySpan<ITypeSymbol> typeArguments)
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

	public static TypeSyntax GetTypeSyntax(this ITypeSymbol typeSymbol, bool fullyQualified = true)
	{
		var format = fullyQualified ? SymbolDisplayFormat.FullyQualifiedFormat : SymbolDisplayFormat.MinimallyQualifiedFormat;
		var typeText = typeSymbol.ToDisplayString(format);

		return SyntaxFactory.ParseTypeName(typeText);
	}

	public static bool TryGetParentOfType<T>(this SyntaxNode node, [NotNullWhen(true)] out T? parent) where T : SyntaxNode
	{
		var tempParent = node.Parent;

		while (tempParent != null)
		{
			if (tempParent is T t)
			{
				parent = t;
				return true;
			}

			tempParent = tempParent.Parent;
		}

		parent = null;
		return false;
	}

	public static bool TryGetSymbol<TSymbol>(this SemanticModel semanticModel, SyntaxNode? node, [NotNullWhen(true)] out TSymbol? value) where TSymbol : ISymbol
	{
		try
		{
			if (node is not null)
			{
				var info = semanticModel.GetSymbolInfo(node);

				if (info.Symbol is null)
				{
					if (semanticModel.Compilation.TryGetSemanticModel(node, out var semantic))
					{
						info = semantic.GetSymbolInfo(node);
					}
					else
					{
						value = default;
						return false;
					}
				}

				if (info.Symbol is TSymbol symbol)
				{
					value = symbol;
					return true;
				}
			}
		}
		catch (Exception e)
		{

		}

		value = default;
		return false;
	}

	public static string GetDeterministicHashString(this SyntaxNode node)
	{
		var hash = node.GetDeterministicHash();

		// Fold to 32-bit to keep identifier suffix short (similar length as previous implementation).
		var folded = (uint) (hash ^ (hash >> 32));

		return Convert.ToBase64String(BitConverter.GetBytes(folded)).TrimEnd('=')
			.Replace('+', '_')
			.Replace('/', '_');
	}

	public static ulong GetDeterministicHash(this SyntaxNode? node)
	{
		if (node is null)
		{
			return 0;
		}

		return DeteministicHashVisitor.Instance.Visit(node);
	}

	public static TAttribute ToAttribute<TAttribute>(this AttributeData attributeData)
		where TAttribute : Attribute
	{
		var type = typeof(TAttribute);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{attributeData.AttributeClass?.ToDisplayString()}' not found.");
		}

		var constructorArgs = attributeData.ConstructorArguments
			.Select(a => a.Value)
			.ToArray();

		var attribute = (TAttribute?) Activator.CreateInstance(type, constructorArgs);

		if (attribute == null)
		{
			throw new InvalidOperationException($"Could not create instance of '{type.FullName}'.");
		}

		foreach (var namedArg in attributeData.NamedArguments)
		{
			var property = type.GetProperty(namedArg.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

			if (property != null && property.CanWrite)
			{
				property.SetValue(attribute, namedArg.Value.Value);
			}
			else
			{
				var field = type.GetField(namedArg.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

				if (field != null)
				{
					field.SetValue(attribute, namedArg.Value.Value);
				}
			}
		}

		return attribute;
	}

	public static bool IsNumericType(this ITypeSymbol? t)
	{
		return t is not null && t.SpecialType is
			SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
			SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
			SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
	}

	public static bool IsNonFloatingNumeric(this ITypeSymbol? t)
	{
		return t is not null && IsNumericType(t)
		                     && t.SpecialType is not SpecialType.System_Single and not SpecialType.System_Double;
	}

	public static bool IsBoolType(this ITypeSymbol? t)
	{
		return t?.SpecialType == SpecialType.System_Boolean;
	}

	public static bool IsUnsignedInteger(this ITypeSymbol t)
	{
		return t.SpecialType is SpecialType.System_Byte
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64;
	}

	public static bool TryGetLiteralValue(this SyntaxNode? node, MetadataLoader loader, IDictionary<string, VariableItem> variables, out object? value)
	{
		switch (node)
		{
			case LiteralExpressionSyntax { Token.Value: var v }:
				value = v;
				return true;
			case IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue:
				if (variable.Value is SyntaxNode sn)
				{
					return TryGetLiteralValue(sn, loader, variables, out value);
				}

				value = variable.Value;
				return true;
			// unwrap ( ... )
			case ParenthesizedExpressionSyntax paren:
				return TryGetLiteralValue(paren.Expression, loader, variables, out value);
			// ^n => System.Index(n, fromEnd: true)
			case PrefixUnaryExpressionSyntax prefix when prefix.OperatorToken.IsKind(SyntaxKind.CaretToken):
			{
				if (TryGetLiteralValue(prefix.Operand, loader, variables, out var inner) && inner is not null)
				{
					try
					{
						var indexType = loader.GetType("System.Index");

						if (indexType is not null)
						{
							var ctor = indexType.GetConstructor([ typeof(int), typeof(bool) ]);

							if (ctor is not null)
							{
								var intVal = Convert.ToInt32(inner);
								value = ctor.Invoke([ intVal, true ]);
								return true;
							}
						}
					}
					catch { }
				}
				value = null;
				return false;
			}
			// a..b => System.Range
			case RangeExpressionSyntax rangeSyntax:
			{
				try
				{
					var indexType = loader.GetType("System.Index");
					var rangeType = loader.GetType("System.Range");

					if (indexType is null || rangeType is null)
					{
						value = null;
						return false;
					}

					object? MakeIndex(ExpressionSyntax expr)
					{
						if (TryGetLiteralValue(expr, loader, variables, out var innerVal) && innerVal is not null)
						{
							// Already an Index (e.g., ^n handled above)
							if (innerVal.GetType().FullName == "System.Index")
							{
								return innerVal;
							}

							// Wrap int as FromStart
							if (innerVal is IConvertible)
							{
								var intVal = Convert.ToInt32(innerVal);
								var ctor2 = indexType.GetConstructor([ typeof(int), typeof(bool) ]);
								var ctor1 = indexType.GetConstructor([ typeof(int) ]);
								if (ctor2 is not null) return ctor2.Invoke([ intVal, false ]);
								if (ctor1 is not null) return ctor1.Invoke([ intVal ]);
							}
						}
						return null;
					}

					var leftIdx = rangeSyntax.LeftOperand is null ? null : MakeIndex(rangeSyntax.LeftOperand);
					var rightIdx = rangeSyntax.RightOperand is null ? null : MakeIndex(rangeSyntax.RightOperand);

					if (leftIdx is null && rightIdx is null)
					{
						var allProp = rangeType.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
						value = allProp?.GetValue(null);
						return value is not null;
					}

					if (leftIdx is not null && rightIdx is null)
					{
						var startAt = rangeType.GetMethod("StartAt", BindingFlags.Public | BindingFlags.Static, null, [ indexType ], null);
						value = startAt?.Invoke(null, [ leftIdx ]);
						return value is not null;
					}

					if (leftIdx is null && rightIdx is not null)
					{
						var endAt = rangeType.GetMethod("EndAt", BindingFlags.Public | BindingFlags.Static, null, [ indexType ], null);
						value = endAt?.Invoke(null, [ rightIdx ]);
						return value is not null;
					}

					var ctorRange = rangeType.GetConstructor([ indexType, indexType ]);
					value = ctorRange?.Invoke([ leftIdx, rightIdx ]);
					return value is not null;
				}
				catch
				{
					value = null;
					return false;
				}
			}
			case CastExpressionSyntax castExpressionSyntax:
			{
				if (TryGetLiteralValue(castExpressionSyntax.Expression, loader, variables, out var innerVal))
				{
					// Try to resolve the *textual* type name from the syntax node (no semantic model)
					var typeName = castExpressionSyntax.Type switch
					{
						PredefinedTypeSyntax p => p.Keyword.ValueText,
						IdentifierNameSyntax id => id.Identifier.Text,
						QualifiedNameSyntax q => q.ToString(), // preserve qualification for System.* cases
						GenericNameSyntax g => g.Identifier.Text,
						NullableTypeSyntax n => (n.ElementType as PredefinedTypeSyntax)?.Keyword.ValueText ?? n.ElementType.ToString(),
						_ => castExpressionSyntax.Type.ToString()
					};

					// normalize common C# keywords and System.* names
					if (typeName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
						typeName = typeName["System.".Length..];

					typeName = typeName switch
					{
						"int" => "Int32",
						"short" => "Int16",
						"long" => "Int64",
						"uint" => "UInt32",
						"ushort" => "UInt16",
						"ulong" => "UInt64",
						"float" => "Single",
						"double" => "Double",
						"bool" => "Boolean",
						"string" => "String",
						"char" => "Char",
						"decimal" => "Decimal",
						"sbyte" => "SByte",
						"byte" => "Byte",
						_ => typeName
					};

					value = typeName switch
					{
						"Boolean" => Convert.ToBoolean(innerVal),
						"Byte" => Convert.ToByte(innerVal),
						"Char" => Convert.ToChar(innerVal),
						"DateTime" => Convert.ToDateTime(innerVal),
						"Decimal" => Convert.ToDecimal(innerVal),
						"Double" => Convert.ToDouble(innerVal),
						"Int16" => Convert.ToInt16(innerVal),
						"Int32" => Convert.ToInt32(innerVal),
						"Int64" => Convert.ToInt64(innerVal),
						"SByte" => Convert.ToSByte(innerVal),
						"Single" => Convert.ToSingle(innerVal),
						"String" => Convert.ToString(innerVal),
						"UInt16" => Convert.ToUInt16(innerVal),
						"UInt32" => Convert.ToUInt32(innerVal),
						"UInt64" => Convert.ToUInt64(innerVal),
						"Object" => innerVal,
						_ => innerVal
					};

					return true;
				}
				break;
			}
		}

		// Fallback to semantic constant evaluation
		// if (TryGetConstantValue(semanticModel.Compilation, loader, node, new VariableItemDictionary(variables), token, out var constVal))
		// {
		// 	value = constVal;
		// 	return true;
		// }

		value = null;
		return false;
	}

	public static bool TryGetMinValue([NotNullWhen(true)] this ITypeSymbol? type, [NotNullWhen(true)] out object? minValue)
	{
		if (type == null)
		{
			minValue = null;
			return false;
		}

		// Handle primitive types
		switch (type.SpecialType)
		{
			case SpecialType.System_Boolean:
				minValue = false;
				return true;
			case SpecialType.System_Byte:
				minValue = byte.MinValue;
				return true;
			case SpecialType.System_SByte:
				minValue = sbyte.MinValue;
				return true;
			case SpecialType.System_Int16:
				minValue = short.MinValue;
				return true;
			case SpecialType.System_UInt16:
				minValue = ushort.MinValue;
				return true;
			case SpecialType.System_Int32:
				minValue = int.MinValue;
				return true;
			case SpecialType.System_UInt32:
				minValue = uint.MinValue;
				return true;
			case SpecialType.System_Int64:
				minValue = long.MinValue;
				return true;
			case SpecialType.System_UInt64:
				minValue = ulong.MinValue;
				return true;
			case SpecialType.System_Single:
				minValue = float.MinValue;
				return true;
			case SpecialType.System_Double:
				minValue = double.MinValue;
				return true;
			case SpecialType.System_Decimal:
				minValue = decimal.MinValue;
				return true;
			case SpecialType.System_Char:
				minValue = char.MinValue;
				return true;
			default:
				minValue = null;
				return false;
		}
	}

	public static bool TryGetMaxValue([NotNullWhen(true)] this ITypeSymbol? type, [NotNullWhen(true)] out object? maxValue)
	{
		if (type == null)
		{
			maxValue = null;
			return false;
		}

		// Handle primitive types
		switch (type.SpecialType)
		{
			case SpecialType.System_Boolean:
				maxValue = true;
				return true;
			case SpecialType.System_Byte:
				maxValue = byte.MaxValue;
				return true;
			case SpecialType.System_SByte:
				maxValue = sbyte.MaxValue;
				return true;
			case SpecialType.System_Int16:
				maxValue = short.MaxValue;
				return true;
			case SpecialType.System_UInt16:
				maxValue = ushort.MaxValue;
				return true;
			case SpecialType.System_Int32:
				maxValue = int.MaxValue;
				return true;
			case SpecialType.System_UInt32:
				maxValue = uint.MaxValue;
				return true;
			case SpecialType.System_Int64:
				maxValue = long.MaxValue;
				return true;
			case SpecialType.System_UInt64:
				maxValue = ulong.MaxValue;
				return true;
			case SpecialType.System_Single:
				maxValue = float.MaxValue;
				return true;
			case SpecialType.System_Double:
				maxValue = double.MaxValue;
				return true;
			case SpecialType.System_Decimal:
				maxValue = decimal.MaxValue;
				return true;
			case SpecialType.System_Char:
				maxValue = char.MaxValue;
				return true;
			default:
				maxValue = null;
				return false;
		}
	}

	public static bool TryGetMinMaxValue(this ITypeSymbol? type, [NotNullWhen(true)] out object? minValue, [NotNullWhen(true)] out object? maxValue)
	{
		var hasMin = type.TryGetMinValue(out minValue);
		var hasMax = type.TryGetMaxValue(out maxValue);
		return hasMin && hasMax;
	}

	public static bool IsSpanOrReadOnlySpan([NotNullWhen(true)] this ITypeSymbol? type)
	{
		return type.IsSpan() || type.IsReadOnlySpan();
	}

	public static bool IsSpan([NotNullWhen(true)] this ITypeSymbol? type)
	{
		return type is INamedTypeSymbol
		{
			Name: nameof(Span<>),
			TypeArguments.Length: 1,
			ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true }
		};
	}

	public static bool IsReadOnlySpan([NotNullWhen(true)] this ISymbol? symbol)
	{
		return symbol is INamedTypeSymbol
		{
			Name: nameof(ReadOnlySpan<>),
			TypeArguments.Length: 1,
			ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true }
		};
	}

	public static bool IsParentKind([NotNullWhen(true)] this SyntaxNode? node, SyntaxKind kind)
	{
		return node?.Parent.IsKind(kind) ?? false;
	}

	public static bool IsLeftSideOfAnyAssignExpression([NotNullWhen(true)] this SyntaxNode? node)
	{
		return node?.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node;
	}

	public static bool IsKind<TNode>([NotNullWhen(true)] this SyntaxNode? node, SyntaxKind kind, [NotNullWhen(true)] out TNode? result)
		where TNode : SyntaxNode
	{
		if (node.IsKind(kind))
		{
			result = (TNode) node;
			return true;
		}

		result = null;
		return false;
	}

	public static bool IsFloatingPoint(this TypeInfo typeInfo)
	{
		return IsFloatingPoint(typeInfo) || IsFloatingPoint(typeInfo.ConvertedType);
	}

	public static bool IsMethod(this IMethodSymbol method, Type parentType, params IEnumerable<string> methodNames)
	{
		return method.ContainingType.ToString() == parentType.FullName && methodNames.Any(n => method.Name == n);
	}

	private static bool IsFloatingPoint([NotNullWhen(returnValue: true)] ITypeSymbol? type)
	{
		return type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double;
	}
}