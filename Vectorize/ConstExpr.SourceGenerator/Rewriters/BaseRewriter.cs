using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

public class BaseRewriter(SemanticModel semanticModel, MetadataLoader loader, IDictionary<string, VariableItem> variables) : CSharpSyntaxRewriter
{
	protected bool TryGetLiteralValue(SyntaxNode? node, out object? value)
	{
		switch (node)
		{
			case LiteralExpressionSyntax { Token.Value: var v }:
				value = v;
				return true;
			case IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue:
				if (variable.Value is SyntaxNode sn)
				{
					return TryGetLiteralValue(sn, out value);
				}

				value = variable.Value;
				return true;
			// unwrap ( ... )
			case ParenthesizedExpressionSyntax paren:
				return TryGetLiteralValue(paren.Expression, out value);
			// ^n => System.Index(n, fromEnd: true)
			case PrefixUnaryExpressionSyntax prefix when prefix.OperatorToken.IsKind(SyntaxKind.CaretToken):
			{
				if (TryGetLiteralValue(prefix.Operand, out var inner) && inner is not null)
				{
					try
					{
						var indexType = loader.GetType("System.Index");

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
					var indexType = loader.GetType("System.Index");
					var rangeType = loader.GetType("System.Range");

					if (indexType is null || rangeType is null)
					{
						value = null;
						return false;
					}

					object? MakeIndex(ExpressionSyntax expr)
					{
						if (TryGetLiteralValue(expr, out var innerVal) && innerVal is not null)
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
			case ObjectCreationExpressionSyntax objectCreationExpression when semanticModel.TryGetSymbol(objectCreationExpression, out IMethodSymbol? constructor):
			{
				var arguments = objectCreationExpression.ArgumentList.Arguments
					.Select(s => Visit(s.Expression))
					.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue);

				if (loader.TryExecuteMethod(constructor, null, null, arguments, out var result))
				{
					value = result;
					return true;
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
			case CastExpressionSyntax castExpressionSyntax:
			{
				if (TryGetLiteralValue(castExpressionSyntax.Expression, out var innerVal))
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
						"Object" => innerVal,
						_ => innerVal
					};

					return true;
				}
				break;
			}
			case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
			{
				if (semanticModel.TryGetSymbol(node, out ISymbol? symbol))
				{
					TryGetLiteralValue(Visit(memberAccessExpressionSyntax.Expression), out var instanceValue);

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
								if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [ ], out value))
								{
									return true;
								}
							}
							break;
					}
				}

				break;
			}
			case CollectionExpressionSyntax collectionExpressionSyntax:
			{
				var elements = new List<object?>();
				
				foreach (var element in collectionExpressionSyntax.Elements.OfType<ExpressionElementSyntax>())
				{
					if (TryGetLiteralValue(element.Expression, out var elemVal))
					{
						elements.Add(elemVal);
					}
					else
					{
						value = null;
						return false;
					}
				}
				
				var type = elements[0]?.GetType() ?? typeof(object);
				
				var array = Array.CreateInstance(type, elements.Count);
				
				for (var i = 0; i < elements.Count; i++)
				{
					array.SetValue(Convert.ChangeType(elements[i], type), i);
				}

				value = array;
				return true;
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