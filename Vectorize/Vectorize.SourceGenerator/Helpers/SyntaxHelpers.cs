using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Vectorize.Helpers;

public static class SyntaxHelpers
{
	public static SyntaxKind GetSyntaxKind(object? value)
	{
		return value switch
		{
			int => SyntaxKind.NumericLiteralExpression,
			float => SyntaxKind.NumericLiteralExpression,
			double => SyntaxKind.NumericLiteralExpression,
			long => SyntaxKind.NumericLiteralExpression,
			decimal => SyntaxKind.NumericLiteralExpression,
			string => SyntaxKind.StringLiteralExpression,
			char => SyntaxKind.CharacterLiteralExpression,
			true => SyntaxKind.TrueLiteralExpression,
			false => SyntaxKind.FalseLiteralExpression,
			null => SyntaxKind.NullLiteralExpression,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public static object? GetVariableValue(Compilation compilation, SyntaxNode? expression, Dictionary<string, object?> variables)
	{
		if (!TryGetVariableValue(compilation, expression, variables, out var value))
		{
			value = null;
		}

		return value;
	}

	public static bool TryGetVariableValue(Compilation compilation, SyntaxNode? expression, Dictionary<string, object?> variables, out object? value)
	{
		if (TryGetSemanticModel(compilation, expression, out var semanticModel) && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: var temp })
		{
			value = temp;
			return true;
		}

		switch (expression)
		{
			case LiteralExpressionSyntax literal:
				switch (literal.Kind())
				{
					case SyntaxKind.StringLiteralExpression:
					case SyntaxKind.CharacterLiteralExpression:
						value = literal.Token.Value;
						return true;
					case SyntaxKind.TrueLiteralExpression:
						value = true;
						return true;
					case SyntaxKind.FalseLiteralExpression:
						value = false;
						return true;
					case SyntaxKind.NullLiteralExpression:
						value = null;
						return true;
					default:
						value = literal.Token.Value;
						return true;
				}
			case IdentifierNameSyntax identifier:
				return variables.TryGetValue(identifier.Identifier.Text, out value);
			case MemberAccessExpressionSyntax simple:
				return TryGetVariableValue(compilation, simple.Expression, variables, out value);
			default:
				value = null;
				return true;
		}
	}

	public static string? GetVariableName(SyntaxNode? expression)
	{
		return expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.Text,
			_ => null
		};
	}

	public static ExpressionSyntax CreateLiteral<T>(T? value)
	{
		switch (value)
		{
			case byte bb:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(bb));
			case int i:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i));
			case float f:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(f));
			case double d:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(d));
			case long l:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(l));
			case decimal dec:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(dec));
			case string s1:
				return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s1));
			case char c:
				return SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(c));
			case bool b:
				return SyntaxFactory.LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
			case null:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
		}

		if (value is IEnumerable enumerable)
		{
			return SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList<CollectionElementSyntax>(
				enumerable
					.Cast<object?>()
					.Select(s => SyntaxFactory.ExpressionElement(CreateLiteral(s)))));
		}

		throw new ArgumentOutOfRangeException();
	}

	public static object? GetConstantValue(Compilation compilation, SyntaxNode expression, CancellationToken token)
	{
		if (TryGetConstantValue(compilation, expression, token, out var value))
		{
			return value;
		}

		return null;
	}

	public static bool TryGetConstantValue(Compilation compilation, SyntaxNode expression, CancellationToken token, out object? value)
	{
		try
		{
			if (TryGetSemanticModel(compilation, expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var temp })
			{
				value = temp;
				return true;
			}

			switch (expression)
			{
				case LiteralExpressionSyntax literal:
					value = literal.Token.Value;
					return true;
				case ImplicitArrayCreationExpressionSyntax array:
					value = array.Initializer.Expressions
						.Select(x => GetConstantValue(compilation, x, token))
						.ToArray();
					return true;
				case CollectionExpressionSyntax collection:
					value = collection.Elements
						.Select(x => GetConstantValue(compilation, x, token))
						.ToArray();
					return true;
				case MemberAccessExpressionSyntax memberAccess when semanticModel.GetOperation(memberAccess) is IPropertyReferenceOperation propertyOperation:
					if (propertyOperation.Property.IsStatic)
					{
						value = GetPropertyValue(compilation, propertyOperation.Property, null);
						return true;
					}

					if (TryGetConstantValue(compilation, memberAccess.Expression, token, out var instance))
					{
						value = GetPropertyValue(compilation, propertyOperation.Property, instance);
						return true;
					}

					value = null;
					return false;
				case InvocationExpressionSyntax invocation when semanticModel.GetOperation(invocation) is IInvocationOperation operation:
					if (operation.TargetMethod.IsStatic) //  && operation.Arguments.All(x => x.Value.ConstantValue.HasValue))
					{
						var methodParameters = operation.TargetMethod.Parameters;
						var arguments = invocation.ArgumentList.Arguments
								.Select(s => GetConstantValue(compilation, s.Expression, token))
								.ToArray();

						if (methodParameters.Length > 0 && methodParameters.Last().IsParams)
						{
							var fixedArguments = arguments.Take(methodParameters.Length - 1).ToArray();
							var paramsArguments = arguments.Skip(methodParameters.Length - 1).ToArray();

							var finalArguments = new object?[fixedArguments.Length + 1];
							Array.Copy(fixedArguments, finalArguments, fixedArguments.Length);
							finalArguments[fixedArguments.Length] = paramsArguments;

							value = ExecuteMethod(compilation, operation.TargetMethod, null, finalArguments);
						}
						else
						{
							value = ExecuteMethod(compilation, operation.TargetMethod, null, arguments);
						}
						return true;
					}
					value = null;
					return false;
				case ObjectCreationExpressionSyntax creation when semanticModel.GetOperation(creation) is IObjectCreationOperation operation:
					if (operation.Arguments.All(x => x.Value.ConstantValue.HasValue))
					{
						var parameters = operation.Arguments.Select(x => x.Value.ConstantValue.Value).ToArray();
						value = ExecuteMethod(compilation, operation.Constructor, null, parameters);
						return true;
					}
					value = null;
					return false;
				default:
					value = null;
					return false;
			}
		}
		catch (Exception e)
		{
			value = null;
			return false;
		}
	}

	public static bool IsConstantValue(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken token)
	{
		if (semanticModel.GetConstantValue(expression, token) is { HasValue: true })
		{
			return true;
		}

		return expression switch
		{
			LiteralExpressionSyntax => true,
			CollectionExpressionSyntax => true,
			_ => false
		};
	}

	public static bool IsConstExprAttribute(AttributeData? attribute)
	{
		return attribute?.AttributeClass is { Name: "ConstExprAttribute", ContainingNamespace.Name: "ConstantExpression" };
	}

	public static bool IsNumericType(ITypeSymbol type)
	{
		return type.SpecialType is SpecialType.System_Byte
			or SpecialType.System_SByte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64
			or SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Decimal;
	}

	public static bool IsImmutableArrayOfNumbers(ITypeSymbol type)
	{
		if (type is INamedTypeSymbol { Name: "IReadOnlyList", TypeArguments.Length: 1 } namedType && namedType.ContainingNamespace.ToString() == "System.Collections.Generic")
		{
			return IsNumericType(namedType.TypeArguments[0]);
		}

		return false;
	}

	public static object? ExecuteMethod(Compilation compilation, IMethodSymbol methodSymbol, object? instance, params object?[]? parameters)
	{
		var fullyQualifiedName = methodSymbol.ContainingType.ToDisplayString();
		var methodName = methodSymbol.Name;

		var type = instance?.GetType() ?? GetTypes(compilation).FirstOrDefault(f => f.FullName == fullyQualifiedName);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{fullyQualifiedName}' not found");
		}

		var methodInfo = type
			.GetMethods(methodSymbol.IsStatic
				? BindingFlags.Public | BindingFlags.Static
				: BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(f =>
			{
				if (f.Name != methodName)
				{
					return false;
				}

				var methodParameters = f
					.GetParameters()
					.Select<ParameterInfo, ITypeSymbol?>(s =>
					{
						var type = s.ParameterType;

						if (type.IsArray)
						{
							var elementType = type.GetElementType();
							return compilation.CreateArrayTypeSymbol(compilation.GetTypeByMetadataName(elementType.FullName));
						}

						return compilation.GetTypeByMetadataName(type.FullName);
					})
					.ToList();

				if (methodParameters.Count != methodSymbol.Parameters.Length)
				{
					return false;
				}

				for (var i = 0; i < methodParameters.Count; i++)
				{
					if (!SymbolEqualityComparer.Default.Equals(methodParameters[i], methodSymbol.Parameters[i].Type))
					{
						return false;
					}
				}

				return true;
			});

		if (methodInfo == null)
		{
			throw new InvalidOperationException($"Methode '{methodName}' niet gevonden in type '{fullyQualifiedName}'.");
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

	public static object? GetPropertyValue(Compilation compilation, IPropertySymbol propertySymbol, object? instance)
	{
		var fullyQualifiedTypeName = $"{GetFullNamespace(propertySymbol.ContainingNamespace)}.{propertySymbol.ContainingType.MetadataName}";
		var assembly = GetAssemblyByType(compilation, propertySymbol.ContainingType);

		var type = assembly.GetType(fullyQualifiedTypeName);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{fullyQualifiedTypeName}' niet gevonden in assembly '{assembly.FullName}'.");
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

	public static object? GetFieldValue(Compilation compilation, IFieldSymbol fieldSymbol, object? instance)
	{
		var fullyQualifiedTypeName = fieldSymbol.ContainingType.ToDisplayString();
		var assembly = GetAssemblyByType(compilation, fieldSymbol.ContainingType);
		var type = assembly?.GetType(fullyQualifiedTypeName);

		if (type == null)
		{
			throw new InvalidOperationException($"Type '{fullyQualifiedTypeName}' niet gevonden in assembly '{assembly.FullName}'.");
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

	public static bool TryGetSemanticModel(Compilation compilation, SyntaxNode? node, out SemanticModel semanticModel)
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

	public static SemanticModel GetSemanticModel(Compilation compilation, SyntaxNode node)
	{
		return TryGetSemanticModel(compilation, node, out var semanticModel)
			? semanticModel
			: throw new InvalidOperationException("SemanticModel could not be retrieved.");
	}

	public static string GetFullNamespace(INamespaceSymbol? namespaceSymbol)
	{
		if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
		{
			return string.Empty;
		}

		var parts = new List<string>();
		var current = namespaceSymbol;

		while (current is { IsGlobalNamespace: false })
		{
			parts.Add(current.Name);
			current = current.ContainingNamespace;
		}

		parts.Reverse();
		return String.Join(".", parts);
	}

	public static bool TryGetOperation<TOperation>(Compilation compilation, ISymbol symbol, out TOperation operation) where TOperation : IOperation
	{
		if (TryGetSemanticModel(compilation, symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(), out var semanticModel)
				&& semanticModel.GetOperation(symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()) is IOperation op)
		{
			operation = (TOperation)op;
			return true;
		}

		operation = default!;
		return false;
	}

	public static bool TryGetOperation<TOperation>(Compilation compilation, SyntaxNode? node, out TOperation operation) where TOperation : IOperation
	{
		if (node is not null
			&& TryGetSemanticModel(compilation, node, out var semanticModel)
			&& semanticModel.GetOperation(node) is IOperation op)
		{
			operation = (TOperation)op;
			return true;
		}

		operation = default!;
		return false;
	}

	public static Assembly? GetAssemblyByType(Compilation compilation, ITypeSymbol typeSymbol)
	{
		// Verkrijg het assembly-symbool dat dit type bevat
		IAssemblySymbol? assemblySymbol = typeSymbol.ContainingAssembly;

		if (assemblySymbol == null)
		{
			return null;
		}

		// assemblySymbol.Identity bevat de naam, versie, enz.
		// Nu zoeken we in de referenties van de compilatie naar een MetadataReference 
		// waarvan de FilePath overeenkomt met deze assembly.
		foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
		{
			if (String.IsNullOrEmpty(reference.FilePath))
			{
				continue;
			}

			try
			{
				var loadedAssembly = Assembly.UnsafeLoadFrom(reference.FilePath);

				// Vergelijk de assemblynaam
				if (String.Equals(loadedAssembly.GetName().Name, assemblySymbol.Identity.Name, StringComparison.OrdinalIgnoreCase))
				{
					return loadedAssembly;
				}
			}
			catch (Exception e)
			{
				// Als het laden mislukt, gaan we verder
			}
		}

		return AppDomain.CurrentDomain
			.GetAssemblies()
			.FirstOrDefault(a => a.DefinedTypes
				.Any(a => SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(a.FullName), typeSymbol)));
	}

	public static IEnumerable<Type> GetTypesByType(Compilation compilation, ITypeSymbol typeSymbol)
	{
		if (typeSymbol is INamedTypeSymbol namedTypeSymbol && typeSymbol != namedTypeSymbol.OriginalDefinition)
		{
			return GetTypesByType(compilation, namedTypeSymbol.OriginalDefinition)
				.Select(s =>
				{
					if (s.IsGenericTypeDefinition)
					{
						var arguments = namedTypeSymbol.TypeArguments
							.Select(s => GetTypeByType(compilation, s))
							.ToArray();

						return s.MakeGenericType(arguments);
					}

					return s;
				});
		}

		return GetTypes(compilation)
			.Where(w => SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(w.FullName), typeSymbol));
	}

	public static Type? GetTypeByType(Compilation compilation, ITypeSymbol typeSymbol)
	{
		return GetTypesByType(compilation, typeSymbol).FirstOrDefault();
	}

	public static IEnumerable<Type> GetTypes(Compilation compilation)
	{
		return compilation.References
			.OfType<PortableExecutableReference>()
			.SelectMany(s =>
			{
				if (String.IsNullOrEmpty(s.FilePath))
				{
					return [];
				}

				try
				{
					var loadedAssembly = Assembly.UnsafeLoadFrom(s.FilePath);

					return loadedAssembly.ExportedTypes.Concat(loadedAssembly.DefinedTypes);
				}
				catch (Exception e)
				{
					return [];
				}
			})
			.Concat(AppDomain.CurrentDomain
				.GetAssemblies()
				.SelectMany(s =>
				{
					try
					{
						return s.DefinedTypes;
					}
					catch
					{
						return Enumerable.Empty<Type>();
					}
				}))
			.Distinct();
	}

	public static bool IsInConstExprBody(SyntaxNode node)
	{
		if (node is MethodDeclarationSyntax method)
		{
			return method.AttributeLists
				.SelectMany(s => s.Attributes)
				.Any(a => a.Name.ToString() == "ConstExpr");
		}

		if (node.Parent is null)
		{
			return false;
		}

		return IsInConstExprBody(node.Parent);
	}
}
