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

namespace ConstExpr.SourceGenerator.Helpers;

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
				return SyntaxFactory.LiteralExpression(b
					? SyntaxKind.TrueLiteralExpression
					: SyntaxKind.FalseLiteralExpression);
			case null:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
		}

		if (value?.GetType().Name.Contains("Tuple") == true)
		{
			var tupleItems = new List<ArgumentSyntax>();
			var type = value.GetType();

			// Check for ValueTuple fields (Item1, Item2, etc.)
			var fields = type.GetFields().Where(f => f.Name.StartsWith("Item")).ToArray();

			if (fields.Length > 0)
			{
				foreach (var field in fields)
				{
					var itemValue = field.GetValue(value);
					tupleItems.Add(SyntaxFactory.Argument(CreateLiteral(itemValue)));
				}
			}
			else
			{
				// Check for Tuple properties (Item1, Item2, etc.)
				var properties = type.GetProperties().Where(p => p.Name.StartsWith("Item")).ToArray();

				foreach (var prop in properties)
				{
					var itemValue = prop.GetValue(value);
					tupleItems.Add(SyntaxFactory.Argument(CreateLiteral(itemValue)));
				}
			}

			return SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(tupleItems));
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

	public static object? GetConstantValue(Compilation compilation, SyntaxNode expression, CancellationToken token = default)
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
					if (operation.TargetMethod.IsStatic)
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
				// for unit tests
				case ReturnStatementSyntax returnStatement:
					return TryGetConstantValue(compilation, returnStatement.Expression, token, out value);
				case YieldStatementSyntax yieldStatement:
					return TryGetConstantValue(compilation, yieldStatement.Expression, token, out value);
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

	public static bool IsConstExprAttribute(AttributeData? attribute)
	{
		return attribute?.AttributeClass is { Name: "ConstExprAttribute", ContainingNamespace.Name: "ConstantExpression" };
	}

	public static object? ExecuteMethod(Compilation compilation, IMethodSymbol methodSymbol, object? instance, params object?[]? parameters)
	{
		var fullyQualifiedName = methodSymbol.ContainingType.ToDisplayString();
		var methodName = methodSymbol.Name;

		var type = GetTypeByType(compilation, methodSymbol.ContainingType)
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
					var methodParamType = GetTypeByType(compilation, methodSymbol.Parameters[i].Type);

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
					.Select(s => GetTypeByType(compilation, s))
					.ToArray();

				methodInfo = methodInfo.MakeGenericMethod(types);
			}

			var methodParams = methodInfo.GetParameters();

			for (var i = 0; i < methodParams.Length; i++)
			{
				if (!methodParams[i].ParameterType.IsAssignableFrom(GetTypeByType(compilation, methodSymbol.Parameters[i].Type)))
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
			operation = (TOperation) op;
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
			operation = (TOperation) op;
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
		if (typeSymbol is INamedTypeSymbol namedTypeSymbol && !SymbolEqualityComparer.Default.Equals(typeSymbol, namedTypeSymbol.OriginalDefinition))
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

	public static Type GetTypeByType(Compilation compilation, ITypeSymbol typeSymbol)
	{
		return GetTypesByType(compilation, typeSymbol).First();
	}

	public static IEnumerable<Type> GetTypes(Compilation compilation)
	{
		return AppDomain.CurrentDomain
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
			});
	}

	public static bool IsInConstExprBody(SyntaxNode node)
	{
		switch (node)
		{
			case MethodDeclarationSyntax method:
				if (method.AttributeLists
				    .SelectMany(s => s.Attributes)
				    .Any(a => a.Name.ToString() == "ConstExpr"))
				{
					return true;
				}
				break;
			case TypeDeclarationSyntax type:
				if (type.AttributeLists
				    .SelectMany(s => s.Attributes)
				    .Any(a => a.Name.ToString() == "ConstExpr"))
				{
					return true;
				}
				break;
		}

		if (node.Parent is null)
		{
			return false;
		}

		return IsInConstExprBody(node.Parent);
	}

	public static bool IsInConstExprBody(ISymbol node)
	{
		if (node.GetAttributes().Any(IsConstExprAttribute))
		{
			return true;
		}

		if (node.ContainingSymbol is null)
		{
			return false;
		}

		return IsInConstExprBody(node.ContainingSymbol);
	}

	public static bool IsIEnumerable(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "IEnumerable" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerable);
	}

	public static bool IsIEnumerable(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return TryGetSemanticModel(compilation, typeSymbol, out var model)
		       && model.GetSymbolInfo(typeSymbol, token).Symbol is INamedTypeSymbol namedTypeSymbol
		       && IsIEnumerable(namedTypeSymbol);
	}

	public static bool IsICollection(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "ICollection" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerable);
	}

	public static bool IsIList(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "IList" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerable);
	}

	public static bool CheckMembers<TMember>(this ITypeSymbol item, Func<TMember, bool> selector, out TMember? member) where TMember : ISymbol
	{
		member = item.GetMembers()
			.OfType<TMember>()
			.FirstOrDefault(selector);

		member = item.AllInterfaces
			.Select(i => i.GetMembers()
				.OfType<TMember>()
				.FirstOrDefault(selector))
			.FirstOrDefault(f => f is not null);

		return member is not null;
	}

	public static bool CheckMembers<TMember>(this ITypeSymbol item, string name, Func<TMember, bool> selector, out TMember? member) where TMember : ISymbol
	{
		member = item.GetMembers(name)
			.OfType<TMember>()
			.FirstOrDefault(selector);

		member = item.AllInterfaces
			.Select(i => i.GetMembers(name)
				.OfType<TMember>()
				.FirstOrDefault(selector))
			.FirstOrDefault(f => f is not null);

		return member is not null;
	}

	public static bool IsInterface(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return TryGetSemanticModel(compilation, typeSymbol, out var model) && model.GetSymbolInfo(typeSymbol, token).Symbol is ITypeSymbol { TypeKind: TypeKind.Interface };
	}

	public static void BuildEnumerable(IEnumerable<object?> items, INamedTypeSymbol namedTypeSymbol, int hashCode, IndentedStringBuilder builder)
	{
		var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
		var elementName = elementType?.ToDisplayString();

		builder.AppendLine($$"""
			private int _state;
			private int _initialThreadId;
			private {{elementName}} _current;

			public {{elementName}} Current => _current;

			object IEnumerator.Current => _current;

			public {{namedTypeSymbol.Name}}_{{hashCode}}(int state)
			{
				_state = state;
				_initialThreadId = Environment.CurrentManagedThreadId;
			}

			""");

		using (builder.AppendBlock("public bool MoveNext()"))
		{
			using (builder.AppendBlock("switch (_state)"))
			{
				var index = 0;

				foreach (var item in items)
				{
					builder.AppendLine($"case {index}:");
					builder.AppendLine("\t_state = -1;");
					builder.AppendLine($"\t_current = {CreateLiteral(item)};");
					builder.AppendLine($"\t_state = {index + 1};");
					builder.AppendLine("\treturn true;");
					index++;
				}

				builder.AppendLine($"default:");
				builder.AppendLine("\treturn false;");
			}
		}

		builder.AppendLine($$"""

			bool IEnumerator.MoveNext()
			{
				return MoveNext();
			}

			public IEnumerator<{{elementName}}> GetEnumerator()
			{
				if (_state == -2 && _initialThreadId == Environment.CurrentManagedThreadId)
				{
					_state = 0;
					return this;
				}
				
				return new {{namedTypeSymbol.Name}}_{{hashCode}}(0);
			}

			IEnumerator IEnumerable.GetEnumerator() 
			{
				return GetEnumerator();
			}

			public void Reset()
			{
				_state = 0;
			}

			public void Dispose()
			{
			}

			#endregion
			""");
	}

	

	// 	public static void BuildCollection(IEnumerable<object?> items, INamedTypeSymbol namedTypeSymbol, IndentedStringBuilder builder)
	// 	{
	// 		var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
	// 		var elementName = elementType?.ToDisplayString();
	//
	// 		builder.AppendLine($$"""
	// 			#region ICollection
	//
	// 			public int Count => {{items.Count()}};
	//
	// 			public bool IsReadOnly => true;
	//
	// 			public bool Contains({{elementName}} item)
	// 			{
	// 				return item is {{String.Join("\n\t\tor ", items.Select(s => CreateLiteral(s).ToString()).Distinct())}};
	// 			}
	//
	// 			""");
	//
	// 		using (builder.AppendBlock($"public void CopyTo({elementName}[] array, int arrayIndex)"))
	// 		{
	// 			using (builder.AppendBlock("if (array is null)"))
	// 			{
	// 				builder.AppendLine("throw new ArgumentNullException(nameof(array));");
	// 			}
	//
	// 			builder.AppendLine();
	//
	// 			using (builder.AppendBlock("if (arrayIndex < 0 || arrayIndex >= array.Length)"))
	// 			{
	// 				builder.AppendLine("throw new ArgumentOutOfRangeException(nameof(arrayIndex));");
	// 			}
	//
	// 			builder.AppendLine();
	//
	// 			var index = 0;
	//
	// 			foreach (var item in items)
	// 			{
	// 				builder.AppendLine($"array[{index++}] = {CreateLiteral(item)};");
	// 			}
	// 		}
	//
	// 		builder.AppendLine($$"""
	//
	// 			public void Add({{elementName}} item)
	// 			{
	// 				throw new NotSupportedException("Collection is read-only.");
	// 			}
	//
	// 			public void Clear()
	// 			{
	// 				throw new NotSupportedException("Collection is read-only.");
	// 			}
	//
	// 			public bool Remove({{elementName}} item)
	// 			{
	// 				throw new NotSupportedException("Collection is read-only.");
	// 			}
	//
	// 			#endregion
	// 			""");
	// 	}
	//
	// 	public static void BuildList(IEnumerable<object?> items, INamedTypeSymbol namedTypeSymbol, IndentedStringBuilder builder)
	// 	{
	// 		var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
	// 		var elementName = elementType?.ToDisplayString();
	//
	// 		builder.AppendLine("""
	// 			#region IList
	//
	// 			""");
	//
	// 		using (builder.AppendBlock($"public {elementName} this[int index]"))
	// 		{
	// 			using (builder.AppendBlock("get => index switch", "};"))
	// 			{
	// 				var index = 0;
	//
	// 				foreach (var item in items)
	// 				{
	// 					builder.AppendLine($"{index} => {CreateLiteral(item)},");
	//
	// 					index++;
	// 				}
	//
	// 				builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
	// 			}
	// 			builder.AppendLine("set => throw new NotSupportedException();");
	// 		}
	//
	// 		builder.AppendLine();
	//
	// 		using (builder.AppendBlock($"public int IndexOf({elementName} item)"))
	// 		{
	// 			using (builder.AppendBlock("return item switch", "};"))
	// 			{
	// 				var index = 0;
	// 				var hashSet = new HashSet<object?>();
	//
	// 				foreach (var item in items)
	// 				{
	// 					if (hashSet.Add(item))
	// 					{
	// 						builder.AppendLine($"{CreateLiteral(item)} => {index},");
	// 					}
	//
	// 					index++;
	// 				}
	//
	// 				builder.AppendLine("_ => -1,");
	// 			}
	// 		}
	//
	// 		builder.AppendLine($$"""
	//
	// 			public void Insert(int index, {{elementName}} item)
	// 			{
	// 				throw new NotSupportedException("Collection is read-only.");
	// 			}
	//
	// 			public void RemoveAt(int index)
	// 			{
	// 				throw new NotSupportedException("Collection is read-only.");
	// 			}
	// 			""");
	// 	}
}