using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

public class BaseRewriter(SemanticModel semanticModel, MetadataLoader loader, IDictionary<string, VariableItem> variables) : CSharpSyntaxRewriter
{
	protected readonly SemanticModel semanticModel = semanticModel;
	protected readonly MetadataLoader loader = loader;
	protected readonly IDictionary<string, VariableItem> variables = variables;

	protected bool TryGetLiteralValue(SyntaxNode? node, [NotNullWhen(true)] out object? value)
	{
		return TryGetLiteralValue(node, null, out value, new HashSet<string>());
	}

	private bool TryGetLiteralValue(SyntaxNode? node, ITypeSymbol? typeSymbol, [NotNullWhen(true)] out object? value, HashSet<string> visitedVariables)
	{
		switch (node)
		{
			case LiteralExpressionSyntax { Token.Value: var v }:
				value = v;
				return true;
			case ArgumentSyntax argumentSyntax:
				return TryGetLiteralValue(argumentSyntax.Expression, typeSymbol, out value, visitedVariables);
			case ExpressionElementSyntax elementSyntax:
				return TryGetLiteralValue(elementSyntax.Expression, typeSymbol, out value, visitedVariables);
			case IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue:
				// Prevent infinite recursion from circular variable references
				if (!visitedVariables.Add(identifier.Identifier.Text))
				{
					value = null;
					return false;
				}

				if (variable.Value is SyntaxNode sn)
				{
					return TryGetLiteralValue(sn, variable.Type, out value, visitedVariables);
				}

				value = variable.Value;
				return true;
			// unwrap ( ... )
			case ParenthesizedExpressionSyntax paren:
				return TryGetLiteralValue(paren.Expression, typeSymbol, out value, visitedVariables) || TryGetLiteralValue(Visit(paren.Expression), typeSymbol, out value, visitedVariables);
			// ^n => System.Index(n, fromEnd: true)
			case PrefixUnaryExpressionSyntax prefix when prefix.OperatorToken.IsKind(SyntaxKind.CaretToken):
				{
					if (TryGetLiteralValue(prefix.Operand, typeSymbol, out var inner, visitedVariables) && inner is not null)
					{
						try
						{
							var indexType = loader.GetType("System.Index");

							var ctor = indexType?.GetConstructor([typeof(int), typeof(bool)]);

							if (ctor != null)
							{
								var intVal = Convert.ToInt32(inner);

								value = ctor.Invoke([intVal, true]);
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
						var indexType = loader.GetType("System.Index");
						var rangeType = loader.GetType("System.Range");

						if (indexType is null || rangeType is null)
						{
							value = null;
							return false;
						}

						object? MakeIndex(ExpressionSyntax expr)
						{
							if (TryGetLiteralValue(Visit(expr), typeSymbol, out var innerVal, visitedVariables) && innerVal is not null)
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
									var ctor2 = indexType.GetConstructor([typeof(int), typeof(bool)]);
									var ctor1 = indexType.GetConstructor([typeof(int)]);
									if (ctor2 is not null) return ctor2.Invoke([intVal, false]);
									if (ctor1 is not null) return ctor1.Invoke([intVal]);
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
							var startAt = rangeType.GetMethod("StartAt", BindingFlags.Public | BindingFlags.Static, null, [indexType], null);
							value = startAt?.Invoke(null, [leftIdx]);
							return value is not null;
						}

						if (leftIdx is null && rightIdx is not null)
						{
							var endAt = rangeType.GetMethod("EndAt", BindingFlags.Public | BindingFlags.Static, null, [indexType], null);
							value = endAt?.Invoke(null, [rightIdx]);
							return value is not null;
						}

						var ctorRange = rangeType.GetConstructor([indexType, indexType]);
						value = ctorRange?.Invoke([leftIdx, rightIdx]);
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
					if (semanticModel.TryGetSymbol(objectCreationExpression.Type, out ITypeSymbol? randomType)
							&& randomType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
					{
						value = null;
						return false;
					}

					var arguments = objectCreationExpression.ArgumentList?.Arguments
														.Select(s => Visit(s.Expression))
														.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
													?? Enumerable.Empty<object?>();

					if (semanticModel.TryGetSymbol(objectCreationExpression, out IMethodSymbol? constructor)
							&& loader.TryExecuteMethod(constructor, null, null, arguments, out var result))
					{
						value = result;
						return true;
					}

					if (semanticModel.TryGetSymbol(objectCreationExpression.Type, out typeSymbol))
					{
						var type = loader.GetType(typeSymbol);

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
					var fallbackType = loader.GetType(typeName) ?? loader.GetType($"System.{typeName}");

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
					if (semanticModel.TryGetSymbol(lambda, out IMethodSymbol symbol))
					{
						var parameters = symbol.Parameters
							.Select(p => Expression.Parameter(loader.GetType(p.Type), p.Name))
							.ToDictionary(t => t.Name);

						var rewriter = new ExpressionRewriter(semanticModel, loader, (_, _) => { }, variables, parameters, CancellationToken.None);
						var body = rewriter.Visit(lambda.Body);

						value = Expression.Lambda(body, parameters.Values).Compile();
						return true;
					}

					break;
				}
			case ParenthesizedLambdaExpressionSyntax parenthesizedLambdaExpressionSyntax:
				{
					if (semanticModel.TryGetSymbol(parenthesizedLambdaExpressionSyntax, out IMethodSymbol symbol))
					{
						var parameters = symbol.Parameters
							.Select(p => Expression.Parameter(loader.GetType(p.Type), p.Name))
							.ToDictionary(t => t.Name);

						var rewriter = new ExpressionRewriter(semanticModel, loader, (_, _) => { }, variables, parameters, CancellationToken.None);
						var body = rewriter.Visit(parenthesizedLambdaExpressionSyntax.Body);

						value = Expression.Lambda(body, parameters.Values).Compile();
						return true;
					}

					break;
				}
			case CastExpressionSyntax castExpressionSyntax:
				{
					if (TryGetLiteralValue(castExpressionSyntax.Expression, typeSymbol, out var innerVal, visitedVariables))
					{
						// Try to resolve the *textual* type name from the syntax node (no semantic model)
						string typeName = castExpressionSyntax.Type switch
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
							typeName = typeName.Substring("System.".Length);

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
					if (semanticModel.TryGetSymbol(memberAccessExpressionSyntax, out ISymbol? symbol))
					{
						var parentType = symbol.ContainingType;

						TryGetLiteralValue(Visit(memberAccessExpressionSyntax.Expression), parentType, out var instanceValue, visitedVariables);

						switch (symbol)
						{
							case IFieldSymbol fieldSymbol:
								if (loader.TryGetFieldValue(fieldSymbol, instanceValue, out value))
								{
									return true;
								}
								break;
							case IPropertySymbol propertySymbol:
								if (propertySymbol.Parameters.Length == 0)
								{
									if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [], out value))
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
								if (TryGetLiteralValue(Visit(size), typeSymbol, out var dimValue, visitedVariables) && dimValue is not null)
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
							if (semanticModel.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
							{
								var elementType = loader.GetType(elementTypeSymbol);

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
							if (TryGetLiteralValue(rankSpecifier.Sizes[0], typeSymbol, out var sizeVal, visitedVariables) && sizeVal is not null)
							{
								try
								{
									var arraySize = Convert.ToInt32(sizeVal);

									if (semanticModel.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
									{
										var elementType = loader.GetType(elementTypeSymbol);

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
							if (TryGetLiteralValue(element, typeSymbol, out var elemVal, visitedVariables))
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
							if (semanticModel.TryGetSymbol(arrayCreationExpression.Type.ElementType, out ITypeSymbol? elementTypeSymbol))
							{
								var elementType = loader.GetType(elementTypeSymbol);

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
			case CollectionExpressionSyntax collectionExpressionSyntax:
				{
					var elements = new List<object?>();

					foreach (var element in collectionExpressionSyntax.Elements.OfType<ExpressionElementSyntax>())
					{
						if (TryGetLiteralValue(element.Expression, typeSymbol, out var elemVal, visitedVariables))
						{
							elements.Add(elemVal);
						}
						else
						{
							value = null;
							return false;
						}
					}

					var type = elements.Count > 0 ? elements[0].GetType() : loader.GetType(typeSymbol);
					var array = Array.CreateInstance(type, elements.Count);

					for (var i = 0; i < elements.Count; i++)
					{
						array.SetValue(Convert.ChangeType(elements[i], type), i);
					}

					value = array;
					return true;
				}
			case TupleExpressionSyntax tupleExpressionSyntax:
				{
					var elements = new List<object?>();

					foreach (var element in tupleExpressionSyntax.Arguments)
					{
						if (TryGetLiteralValue(element.Expression, typeSymbol, out var elemVal, visitedVariables))
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
					var tupleType = loader.GetTupleType(tupleTypes.Length);

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

	/// <summary>
	/// Checks if a variable has a known constant value that can be inlined.
	/// Used for constant folding decisions during evaluation.
	/// </summary>
	protected bool CanBePruned(string variableName)
	{
		if (!variables.TryGetValue(variableName, out var variable) || !variable.HasValue || variable.IsAltered)
		{
			return false;
		}

		// Check if the value is a simple constant that can be inlined
		return variable.Value switch
		{
			SyntaxNode syntaxNode => syntaxNode switch
			{
				LiteralExpressionSyntax => true,
				IdentifierNameSyntax id => variables.TryGetValue(id.Identifier.Text, out var nested) && !nested.IsAltered,
				_ => false
			},
			// Non-SyntaxNode values are true constants (string, int, etc.)
			_ => true
		};
	}

	protected bool CanBePruned(SyntaxNode node)
	{
		return node is IdentifierNameSyntax identifier && CanBePruned(identifier.Identifier.Text);
	}
	
	protected bool TryGetTypeSymbol(SyntaxNode? node, [NotNullWhen(true)] out ITypeSymbol? typeSymbol)
	{
		switch (node)
		{
			case null:
				typeSymbol = null;
				return false;
			case IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var value):
				typeSymbol = value.Type;
				return true;
		}

		var typeInfo = semanticModel.GetTypeInfo(node);
		typeSymbol = typeInfo.Type ?? typeInfo.ConvertedType;

		return typeSymbol is not null;
	}
	
	protected IEnumerable<BinaryExpressionSyntax>  GetBinaryExpressions(SyntaxNode node)
	{
		return node.Ancestors()
			.Where(w => w is BinaryExpressionSyntax or WhileStatementSyntax or IfStatementSyntax)
			.Select(s => s switch
			{
				BinaryExpressionSyntax binary => binary,
				WhileStatementSyntax whileStmt => whileStmt.Condition,
				IfStatementSyntax ifStmt => ifStmt.Condition,
				ForStatementSyntax forStmt => forStmt.Condition,
				_ => null
			})
			.OfType<BinaryExpressionSyntax>()
			.SelectMany(GetInnerBinaryExpressions);
		
		IEnumerable<BinaryExpressionSyntax> GetInnerBinaryExpressions(ExpressionSyntax expr)
		{
			if (expr is BinaryExpressionSyntax binary)
			{
				yield return binary;

				foreach (var inner in GetInnerBinaryExpressions(binary.Left))
				{
					yield return inner;
				}

				foreach (var inner in GetInnerBinaryExpressions(binary.Right))
				{
					yield return inner;
				}
			}
		}
	}
}