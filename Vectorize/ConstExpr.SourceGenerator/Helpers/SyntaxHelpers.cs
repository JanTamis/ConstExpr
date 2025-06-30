using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
		if (compilation.TryGetSemanticModel(expression, out var semanticModel) && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: var temp })
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

	public static bool TryGetLiteral<T>(T? value, out ExpressionSyntax? expression)
	{
		expression = CreateLiteral(value);
		return expression is not null;
	}

	public static ExpressionSyntax? CreateLiteral<T>(T? value)
	{
		switch (value)
		{
			case byte bb:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(bb));
			case int i:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i));
			case uint ui:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ui));
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

		if (value.GetType().Name.Contains("Tuple"))
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
			return SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList<CollectionElementSyntax>(enumerable
				.Cast<object?>()
				.Select(s => SyntaxFactory.ExpressionElement(CreateLiteral(s)))));
		}

		return null;
	}

	public static object? GetConstantValue(Compilation compilation, MetadataLoader loader, SyntaxNode expression, CancellationToken token = default)
	{
		if (TryGetConstantValue(compilation, loader, expression, token, out var value))
		{
			return value;
		}

		return null;
	}

	public static bool TryGetConstantValue(Compilation compilation, MetadataLoader loader, SyntaxNode? expression, CancellationToken token, out object? value)
	{
		if (expression is null)
		{
			value = null;
			return false;
		}

		try
		{
			if (compilation.TryGetSemanticModel(expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var temp })
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
						.Select(x => GetConstantValue(compilation, loader, x, token))
						.ToArray();
					return true;
				case CollectionExpressionSyntax collection:
					value = collection.Elements
						.Select(x => GetConstantValue(compilation, loader, x, token))
						.ToArray();
					return true;
				case MemberAccessExpressionSyntax memberAccess when semanticModel.GetOperation(memberAccess) is IPropertyReferenceOperation propertyOperation:
					if (propertyOperation.Property.IsStatic)
					{
						value = compilation.GetPropertyValue(loader, propertyOperation.Property, null);
						return true;
					}

					if (TryGetConstantValue(compilation, loader, memberAccess.Expression, token, out var instance))
					{
						value = compilation.GetPropertyValue(loader, propertyOperation.Property, instance);
						return true;
					}

					value = null;
					return false;
				case InvocationExpressionSyntax invocation when semanticModel.GetOperation(invocation) is IInvocationOperation operation:
					if (operation.TargetMethod.IsStatic)
					{
						var methodParameters = operation.TargetMethod.Parameters;
						var arguments = invocation.ArgumentList.Arguments
							.Select(s => GetConstantValue(compilation, loader, s.Expression, token))
							.ToArray();

						if (methodParameters.Length > 0 && methodParameters.Last().IsParams)
						{
							var fixedArguments = arguments.Take(methodParameters.Length - 1).ToArray();
							var paramsArguments = arguments.Skip(methodParameters.Length - 1).ToArray();

							var finalArguments = new object?[fixedArguments.Length + 1];
							Array.Copy(fixedArguments, finalArguments, fixedArguments.Length);
							finalArguments[fixedArguments.Length] = paramsArguments;

							value = compilation.ExecuteMethod(loader, operation.TargetMethod, null, finalArguments);
						}
						else
						{
							value = compilation.ExecuteMethod(loader, operation.TargetMethod, null, arguments);
						}
						return true;
					}
					value = null;
					return false;
				case ObjectCreationExpressionSyntax creation when semanticModel.GetOperation(creation) is IObjectCreationOperation operation:
					if (operation.Arguments.All(x => x.Value.ConstantValue.HasValue))
					{
						var parameters = operation.Arguments.Select(x => x.Value.ConstantValue.Value).ToArray();
						value = compilation.ExecuteMethod(loader, operation.Constructor, null, parameters);
						return true;
					}
					value = null;
					return false;
				// for unit tests
				case ReturnStatementSyntax returnStatement:
					return TryGetConstantValue(compilation, loader, returnStatement.Expression, token, out value);
				case YieldStatementSyntax yieldStatement:
					return TryGetConstantValue(compilation, loader, yieldStatement.Expression, token, out value);
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
		if (compilation.TryGetSemanticModel(symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(), out var semanticModel)
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
				&& compilation.TryGetSemanticModel(node, out var semanticModel)
				&& semanticModel.GetOperation(node) is IOperation op)
		{
			operation = (TOperation)op;
			return true;
		}

		operation = default!;
		return false;
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

	public static bool IsIEnumerableRecursive(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "IEnumerable" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerableRecursive);
	}

	public static bool IsIEnumerableRecursive(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model)
					 && model.GetSymbolInfo(typeSymbol, token).Symbol is INamedTypeSymbol namedTypeSymbol
					 && IsIEnumerableRecursive(namedTypeSymbol);
	}

	public static bool IsIEnumerable(ITypeSymbol typeSymbol)
	{
		return typeSymbol.Name == "IEnumerable" 
			&& typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
	}

	public static bool IsIEnumerable(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model)
					 && model.GetSymbolInfo(typeSymbol, token).Symbol is INamedTypeSymbol namedTypeSymbol
					 && IsIEnumerable(namedTypeSymbol);
	}

	public static bool IsICollection(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "ICollection" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerableRecursive);
	}

	public static bool IsIList(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.Name == "IList" && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
		{
			return true;
		}

		return typeSymbol.Interfaces.Any(IsIEnumerableRecursive);
	}

	public static bool CheckMembers<TMember>(this ITypeSymbol item, Func<TMember, bool> selector, out TMember? member) where TMember : ISymbol
	{
		member = item.GetMembers()
			.OfType<TMember>()
			.FirstOrDefault(selector);

		member ??= item.AllInterfaces
			.SelectMany(i => i.GetMembers()
				.OfType<TMember>()
				.Where(selector))
			.FirstOrDefault();

		return member is not null;
	}

	public static bool CheckMembers<TMember>(this ITypeSymbol item, string name, Func<TMember, bool> selector, [NotNullWhen(true)] out TMember? member) where TMember : ISymbol
	{
		member = item.GetMembers(name)
			.OfType<TMember>()
			.FirstOrDefault(selector);

		member ??= item.AllInterfaces
			.SelectMany(i => i.GetMembers(name)
				.OfType<TMember>())
			.FirstOrDefault(selector);

		if (member is null && item is ITypeParameterSymbol typeParameterSymbol)
		{
			member = typeParameterSymbol.ConstraintTypes
				.SelectMany(i => i.GetMembers(name)
					.OfType<TMember>())
				.FirstOrDefault(selector);
		}

		return member is not null;
	}

	public static bool CheckMethodWithReturnType(this ITypeSymbol item, string name, ITypeSymbol returnType, [NotNullWhen(true)] out IMethodSymbol? member)
	{
		return item.CheckMembers(name, m => m.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(m.ReturnType, returnType), out member);
	}

	public static bool CheckMethod(this ITypeSymbol item, string name, ITypeSymbol[] parameters, [NotNullWhen(true)] out IMethodSymbol? member)
	{
		return item.CheckMembers(name, m => m.ReturnsVoid
																				&& m.Parameters.Length == parameters.Length
																				&& m.Parameters
																					.Select(s => s.Type)
																					.Zip(parameters, IsEqualSymbol)
																					.All(a => a), out member);
	}

	public static bool CheckMethod(this ITypeSymbol item, string name, ITypeSymbol returnType, ITypeSymbol[] parameters, [NotNullWhen(true)] out IMethodSymbol? member)
	{
		return item.CheckMembers(name, m => SymbolEqualityComparer.Default.Equals(m.ReturnType, returnType)
																				&& m.Parameters.Length == parameters.Length
																				&& m.Parameters
																					.Select(s => s.Type)
																					.Zip(parameters, IsEqualSymbol)
																					.All(a => a), out member);
	}

	public static void CheckMethods(this ITypeSymbol item, string name, Dictionary<Func<IMethodSymbol, bool>, Action<IMethodSymbol>> methods)
	{
		var items = item.GetMembers(name)
			.OfType<IMethodSymbol>();

		foreach (var methodSymbol in items)
		{
			foreach (var method in methods.Where(w => w.Key(methodSymbol)))
			{
				method.Value(methodSymbol);
				return;
			}
		}
	}

	public static bool IsEqualSymbol(ITypeSymbol typeSymbol, ITypeSymbol? other)
	{
		if (other is null)
		{
			return false;
		}

		if (SymbolEqualityComparer.Default.Equals(typeSymbol, other))
		{
			return true;
		}

		return other.AllInterfaces.Any(a => SymbolEqualityComparer.Default.Equals(a, other));
	}

	public static SyntaxNode? GetSyntaxNode(ISymbol symbol, CancellationToken cancellationToken = default)
	{
		return symbol.DeclaringSyntaxReferences
			.Select(s => s?.GetSyntax(cancellationToken))
			.FirstOrDefault(s => s is not null);
	}
}