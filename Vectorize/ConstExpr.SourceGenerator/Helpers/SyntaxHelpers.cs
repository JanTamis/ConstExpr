using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ConstExpr.SourceGenerator.Helpers;

public static class SyntaxHelpers
{
	public static SyntaxKind GetSyntaxKind(object? value)
	{
		return value switch
		{
			int or float or double or long or decimal => SyntaxKind.NumericLiteralExpression,
			string => SyntaxKind.StringLiteralExpression,
			char => SyntaxKind.CharacterLiteralExpression,
			true => SyntaxKind.TrueLiteralExpression,
			false => SyntaxKind.FalseLiteralExpression,
			null => SyntaxKind.NullLiteralExpression,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public static object? GetVariableValue(SyntaxNode? expression, IDictionary<string, object?> variables)
	{
		if (!TryGetVariableValue(expression, variables, out var value))
		{
			value = null;
		}

		return value;
	}

	public static bool TryGetVariableValue(SyntaxNode? expression, IDictionary<string, object?> variables, out object? value)
	{
		// if (compilation.TryGetSemanticModel(expression, out var semanticModel) && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: var temp })
		// {
		// 	value = temp;
		// 	return true;
		// }

		switch (expression)
		{
			case LiteralExpressionSyntax literal:
				{
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
				}
			case IdentifierNameSyntax identifier:
				return variables.TryGetValue(identifier.Identifier.Text, out value);
			case MemberAccessExpressionSyntax simple:
				return TryGetVariableValue(simple.Expression, variables, out value);
			default:
				{
					value = null;
					return true;
				}
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

	public static bool TryGetLiteral<T>(T? value, [NotNullWhen(true)] out ExpressionSyntax? expression)
	{
		expression = CreateLiteral(value);
		return expression is not null;
	}

	public static ExpressionSyntax CreateLiteral<T>(T? value)
	{
		switch (value)
		{
			case byte bb:
				return SyntaxFactory.CastExpression(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword)),
					SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(bb)));
			case int i:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i));
			case uint ui:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ui));
			case float f:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal($"{f.ToString(CultureInfo.InvariantCulture)}F", f));
			case double d:
				{
					if (Math.Abs(d - Math.Round(d)) < Double.Epsilon)
					{
						return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal($"{d.ToString(CultureInfo.InvariantCulture)}D", d));
					}

					return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(d));
				}
			case long l:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(l));
			case decimal dec:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(dec));
			case string s1:
				if (s1.Length == 0)
				{
					return SyntaxFactory.ParseExpression("String.Empty");
				}

				return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s1));
			case char c:
				return SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(c));
			case bool b:
				{
					return SyntaxFactory.LiteralExpression(b
						? SyntaxKind.TrueLiteralExpression
						: SyntaxKind.FalseLiteralExpression);
				}
			case Enum e:
				{
					var enumType = e.GetType();
					var enumValue = Enum.GetName(enumType, e);

					if (enumValue is not null)
					{
						return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							SyntaxFactory.IdentifierName(enumType.Name),
							SyntaxFactory.IdentifierName(enumValue));
					}
					return null;
				}
			case null:
				return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
			case DateTime dt:
			{
				return SyntaxFactory.ObjectCreationExpression(
					SyntaxFactory.IdentifierName("DateTime"))
					.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
					{
						SyntaxFactory.Argument(CreateLiteral(dt.Ticks)),
						SyntaxFactory.Argument(
							SyntaxFactory.MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								SyntaxFactory.IdentifierName("DateTimeKind"),
								SyntaxFactory.IdentifierName(dt.Kind.ToString())))
					})));
			}
		}

		// Support for Span<T> / ReadOnlySpan<T> via reflection by converting to array
		if (value is object obj && IsSpanLike(obj))
		{
			var array = TryToArray(obj);

			if (array != null)
			{
				// If char-span, emit a string literal
				var elemType = array.GetType().GetElementType();

				if (elemType == typeof(char))
				{
					var chars = (char[])array;
					return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(new string(chars)));
				}

				if (elemType == typeof(byte))
				{
					var bytes = (byte[])array;
					var data = Encoding.UTF8.GetString(bytes);
					var escaped = data
						.Replace("\\", @"\\")
						.Replace("\"", "\\\"")
						.Replace("\r", "\\r")
						.Replace("\n", "\\n")
						.Replace("\t", "\\t");

					return SyntaxFactory.ParseExpression($"\"{escaped}\"u8");
				}

				return SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList<CollectionElementSyntax>(array
					.Cast<object?>()
					.Select(s => SyntaxFactory.ExpressionElement(CreateLiteral(s)))));
			}
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

		return SyntaxFactory.ParseExpression(value.ToString());
	}

	private static bool IsSpanLike(object obj)
	{
		var type = obj.GetType();

		return type is { IsGenericType: true, Namespace: "System", Name: "Span`1" or "ReadOnlySpan`1" };
	}

	private static Array? TryToArray(object spanLike)
	{
		var type = spanLike.GetType();
		var toArray = type.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

		if (toArray != null)
		{
			return toArray.Invoke(spanLike, null) as Array;
		}
		return null;
	}

	public static object? GetConstantValue(Compilation compilation, MetadataLoader loader, SyntaxNode expression, IDictionary<string, object?>? variables, CancellationToken token = default)
	{
		if (TryGetConstantValue(compilation, loader, expression, variables, token, out var value))
		{
			return value;
		}

		return null;
	}

	public static bool TryGetConstantValue(Compilation compilation, MetadataLoader loader, SyntaxNode? expression, IDictionary<string, object?>? variables, CancellationToken token, out object? value)
	{
		if (expression is null)
		{
			value = null;
			return false;
		}

		try
		{
			// if (compilation.TryGetSemanticModel(expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var temp })
			// {
			// 	value = temp;
			// 	return true;
			// }

			switch (expression)
			{
				case LiteralExpressionSyntax literal:
					{
						value = literal.Token.Value;

						return true;
					}
				case ExpressionElementSyntax elementSyntax:
					{
						value = GetConstantValue(compilation, loader, elementSyntax.Expression, variables, token);

						return true;
					}
				case ImplicitArrayCreationExpressionSyntax array:
					{
						value = array.Initializer.Expressions
							.Select(x => GetConstantValue(compilation, loader, x, variables, token))
							.ToArray();

						return true;
					}
				case CollectionExpressionSyntax collection:
					{
						var elementType = collection.Elements.FirstOrDefault() is ExpressionElementSyntax firstElement
							? GetConstantValue(compilation, loader, firstElement.Expression, variables, token)?.GetType() ?? typeof(object)
							: typeof(object);

						var arrayLength = collection.Elements.Count;
						var data = Array.CreateInstance(elementType, arrayLength);

						for (var i = 0; i < arrayLength; i++)
						{
							var constantValue = GetConstantValue(compilation, loader, collection.Elements[i], variables, token);
							data.SetValue(constantValue, i);
						}

						value = data;
						return true;
					}
				case MemberAccessExpressionSyntax memberAccess when compilation.TryGetSemanticModel(expression, out var model) && model.GetOperation(memberAccess) is IMemberReferenceOperation memberOperation:
					{
						switch (memberOperation)
						{
							case IPropertyReferenceOperation:
								{
									if (memberOperation.Member.IsStatic)
									{
										value = loader.GetPropertyValue(memberOperation.Member, null);
										return true;
									}

									if (TryGetConstantValue(compilation, loader, memberAccess.Expression, variables, token, out var instance))
									{
										value = loader.GetPropertyValue(memberOperation.Member, instance);
										return true;
									}

									break;
								}
							case IFieldReferenceOperation:
								{
									if (memberOperation.Member.IsStatic)
									{
										value = loader.GetFieldValue(memberOperation.Member, null);
										return true;
									}

									if (TryGetConstantValue(compilation, loader, memberAccess.Expression, variables, token, out var instance))
									{
										value = loader.GetFieldValue(memberOperation.Member, instance);
										return true;
									}

									break;
								}
						}

						value = null;
						return false;
					}
				case InvocationExpressionSyntax invocation when compilation.TryGetSemanticModel(expression, out var model) && model.GetOperation(invocation) is IInvocationOperation operation:
					{
						if (operation.TargetMethod.IsStatic)
						{
							var methodParameters = operation.TargetMethod.Parameters;

							var arguments = invocation.ArgumentList.Arguments
								.Select(s => GetConstantValue(compilation, loader, s.Expression, variables, token));

							if (methodParameters.Length > 0 && methodParameters.Last().IsParams)
							{
								var fixedArguments = arguments.Take(methodParameters.Length - 1).ToArray();
								var paramsArguments = arguments.Skip(methodParameters.Length - 1).ToArray();

								var finalArguments = new object?[fixedArguments.Length + 1];
								Array.Copy(fixedArguments, finalArguments, fixedArguments.Length);
								finalArguments[fixedArguments.Length] = paramsArguments;

								if (loader.TryExecuteMethod(operation.TargetMethod, null, variables, finalArguments, out value))
								{
									return true;
								}
							}

							if (loader.TryExecuteMethod(operation.TargetMethod, null, variables, arguments, out value))
							{
								return true;
							}
						}

						value = null;
						return false;
					}
				case ObjectCreationExpressionSyntax creation when compilation.TryGetSemanticModel(expression, out var model) && model.GetOperation(creation) is IObjectCreationOperation operation:
					{
						if (operation.Arguments.All(x => x.Value.ConstantValue.HasValue)
								&& loader.TryExecuteMethod(operation.Constructor, null, variables, operation.Arguments.Select(x => x.Value.ConstantValue.Value), out value))
						{
							return true;
						}
						value = null;
						return false;
					}
				case CastExpressionSyntax castExpressionSyntax:
					{
						// Try Roslyn constant evaluation first
						if (compilation.TryGetSemanticModel(castExpressionSyntax, out var castModel))
						{
							var constVal = castModel.GetConstantValue(castExpressionSyntax, token);

							if (constVal.HasValue)
							{
								value = constVal.Value;
								return true;
							}
						}

						// Evaluate the inner expression
						if (!TryGetConstantValue(compilation, loader, castExpressionSyntax.Expression, variables, token, out var inner))
						{
							value = null;
							return false;
						}

						// Handle nullable target types like (int?)x
						var targetTypeSyntax = castExpressionSyntax.Type;

						if (targetTypeSyntax is NullableTypeSyntax nullableType)
						{
							if (inner is null)
							{
								value = null;
								return true;
							}

							targetTypeSyntax = nullableType.ElementType;
						}

						// Handle predefined primitive casts
						if (targetTypeSyntax is PredefinedTypeSyntax predefined)
						{
							var kind = predefined.Keyword.Kind();

							try
							{
								switch (kind)
								{
									case SyntaxKind.SByteKeyword:
										value = Convert.ToSByte(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.ByteKeyword:
										value = Convert.ToByte(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.ShortKeyword:
										value = Convert.ToInt16(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.UShortKeyword:
										value = Convert.ToUInt16(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.IntKeyword:
										value = Convert.ToInt32(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.UIntKeyword:
										value = Convert.ToUInt32(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.LongKeyword:
										value = Convert.ToInt64(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.ULongKeyword:
										value = Convert.ToUInt64(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.FloatKeyword:
										value = Convert.ToSingle(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.DoubleKeyword:
										value = Convert.ToDouble(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.DecimalKeyword:
										value = Convert.ToDecimal(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.CharKeyword:
										value = inner is char ch ? ch : Convert.ToChar(inner, CultureInfo.InvariantCulture);
										return true;
									case SyntaxKind.StringKeyword:
										if (inner is string s)
										{
											value = s;
											return true;
										}
										value = null;
										return false;
									case SyntaxKind.BoolKeyword:
										if (inner is bool b)
										{
											value = b;
											return true;
										}
										value = null;
										return false;
									case SyntaxKind.ObjectKeyword:
										value = inner;
										return true;
								}
							}
							catch
							{
								value = null;
								return false;
							}
						}

						// Fallback to Roslyn constant evaluation retry
						if (compilation.TryGetSemanticModel(castExpressionSyntax, out var castModel2))
						{
							var cv = castModel2.GetConstantValue(castExpressionSyntax, token);

							if (cv.HasValue)
							{
								value = cv.Value;
								return true;
							}
						}

						value = null;
						return false;
					}
				// for unit tests
				case ReturnStatementSyntax returnStatement:
					return TryGetConstantValue(compilation, loader, returnStatement.Expression, variables, token, out value);
				case YieldStatementSyntax yieldStatement:
					return TryGetConstantValue(compilation, loader, yieldStatement.Expression, variables, token, out value);
				default:
					{
						if (compilation.TryGetSemanticModel(expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var temp })
						{
							value = temp;
							return true;
						}

						value = null;
						return false;
					}
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
		return attribute?.AttributeClass?.ToDisplayString() == typeof(ConstExprAttribute).FullName;
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

	public static bool TryGetOperation<TOperation>(Compilation compilation, ISymbol symbol, [NotNullWhen(true)] out TOperation? operation) where TOperation : IOperation
	{
		if (compilation.TryGetSemanticModel(symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(), out var semanticModel)
				&& semanticModel.GetOperation(symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()) is TOperation op)
		{
			operation = op;
			return true;
		}

		operation = default!;
		return false;
	}

	public static bool TryGetOperation<TOperation>(SemanticModel model, ISymbol symbol, [NotNullWhen(true)] out TOperation? operation) where TOperation : IOperation
	{
		var syntax = symbol.DeclaringSyntaxReferences
			.Select(symbol => symbol.GetSyntax())
			.FirstOrDefault();

		if (syntax == null)
		{
			operation = default!;
			return false;
		}

		if (model.GetOperation(syntax) is TOperation op1)
		{
			operation = op1;
			return true;
		}

		if (model.Compilation.TryGetSemanticModel(syntax, out var semanticModel)
				&& semanticModel.GetOperation(syntax) is TOperation op2)
		{
			operation = op2;
			return true;
		}

		operation = default!;
		return false;
	}

	public static bool TryGetOperation<TOperation>(Compilation compilation, SyntaxNode? node, out TOperation operation) where TOperation : IOperation
	{
		if (node is not null
				&& compilation.TryGetSemanticModel(node, out var semanticModel)
				&& semanticModel.GetOperation(node) is TOperation op)
		{
			operation = op;
			return true;
		}

		operation = default!;
		return false;
	}

	public static bool TryGetOperation<TOperation>(SemanticModel semanticModel, SyntaxNode? node, [NotNullWhen(true)] out TOperation? operation) where TOperation : IOperation
	{
		try
		{
			if (semanticModel.GetOperation(node) is TOperation op)
			{
				operation = op;
				return true;
			}

			return TryGetOperation(semanticModel.Compilation, node, out operation);
		}
		catch (Exception)
		{
			operation = default;
			return false;
		}
	}

	public static bool IsInConstExprBody(Compilation compilation, SyntaxNode node)
	{
		var typeText = node.ToFullString();
		var typeName = node.GetType();

		// Check if the node is part of a method or type with [ConstExpr] attribute
		switch (node)
		{
			case MethodDeclarationSyntax method:
				if (compilation.TryGetSemanticModel(method, out var model)
						&& model.GetDeclaredSymbol(method) is IMethodSymbol methodSymbol
						&& IsInConstExprBody(methodSymbol))
				{
					return true;
				}

				break;
		}

		if (node.Parent is null)
		{
			return false;
		}

		return IsInConstExprBody(compilation, node.Parent);
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

	public static bool IsIEnumerableRecursive(INamedTypeSymbol? typeSymbol)
	{
		if (typeSymbol is null)
		{
			return false;
		}

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

	public static bool IsIAsyncEnumerable(ITypeSymbol typeSymbol)
	{
		return typeSymbol.Name == "IAsyncEnumerable"
					 && typeSymbol.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
	}

	public static bool IsIEnumerable(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model)
					 && model.GetSymbolInfo(typeSymbol, token).Symbol is INamedTypeSymbol namedTypeSymbol
					 && IsIEnumerable(namedTypeSymbol);
	}

	public static bool IsIAsyncEnumerable(Compilation compilation, TypeSyntax typeSymbol, CancellationToken token = default)
	{
		return compilation.TryGetSemanticModel(typeSymbol, out var model)
					 && model.GetSymbolInfo(typeSymbol, token).Symbol is INamedTypeSymbol namedTypeSymbol
					 && IsIAsyncEnumerable(namedTypeSymbol);
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