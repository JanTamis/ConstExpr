using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers
{
	public abstract class BaseFunctionOptimizer
	{
		public abstract bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result);

		protected InvocationExpressionSyntax CreateInvocation(ITypeSymbol type, string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return InvocationExpression(
					MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						ParseTypeName(type.Name),
						IdentifierName(name)))
				.WithArgumentList(
					ArgumentList(
						SeparatedList(
							parameters.Select(Argument))));
		}

		protected InvocationExpressionSyntax CreateInvocation(string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return InvocationExpression(
					IdentifierName(name))
				.WithArgumentList(
					ArgumentList(
						SeparatedList(
							parameters.Select(Argument))));
		}

		protected static bool IsPure(SyntaxNode node)
		{
			return node switch
			{
				IdentifierNameSyntax => true,
				LiteralExpressionSyntax => true,
				ParenthesizedExpressionSyntax par => IsPure(par.Expression),
				PrefixUnaryExpressionSyntax u => IsPure(u.Operand),
				BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
				_ => false
			};
		}

		protected static MethodDeclarationSyntax ParseMethodFromString(string methodString)
		{
			var wrappedCode = $$"""
				public class TempClass
				{
					{{methodString}}
				}
				""";

			var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

			return syntaxTree.GetRoot()
				.DescendantNodes()
				.OfType<MethodDeclarationSyntax>()
				.First();
		}

		protected bool TryGetLiteralValue([NotNullWhen(true)] SyntaxNode? node, FunctionOptimizerContext context, ITypeSymbol? typeSymbol, out object? value)
		{
			return TryGetLiteralValue(node, context, typeSymbol, out value, [ ]);
		}

		protected bool TryGetLiteralValue([NotNullWhen(true)] SyntaxNode? node, FunctionOptimizerContext context, ITypeSymbol? typeSymbol, out object? value, HashSet<string> visitedVariables)
		{
			switch (node)
			{
				case LiteralExpressionSyntax { Token.Value: var v }:
					value = v;
					return true;
				case ArgumentSyntax argumentSyntax:
					return TryGetLiteralValue(argumentSyntax.Expression, context, typeSymbol, out value, visitedVariables);
				case ExpressionElementSyntax elementSyntax:
					return TryGetLiteralValue(elementSyntax.Expression, context, typeSymbol, out value, visitedVariables);
				case IdentifierNameSyntax identifier when context.Variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue:
					// Prevent infinite recursion from circular variable references
					if (!visitedVariables.Add(identifier.Identifier.Text))
					{
						value = null;
						return false;
					}

					if (variable.Value is SyntaxNode sn)
					{
						return TryGetLiteralValue(sn, context, variable.Type, out value, visitedVariables);
					}

					value = variable.Value;
					return true;
				// unwrap ( ... )
				case ParenthesizedExpressionSyntax paren:
					return TryGetLiteralValue(paren.Expression, context, typeSymbol, out value, visitedVariables) || TryGetLiteralValue(context.Visit(paren.Expression), context, typeSymbol, out value, visitedVariables);
				// ^n => System.Index(n, fromEnd: true)
				case PrefixUnaryExpressionSyntax prefix when prefix.OperatorToken.IsKind(SyntaxKind.CaretToken):
				{
					if (TryGetLiteralValue(prefix.Operand, context, typeSymbol, out var inner, visitedVariables) && inner is not null)
					{
						try
						{
							var indexType = context.Loader.GetType("System.Index");

							var ctor = indexType?.GetConstructor([ typeof(int), typeof(bool) ]);

							if (ctor != null)
							{
								var intVal = Convert.ToInt32(inner);

								value = ctor.Invoke([ intVal, true ]);
								return true;
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
						var indexType = context.Loader.GetType("System.Index");
						var rangeType = context.Loader.GetType("System.Range");

						if (indexType is null || rangeType is null)
						{
							value = null;
							return false;
						}

						object? MakeIndex(ExpressionSyntax expr)
						{
							if (TryGetLiteralValue(context.Visit(expr), context, typeSymbol, out var innerVal, visitedVariables) && innerVal is not null)
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

									if (ctor2 is not null)
									{
										return ctor2.Invoke([ intVal, false ]);
									}

									if (ctor1 is not null)
									{
										return ctor1.Invoke([ intVal ]);
									}
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
				case ObjectCreationExpressionSyntax objectCreationExpression:
				{
					if (context.Model.TryGetSymbol(objectCreationExpression.Type, out ITypeSymbol? randomType)
					    && randomType.EqualsType(context.Model.Compilation.GetTypeByMetadataName("System.Random")))
					{
						value = null;
						return false;
					}

					var arguments = objectCreationExpression.ArgumentList?.Arguments
						                .Select(s => context.Visit(s.Expression))
						                .WhereSelect<SyntaxNode, object?>((syntaxNode, out o) => TryGetLiteralValue(syntaxNode, context, typeSymbol, out o, visitedVariables))
					                ?? Enumerable.Empty<object?>();

					if (context.Model.TryGetSymbol(objectCreationExpression, out IMethodSymbol? constructor)
					    && context.Loader.TryExecuteMethod(constructor, null, null, arguments, out var result))
					{
						value = result;
						return true;
					}

					if (context.Model.TryGetSymbol(objectCreationExpression.Type, out typeSymbol))
					{
						var type = context.Loader.GetType(typeSymbol);

						if (type != null)
						{
							var argumentsArray = arguments.ToArray();
							var ctorInfos = type.GetConstructors()
								.Where(c => c.GetParameters().Length == argumentsArray.Length)
								.ToList();

							foreach (var ctorInfo in ctorInfos)
							{
								try
								{
									value = ctorInfo.Invoke(argumentsArray);
									return true;
								}
								catch
								{
									// Try next
								}
							}
						}
					}

					// Fallback for SyntaxFactory-created nodes without semantic binding
					var typeName = objectCreationExpression.Type.ToString();
					var fallbackType = context.Loader.GetType(typeName) ?? context.Loader.GetType($"System.{typeName}");

					if (fallbackType != null)
					{
						var argumentsArray = arguments.ToArray();
						var ctorInfos = fallbackType.GetConstructors()
							.Where(c => c.GetParameters().Length == argumentsArray.Length)
							.ToList();

						foreach (var ctorInfo in ctorInfos)
						{
							try
							{
								value = ctorInfo.Invoke(argumentsArray);
								return true;
							}
							catch
							{
								// Try next
							}
						}
					}

					break;
				}
				case SimpleLambdaExpressionSyntax lambda:
				{
					if (context.Model.TryGetSymbol(lambda, out IMethodSymbol symbol))
					{
						var parameters = symbol.Parameters
							.Select(p => Expression.Parameter(context.Loader.GetType(p.Type), p.Name))
							.ToDictionary(t => t.Name);

						var rewriter = new ExpressionRewriter(context.Model, context.Loader, (_, _) => { }, context.Variables, parameters, CancellationToken.None);
						var body = rewriter.Visit(lambda.Body);

						if (body is null)
						{
							value = null;
							return false;
						}

						value = Expression.Lambda(body, parameters.Values).Compile();
						return true;
					}

					break;
				}
				case ParenthesizedLambdaExpressionSyntax parenthesizedLambdaExpressionSyntax:
				{
					if (context.Model.TryGetSymbol(parenthesizedLambdaExpressionSyntax, out IMethodSymbol symbol))
					{
						var parameters = symbol.Parameters
							.Select(p => Expression.Parameter(context.Loader.GetType(p.Type), p.Name))
							.ToDictionary(t => t.Name);

						var rewriter = new ExpressionRewriter(context.Model, context.Loader, (_, _) => { }, context.Variables, parameters, CancellationToken.None);
						var body = rewriter.Visit(parenthesizedLambdaExpressionSyntax.Body);

						value = Expression.Lambda(body, parameters.Values).Compile();
						return true;
					}

					break;
				}
				case CastExpressionSyntax castExpressionSyntax:
				{
					if (TryGetLiteralValue(castExpressionSyntax.Expression, context, typeSymbol, out var innerVal, visitedVariables))
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
						{
							typeName = typeName.Substring("System.".Length);
						}

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
							_ => innerVal
						};

						return true;
					}
					break;
				}
				case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
				{
					if (context.Model.TryGetSymbol(memberAccessExpressionSyntax, out ISymbol? symbol))
					{
						var parentType = symbol.ContainingType;

						TryGetLiteralValue(context.Visit(memberAccessExpressionSyntax.Expression), context, parentType, out var instanceValue, visitedVariables);

						switch (symbol)
						{
							case IFieldSymbol fieldSymbol:
								if (context.Loader.TryGetFieldValue(fieldSymbol, instanceValue, out value))
								{
									return true;
								}
								break;
							case IPropertySymbol propertySymbol:
								if (propertySymbol.Parameters.Length == 0)
								{
									if (context.Loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(context.Variables), [ ], out value))
									{
										return true;
									}
								}
								break;
						}
					}

					break;
				}
				case ArrayCreationExpressionSyntax arrayCreationExpression:
				{
					// Handle multidimensional arrays like new int[2,3] or new int[2,3,4]
					if (arrayCreationExpression.Initializer is null && arrayCreationExpression.Type.RankSpecifiers.Count > 0)
					{
						var rankSpecifier = arrayCreationExpression.Type.RankSpecifiers[0];
						var dimensions = rankSpecifier.Sizes.Count;

						if (dimensions > 1)
						{
							// Multidimensional array
							var dimensionLengths = new List<int>();

							foreach (var size in rankSpecifier.Sizes)
							{
								if (TryGetLiteralValue(context.Visit(size), context, typeSymbol, out var dimValue, visitedVariables) && dimValue is not null)
								{
									try
									{
										dimensionLengths.Add(Convert.ToInt32(dimValue));
									}
									catch
									{
										value = null;
										return false;
									}
								}
								else
								{
									value = null;
									return false;
								}
							}

							// Create multidimensional array
							if (context.Model.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
							{
								var elementType = context.Loader.GetType(elementTypeSymbol);

								if (elementType is not null)
								{
									try
									{
										value = Array.CreateInstance(elementType, dimensionLengths.ToArray());
										return true;
									}
									catch
									{
										value = null;
										return false;
									}
								}
							}
						}
						else if (dimensions == 1)
						{
							// Single-dimensional array with explicit size
							if (TryGetLiteralValue(rankSpecifier.Sizes[0], context, typeSymbol, out var sizeVal, visitedVariables) && sizeVal is not null)
							{
								try
								{
									var arraySize = Convert.ToInt32(sizeVal);

									if (context.Model.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
									{
										var elementType = context.Loader.GetType(elementTypeSymbol);

										if (elementType is not null)
										{
											value = Array.CreateInstance(elementType, arraySize);
											return true;
										}
									}
								}
								catch
								{
									value = null;
									return false;
								}
							}
						}
					}

					// Handle array initialization like new int[] { 1, 2, 3 } or new[] { 1, 2, 3 }
					if (arrayCreationExpression.Initializer is not null)
					{
						var elements = new List<object?>();

						foreach (var element in arrayCreationExpression.Initializer.Expressions)
						{
							if (TryGetLiteralValue(element, context, typeSymbol, out var elemVal, visitedVariables))
							{
								elements.Add(elemVal);
							}
							else
							{
								value = null;
								return false;
							}
						}

						if (elements.Count == 0)
						{
							// Empty array
							if (context.Model.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
							{
								var elementType = context.Loader.GetType(elementTypeSymbol);

								if (elementType is not null)
								{
									value = Array.CreateInstance(elementType, 0);
									return true;
								}
							}
						}
						else
						{
							var elementType = elements[0]?.GetType() ?? typeof(object);
							var array = Array.CreateInstance(elementType, elements.Count);

							for (var i = 0; i < elements.Count; i++)
							{
								array.SetValue(Convert.ChangeType(elements[i], elementType), i);
							}

							value = array;
							return true;
						}
					}

					break;
				}
				case ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpressionSyntax:
				{
					var elements = new List<object?>();

					foreach (var element in implicitArrayCreationExpressionSyntax.Initializer.Expressions)
					{
						if (TryGetLiteralValue(element, context, typeSymbol, out var elemVal, visitedVariables))
						{
							elements.Add(elemVal);
						}
						else
						{
							value = null;
							return false;
						}
					}

					// Determine the element type: prefer the type argument from the target typeSymbol,
					// then fall back to the runtime type of the first element.
					Type? elementType = null;

					if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedCollectionType && namedCollectionType.TypeArguments.Length > 0)
					{
						elementType = context.Loader.GetType(namedCollectionType.TypeArguments[0]);
					}

					elementType ??= elements.Count > 0 ? elements[0]?.GetType() : null;
					elementType ??= context.Loader.GetType(typeSymbol) ?? typeof(object);

					// Resolve the concrete runtime type to instantiate.
					// For interfaces, map to a known concrete generic; for concrete types use them directly.
					Type? resolvedType = null;

					if (typeSymbol is INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType)
					{
						var concreteGeneric = interfaceType.MetadataName switch
						{
							"ISet`1" or "IReadOnlySet`1" => typeof(HashSet<>),
							"ICollection`1" or "IList`1" or "IReadOnlyCollection`1" or "IReadOnlyList`1" => typeof(List<>),
							_ => null
						};

						if (concreteGeneric != null)
						{
							resolvedType = concreteGeneric.MakeGenericType(elementType);
						}
					}
					else if (typeSymbol?.TypeKind is not TypeKind.Array)
					{
						resolvedType = context.Loader.GetType(typeSymbol);
					}

					// If the resolved type has a parameterless constructor and an Add method, use it.
					if (resolvedType != null && resolvedType.GetConstructor(Type.EmptyTypes) != null)
					{
						var addMethod = resolvedType.GetMethod("Add", [ elementType ])
						                ?? resolvedType.GetMethod("Add");

						if (addMethod != null)
						{
							var instance = Activator.CreateInstance(resolvedType);

							foreach (var elem in elements)
							{
								addMethod.Invoke(instance, [ elem is IConvertible ? Convert.ChangeType(elem, elementType) : elem ]);
							}

							value = instance;
							return true;
						}
					}

					// Default: create an array (covers T[], IEnumerable<T>, and unknown types).
					var array = Array.CreateInstance(elementType, elements.Count);

					for (var i = 0; i < elements.Count; i++)
					{
						array.SetValue(elements[i] is IConvertible ? Convert.ChangeType(elements[i], elementType) : elements[i], i);
					}

					value = array;
					return true;
				}
				case CollectionExpressionSyntax collectionExpressionSyntax:
				{
					var elements = new List<object?>();

					foreach (var element in collectionExpressionSyntax.Elements.OfType<ExpressionElementSyntax>())
					{
						if (TryGetLiteralValue(element.Expression, context, typeSymbol, out var elemVal, visitedVariables))
						{
							elements.Add(elemVal);
						}
						else
						{
							value = null;
							return false;
						}
					}

					// Determine the element type: prefer the type argument from the target typeSymbol,
					// then fall back to the runtime type of the first element.
					Type? elementType = null;

					if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedCollectionType && namedCollectionType.TypeArguments.Length > 0)
					{
						elementType = context.Loader.GetType(namedCollectionType.TypeArguments[0]);
					}

					elementType ??= elements.Count > 0 ? elements[0]?.GetType() : null;
					elementType ??= context.Loader.GetType(typeSymbol) ?? typeof(object);

					// Resolve the concrete runtime type to instantiate.
					// For interfaces, map to a known concrete generic; for concrete types use them directly.
					Type? resolvedType = null;

					if (typeSymbol is INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType)
					{
						var concreteGeneric = interfaceType.MetadataName switch
						{
							"ISet`1" or "IReadOnlySet`1" => typeof(HashSet<>),
							"ICollection`1" or "IList`1" or "IReadOnlyCollection`1" or "IReadOnlyList`1" => typeof(List<>),
							_ => null
						};

						if (concreteGeneric != null)
						{
							resolvedType = concreteGeneric.MakeGenericType(elementType);
						}
					}
					else if (typeSymbol?.TypeKind is not TypeKind.Array)
					{
						resolvedType = context.Loader.GetType(typeSymbol);
					}

					// If the resolved type has a parameterless constructor and an Add method, use it.
					if (resolvedType != null && resolvedType.GetConstructor(Type.EmptyTypes) != null)
					{
						var addMethod = resolvedType.GetMethod("Add", [ elementType ])
						                ?? resolvedType.GetMethod("Add");

						if (addMethod != null)
						{
							var instance = Activator.CreateInstance(resolvedType);

							foreach (var elem in elements)
							{
								addMethod.Invoke(instance, [ elem is IConvertible ? Convert.ChangeType(elem, elementType) : elem ]);
							}

							value = instance;
							return true;
						}
					}

					// Default: create an array (covers T[], IEnumerable<T>, and unknown types).
					var array = Array.CreateInstance(elementType, elements.Count);

					for (var i = 0; i < elements.Count; i++)
					{
						array.SetValue(elements[i] is IConvertible ? Convert.ChangeType(elements[i], elementType) : elements[i], i);
					}

					value = array;
					return true;
				}
				case TupleExpressionSyntax tupleExpressionSyntax:
				{
					var elements = new List<object?>();

					foreach (var element in tupleExpressionSyntax.Arguments)
					{
						if (TryGetLiteralValue(element.Expression, context, typeSymbol, out var elemVal, visitedVariables))
						{
							elements.Add(elemVal);
						}
						else
						{
							value = null;
							return false;
						}
					}

					var tupleTypes = elements.Select(e => e?.GetType() ?? typeof(object)).ToArray();
					var tupleType = context.Loader.GetTupleType(tupleTypes.Length);

					if (tupleType != null)
					{
						var genericTupleType = tupleType.MakeGenericType(tupleTypes);
						var ctor = genericTupleType.GetConstructor(tupleTypes);

						if (ctor != null)
						{
							value = ctor.Invoke(elements.ToArray());
							return true;
						}
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
	}
}