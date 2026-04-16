using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;

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
		switch (expression)
		{
			case LiteralExpressionSyntax literal:
			{
				value = literal.Token.Value;
				return true;
			}
			case IdentifierNameSyntax identifier:
			{
				return variables.TryGetValue(identifier.Identifier.Text, out value);
			}
			case MemberAccessExpressionSyntax simple:
			{
				return TryGetVariableValue(simple.Expression, variables, out value);
			}
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

	public static bool TryCreateLiteral<T>(T? value, [NotNullWhen(true)] out ExpressionSyntax? result)
	{
		return TryCreateLiteral(value, false, out result);
	}

	public static bool TryCreateLiteral<T>(T? value, bool useExplicitByte, [NotNullWhen(true)] out ExpressionSyntax? result)
	{
		var valueType = value?.GetType();

		// check if value is lookup and skip if it is
		// check if value is IGrouping and skip if it is
		if (valueType?.GetInterface("System.Linq.ILookup`2") is not null
		    || valueType?.GetInterface("System.Linq.IGrouping`2") is not null)
		{
			result = null;
			return false;
		}

		switch (value)
		{
			case byte bb:
			{
				if (useExplicitByte)
				{
					result = CastExpression(
						PredefinedType(Token(SyntaxKind.ByteKeyword)),
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(bb)));
					return true;
				}

				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(bb));
				return true;
			}
		case int i and < 0 when i != int.MinValue:
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(-i)));
			return true;
		}
		case int i:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i));
			return true;
		}
		case uint ui:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(ui));
			return true;
		}
		case float f and < 0:
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal($"{(-f).ToString(CultureInfo.InvariantCulture)}F", -f)));
			return true;
		}
		case float f:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal($"{f.ToString(CultureInfo.InvariantCulture)}F", f));
			return true;
		}
		case double d and < 0:
		{
			var absD = -d;
			if (Math.Abs(absD - Math.Round(absD)) < Double.Epsilon)
			{
				result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
					LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal($"{absD.ToString(CultureInfo.InvariantCulture)}D", absD)));
				return true;
			}

			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(absD)));
			return true;
		}
		case double d:
		{
			if (Math.Abs(d - Math.Round(d)) < Double.Epsilon)
			{
				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal($"{d.ToString(CultureInfo.InvariantCulture)}D", d));
				return true;
			}

			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(d));
			return true;
		}
		case long l and < 0 when l != long.MinValue:
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(-l)));
			return true;
		}
		case long l:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(l));
			return true;
		}
		case ulong ul:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(ul));
			return true;
		}
		case decimal dec and < 0:
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(-dec)));
			return true;
		}
		case decimal dec:
		{
			result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(dec));
			return true;
		}
			case string s1:
			{
				result = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s1));
				return true;
			}
			case char c:
			{
				result = LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(c));
				return true;
			}
			case bool b:
			{
				result = LiteralExpression(b
					? SyntaxKind.TrueLiteralExpression
					: SyntaxKind.FalseLiteralExpression);
				return true;
			}
			case Enum e:
			{
				var enumType = e.GetType();
				var enumValue = Enum.GetName(enumType, e);

				if (enumValue is null)
				{
					result = null;
					return false;
				}

				result = MemberAccessExpression(IdentifierName(enumType.Name), IdentifierName(enumValue));
				return true;
			}
			case null:
			{
				result = LiteralExpression(SyntaxKind.NullLiteralExpression);
				return true;
			}
			case DateTime dt:
			{
				result = ObjectCreationExpression(
						IdentifierName("DateTime"))
					.WithArgumentList(ArgumentList(SeparatedList([
						Argument(CreateLiteral(dt.Ticks)!),
						Argument(
							MemberAccessExpression(
								IdentifierName("DateTimeKind"),
								IdentifierName(dt.Kind.ToString())))
					])));
				return true;
			}
			case TimeSpan ts:
			{
				result = ObjectCreationExpression(
						IdentifierName("TimeSpan"))
					.WithArgumentList(ArgumentList(SeparatedList([
						Argument(CreateLiteral(ts.Ticks)!)
					])));
				return true;
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
					var chars = (char[]) array;

					result = CreateLiteral(new string(chars));
					return true;
				}

				if (elemType == typeof(byte))
				{
					var bytes = (byte[]) array;
					var data = Encoding.UTF8.GetString(bytes);
					var escaped = data
						.Replace("\\", @"\\")
						.Replace("\"", "\\\"")
						.Replace("\r", "\\r")
						.Replace("\n", "\\n")
						.Replace("\t", "\\t");

					result = ParseExpression($"\"{escaped}\"u8");
					return true;
				}

				result = CollectionExpression(SeparatedList<CollectionElementSyntax>(array
					.Cast<object?>()
					.Select(s => ExpressionElement(CreateLiteral(s, true)))));
				return true;
			}
		}

		if (valueType!.Name.Contains("Tuple"))
		{
			var tupleItems = new List<ArgumentSyntax>();

			// Check for ValueTuple fields (Item1, Item2, etc.)
			var fields = valueType.GetFields().Where(f => f.Name.StartsWith("Item")).ToArray();

			if (fields.Length > 0)
			{
				foreach (var field in fields)
				{
					var itemValue = field.GetValue(value);

					if (!TryCreateLiteral(itemValue, out var itemExpr))
					{
						result = null;
						return false;
					}

					tupleItems.Add(Argument(itemExpr));
				}
			}
			else
			{
				// Check for Tuple properties (Item1, Item2, etc.)
				var properties = valueType.GetProperties().Where(p => p.Name.StartsWith("Item")).ToArray();

				foreach (var prop in properties)
				{
					var itemValue = prop.GetValue(value);

					if (!TryCreateLiteral(itemValue, out var itemExpr))
					{
						result = null;
						return false;
					}

					tupleItems.Add(Argument(itemExpr));
				}
			}

			result = TupleExpression(SeparatedList(tupleItems));
			return true;
		}

		// Support for IDictionary – emit new Dictionary<K, V> { { k, v }, ... }
		if (value is IDictionary dictionary)
		{
			var typeArgs = valueType.GetGenericArguments();
			var keyTypeSyntax = typeArgs.Length > 0 ? CreateTypeSyntax(typeArgs[0]) : PredefinedType(Token(SyntaxKind.ObjectKeyword));
			var valueTypeSyntax = typeArgs.Length > 1 ? CreateTypeSyntax(typeArgs[1]) : PredefinedType(Token(SyntaxKind.ObjectKeyword));

			var initializerExpressions = new List<ExpressionSyntax>();

			foreach (DictionaryEntry entry in dictionary)
			{
				if (!TryCreateLiteral(entry.Key, out var keyExpr)
				    || !TryCreateLiteral(entry.Value, out var valExpr))
				{
					result = null;
					return false;
				}

				initializerExpressions.Add(
					InitializerExpression(
						SyntaxKind.ComplexElementInitializerExpression,
						SeparatedList<ExpressionSyntax>([ keyExpr, valExpr ])));
			}

			var dictionaryType = GenericName(
					Identifier("Dictionary"))
				.WithTypeArgumentList(TypeArgumentList(
					SeparatedList<TypeSyntax>([
						keyTypeSyntax,
						valueTypeSyntax
					])));

			result = ObjectCreationExpression(dictionaryType, CreateLiteral(initializerExpressions.Count))
				.WithInitializer(InitializerExpression(
					SyntaxKind.CollectionInitializerExpression,
					SeparatedList(initializerExpressions)));
			return true;
		}

		if (value is IEnumerable enumerable)
		{
			var elements = new List<CollectionElementSyntax>();

			foreach (var item in enumerable.Cast<object?>())
			{
				if (!TryCreateLiteral(item, out var itemExpr))
				{
					result = null;
					return false;
				}

				elements.Add(ExpressionElement(itemExpr));
			}

			result = CollectionExpression(SeparatedList<CollectionElementSyntax>(elements));
			return true;
		}

		if (valueType.Name == "KeyValuePair`2")
		{
			var keyProperty = valueType.GetProperty("Key");
			var valueProperty = valueType.GetProperty("Value");
			
			if (!TryCreateLiteral(keyProperty.GetValue(value), out var keyExpr)
			    || !TryCreateLiteral(valueProperty.GetValue(value), out var valueExpr))
			{
				result = null;
				return false;
			}

			result = InvocationExpression(
					MemberAccessExpression(
						IdentifierName("KeyValuePair"),
						IdentifierName("Create")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(keyExpr),
					Argument(valueExpr)
				])));
			return true;
		}

		result = null;
		return false;
	}

	public static ExpressionSyntax CreateLiteral<T>(T? value, bool useExplicitByte = false)
	{
		if (!TryCreateLiteral(value, useExplicitByte, out var result))
		{
			throw new NotSupportedException($"Type {typeof(T)} is not supported for literal creation.");
		}

		return result;
	}

	public static TypeSyntax CreateTypeSyntax(Type type)
	{
		var keyword = type == typeof(bool) ? SyntaxKind.BoolKeyword
			: type == typeof(byte) ? SyntaxKind.ByteKeyword
			: type == typeof(sbyte) ? SyntaxKind.SByteKeyword
			: type == typeof(short) ? SyntaxKind.ShortKeyword
			: type == typeof(ushort) ? SyntaxKind.UShortKeyword
			: type == typeof(int) ? SyntaxKind.IntKeyword
			: type == typeof(uint) ? SyntaxKind.UIntKeyword
			: type == typeof(long) ? SyntaxKind.LongKeyword
			: type == typeof(ulong) ? SyntaxKind.ULongKeyword
			: type == typeof(float) ? SyntaxKind.FloatKeyword
			: type == typeof(double) ? SyntaxKind.DoubleKeyword
			: type == typeof(decimal) ? SyntaxKind.DecimalKeyword
			: type == typeof(char) ? SyntaxKind.CharKeyword
			: type == typeof(string) ? SyntaxKind.StringKeyword
			: type == typeof(object) ? SyntaxKind.ObjectKeyword
			: SyntaxKind.None;

		if (keyword != SyntaxKind.None)
		{
			return PredefinedType(Token(keyword));
		}

		return ParseTypeName(type.Name);
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
				case IdentifierNameSyntax identifier when variables is not null:
				{
					return variables.TryGetValue(identifier.Identifier.Text, out value);
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

	public static bool IsAttribute<TAttribute>(AttributeData? attribute)
		where TAttribute : Attribute
	{
		return attribute?.AttributeClass?.ToDisplayString() == typeof(TAttribute).FullName;
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
		return TryGetOperation(semanticModel, node, null, CancellationToken.None, out operation);
	}

	public static bool TryGetOperation<TOperation>(SemanticModel semanticModel, SyntaxNode? node, RoslynApiCache? cache, CancellationToken cancellationToken, [NotNullWhen(true)] out TOperation? operation) where TOperation : IOperation
	{
		try
		{
			var op = cache != null
				? cache.GetOrAddOperation(node, semanticModel, cancellationToken)
				: semanticModel.GetOperation(node, cancellationToken);

			if (op is TOperation typedOp)
			{
				operation = typedOp;
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

	public static bool IsInConstEvalBody(Compilation compilation, SyntaxNode node)
	{
		//var typeText = node.ToFullString();
		//var typeName = node.GetType();

		// Check if the node is part of a method or type with [ConstExpr] attribute
		switch (node)
		{
			case MethodDeclarationSyntax method:
				if (compilation.TryGetSemanticModel(method, out var model)
				    && model.GetDeclaredSymbol(method) is IMethodSymbol methodSymbol
				    && IsInConstEvalBody(methodSymbol))
				{
					return true;
				}

				break;
		}

		if (node.Parent is null)
		{
			return false;
		}

		return IsInConstEvalBody(compilation, node.Parent);
	}

	public static bool IsInConstExprBody(Compilation compilation, SyntaxNode node)
	{
		//var typeText = node.ToFullString();
		//var typeName = node.GetType();

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

	public static bool IsInConstEvalBody(ISymbol node)
	{
		if (node.GetAttributes().Any(IsAttribute<ConstEvalAttribute>))
		{
			return true;
		}

		if (node.ContainingSymbol is null)
		{
			return false;
		}

		return IsInConstEvalBody(node.ContainingSymbol);
	}

	public static bool IsInConstExprBody(ISymbol node)
	{
		if (node.GetAttributes().Any(IsAttribute<ConstExprAttribute>))
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

	/// <summary>
	/// Checks if the method containing the given invocation is actually invoked in the compilation.
	/// Uses recursive call graph analysis to trace through intermediate callers.
	/// Supports methods, local functions, and global statements.
	/// </summary>
	/// <param name="compilation">The compilation context</param>
	/// <param name="invocation">The invocation expression to check</param>
	/// <param name="cache">Optional cache for Roslyn API calls to improve performance</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>True if the containing method is invoked (directly or transitively), false otherwise</returns>
	public static bool IsContainingMethodInvoked(Compilation compilation, InvocationExpressionSyntax invocation, RoslynApiCache? cache = null, CancellationToken token = default)
	{
		// Check for global statement - always considered invoked (it's top-level code)
		var globalStatement = invocation.Ancestors().OfType<GlobalStatementSyntax>().FirstOrDefault();

		if (globalStatement != null)
		{
			return true;
		}

		// Find the containing method (could be a regular method, local function, etc.)
		var containingMethod = invocation.Ancestors()
			.OfType<BaseMethodDeclarationSyntax>()
			.FirstOrDefault();

		// Also check for LocalFunctionStatementSyntax
		var localFunction = containingMethod == null
			? invocation.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault()
			: null;

		if (containingMethod == null && localFunction == null)
		{
			// If not in a method or local function (e.g., in a field initializer, property initializer), consider it invoked
			return true;
		}

		SyntaxNode nodeToCheck = (SyntaxNode?) containingMethod ?? localFunction!;
		var tree = nodeToCheck.SyntaxTree;

		if (!compilation.SyntaxTrees.Contains(tree))
		{
			return true;
		}

		var semanticModel = compilation.GetSemanticModel(tree);

		if (semanticModel.GetDeclaredSymbol(nodeToCheck, token) is not IMethodSymbol methodSymbol)
		{
			return true;
		}

		// Check if this is a public API method (entry point)
		if (IsPublicApiMethod(methodSymbol))
		{
			return true;
		}

		// Use recursive call graph analysis with visited set to prevent infinite loops
		var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

		return IsMethodInvokedRecursive(compilation, methodSymbol, visited, cache, token);
	}

	public static bool HasIdentifier(this SyntaxNode node, string identifier)
	{
		return node.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Any(id => id.Identifier.Text == identifier);
	}

	public static TypeSyntax CreateTypeSyntax<T>()
	{
		return CreateTypeSyntax(typeof(T));
	}

	public static CastExpressionSyntax CreateCastSyntax<T>(ExpressionSyntax expression)
	{
		var type = typeof(T);

		return CastExpression(CreateTypeSyntax(type), expression);
	}

	public static BinaryExpressionSyntax EqualsExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.EqualsExpression, left, right);
	}

	public static BinaryExpressionSyntax NotEqualsExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.NotEqualsExpression, left, right);
	}

	public static BinaryExpressionSyntax AddExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.AddExpression, left, right);
	}

	public static BinaryExpressionSyntax SubtractExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.SubtractExpression, left, right);
	}

	public static BinaryExpressionSyntax MultiplyExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.MultiplyExpression, left, right);
	}

	public static BinaryExpressionSyntax DivideExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.DivideExpression, left, right);
	}

	public static BinaryExpressionSyntax ModuloExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.ModuloExpression, left, right);
	}

	public static BinaryExpressionSyntax GreaterThanExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.GreaterThanExpression, left, right);
	}

	public static BinaryExpressionSyntax GreaterThanOrEqualExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, left, right);
	}

	public static BinaryExpressionSyntax LessThanExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.LessThanExpression, left, right);
	}

	public static BinaryExpressionSyntax LessThanOrEqualExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.LessThanOrEqualExpression, left, right);
	}

	public static BinaryExpressionSyntax LogicalAndExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.LogicalAndExpression, left, right);
	}

	public static BinaryExpressionSyntax LogicalOrExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.LogicalOrExpression, left, right);
	}

	public static BinaryExpressionSyntax BitwiseAndExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.BitwiseAndExpression, left, right);
	}

	public static BinaryExpressionSyntax BitwiseOrExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.BitwiseOrExpression, left, right);
	}

	public static BinaryExpressionSyntax ExclusiveOrExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right);
	}

	public static BinaryExpressionSyntax LeftShiftExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.LeftShiftExpression, left, right);
	}

	public static BinaryExpressionSyntax RightShiftExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return BinaryExpression(SyntaxKind.RightShiftExpression, left, right);
	}

	public static PrefixUnaryExpressionSyntax UnaryMinusExpression(ExpressionSyntax operand)
	{
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, operand);
	}

	public static PrefixUnaryExpressionSyntax BitwiseNotExpression(ExpressionSyntax operand)
	{
		return PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, operand);
	}

	public static PrefixUnaryExpressionSyntax IndexFromEndExpression(ExpressionSyntax operand)
	{
		return PrefixUnaryExpression(SyntaxKind.IndexExpression, operand);
	}

	public static PostfixUnaryExpressionSyntax PostIncrementExpression(ExpressionSyntax operand)
	{
		return PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, operand);
	}

	public static PostfixUnaryExpressionSyntax PostDecrementExpression(ExpressionSyntax operand)
	{
		return PostfixUnaryExpression(SyntaxKind.PostDecrementExpression, operand);
	}

	public static MemberAccessExpressionSyntax MemberAccessExpression(ExpressionSyntax expression, SimpleNameSyntax name)
	{
		return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, name);
	}

	public static AssignmentExpressionSyntax AssignmentExpression(ExpressionSyntax left, SimpleNameSyntax name)
	{
		return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, name);
	}

	public static AssignmentExpressionSyntax AssignmentExpression(ExpressionSyntax left, ExpressionSyntax right)
	{
		return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);
	}

	public static ElementAccessExpressionSyntax ElementAccessExpression(ExpressionSyntax expression, params ExpressionSyntax[] arguments)
	{
		return SyntaxFactory.ElementAccessExpression(expression)
			.WithArgumentList(BracketedArgumentList(SeparatedList(arguments.Select(Argument))));
	}

	public static PrefixUnaryExpressionSyntax LogicalNotExpression(ExpressionSyntax operand)
	{
		return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand);
	}

	public static ObjectCreationExpressionSyntax ObjectCreationExpression(TypeSyntax type, params ExpressionSyntax[] arguments)
	{
		return SyntaxFactory.ObjectCreationExpression(type)
			.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
	}

	public static SyntaxNode ReplaceIdentifier(this SyntaxNode body, string oldIdentifier, ExpressionSyntax replacement)
	{
		var wrappedReplacement = replacement is BinaryExpressionSyntax or ConditionalExpressionSyntax
			? ParenthesizedExpression(replacement)
			: replacement;

		return new IdentifierReplacer(oldIdentifier, wrappedReplacement).Visit(body);
	}

	public static ExpressionSyntax InvertSyntax(this ExpressionSyntax node)
	{
		return node switch
		{
			// invert binary expressions with logical operators
			BinaryExpressionSyntax binary => binary.Kind() switch
			{
				SyntaxKind.LogicalAndExpression => LogicalOrExpression(InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.LogicalOrExpression => LogicalAndExpression(InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.EqualsExpression => NotEqualsExpression(binary.Left, binary.Right),
				SyntaxKind.NotEqualsExpression => EqualsExpression(binary.Left, binary.Right),
				SyntaxKind.GreaterThanExpression => LessThanOrEqualExpression(binary.Left, binary.Right),
				SyntaxKind.GreaterThanOrEqualExpression => LessThanExpression(binary.Left, binary.Right),
				SyntaxKind.LessThanExpression => GreaterThanOrEqualExpression(binary.Left, binary.Right),
				SyntaxKind.LessThanOrEqualExpression => GreaterThanExpression(binary.Left, binary.Right),
				_ => LogicalNotExpression(ParenthesizedExpression(node))
			},
			PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.IsKind(SyntaxKind.LogicalNotExpression) => prefixUnary.Operand,
			// handle 'x is T' (pattern form) and 'x is not T'
			// x is not T  →  x is T  (strip the negation)
			IsPatternExpressionSyntax isPattern when isPattern.Pattern.Kind() == SyntaxKind.NotPattern && isPattern.Pattern is UnaryPatternSyntax negated => IsPatternExpression(isPattern.Expression, negated.Pattern),
			// x is T  →  x is not T  (add negation)
			IsPatternExpressionSyntax isPattern => IsPatternExpression(isPattern.Expression, UnaryPattern(Token(SyntaxKind.NotKeyword), isPattern.Pattern)),
			InvocationExpressionSyntax or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax => LogicalNotExpression(node),
			_ => LogicalNotExpression(ParenthesizedExpression(node))
		};
	}

	/// <summary>
	/// Recursively checks if a method is invoked, following the call chain up to entry points.
	/// </summary>
	private static bool IsMethodInvokedRecursive(Compilation compilation, IMethodSymbol targetMethod, HashSet<IMethodSymbol> visited, RoslynApiCache? cache, CancellationToken token)
	{
		// Prevent infinite recursion
		if (!visited.Add(targetMethod))
		{
			return false;
		}

		// Find all methods that call the target method
		var callers = FindCallingMethods(compilation, targetMethod, cache, token);

		foreach (var caller in callers)
		{
			if (token.IsCancellationRequested)
			{
				return true; // Assume invoked if cancelled
			}

			// If the caller is a public API method, the target is invoked
			if (IsPublicApiMethod(caller))
			{
				return true;
			}

			// Recursively check if the caller is invoked
			if (IsMethodInvokedRecursive(compilation, caller, visited, cache, token))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Finds all methods in the compilation that invoke the target method.
	/// Checks regular methods, local functions, and global statements.
	/// Uses RoslynApiCache to avoid expensive repeated GetSymbolInfo calls.
	/// </summary>
	private static IEnumerable<IMethodSymbol> FindCallingMethods(Compilation compilation, IMethodSymbol targetMethod, RoslynApiCache? cache, CancellationToken token)
	{
		var callers = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

		foreach (var tree in compilation.SyntaxTrees)
		{
			if (token.IsCancellationRequested)
			{
				yield break;
			}

			if (!compilation.SyntaxTrees.Contains(tree))
			{
				continue;
			}

			var semanticModel = compilation.GetSemanticModel(tree);
			var root = tree.GetRoot(token);

			// Find all invocations in this tree
			var invocations = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>();

			foreach (var invocation in invocations)
			{
				if (token.IsCancellationRequested)
				{
					yield break;
				}

				// Check if this invocation calls our target method - USE CACHE HERE
				SymbolInfo symbolInfo = cache != null
					? cache.GetOrAddSymbolInfo(invocation, semanticModel, token)
					: semanticModel.GetSymbolInfo(invocation, token);

				if (symbolInfo.Symbol is IMethodSymbol symbol &&
				    (SymbolEqualityComparer.Default.Equals(symbol, targetMethod) ||
				     (symbol.OriginalDefinition != null &&
				      SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, targetMethod.OriginalDefinition))))
				{
					// Check if invocation is in a global statement - if so, it's always invoked
					var globalStatement = invocation.Ancestors().OfType<GlobalStatementSyntax>().FirstOrDefault();

					if (globalStatement != null)
					{
						// Global statements are top-level code, so we treat them as entry points
						// Return a synthetic "Main" method to represent the global scope
						var entryPoint = compilation.GetEntryPoint(token);

						if (entryPoint != null && callers.Add(entryPoint))
						{
							yield return entryPoint;
						}
						continue;
					}

					// Find the containing method or local function of this invocation
					var containingMethod = invocation.Ancestors()
						.OfType<BaseMethodDeclarationSyntax>()
						.FirstOrDefault();

					var localFunction = containingMethod == null
						? invocation.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault()
						: null;

					var nodeToCheck = (SyntaxNode?) containingMethod ?? localFunction;

					if (nodeToCheck != null)
					{
						if (semanticModel.GetDeclaredSymbol(nodeToCheck, token) is IMethodSymbol callingMethod && callers.Add(callingMethod))
						{
							yield return callingMethod;
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Checks if a method is a public API method (likely to be called from outside)
	/// </summary>
	private static bool IsPublicApiMethod(IMethodSymbol method)
	{
		// Public methods in public types are considered entry points
		if (method.DeclaredAccessibility == Accessibility.Public)
		{
			var containingType = method.ContainingType;

			while (containingType != null)
			{
				if (containingType.DeclaredAccessibility != Accessibility.Public)
				{
					return false;
				}
				containingType = containingType.ContainingType;
			}
			return true;
		}

		// Special entry points (Main, test methods with [Test], [Fact], etc.)
		if (method is { Name: "Main", IsStatic: true })
		{
			return true;
		}

		// Entry point for global statements (synthesized Main method)
		if (method.Name == "<Main>$")
		{
			return true;
		}

		// Test methods
		var testAttributes = new[] { "Test", "TestMethod", "Fact", "Theory" };

		if (method.GetAttributes().Any(attr => testAttributes.Any(ta => attr.AttributeClass?.Name.Contains(ta) == true)))
		{
			return true;
		}

		return false;
	}

	private sealed class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			return node.Identifier.Text == identifier ? replacement : base.VisitIdentifierName(node);
		}
	}
}