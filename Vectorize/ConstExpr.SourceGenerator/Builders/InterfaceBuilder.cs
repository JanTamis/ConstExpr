using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ConstExpr.SourceGenerator.Builders;

public class InterfaceBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, string dataName) : BaseBuilder(elementType, compilation, generationLevel, loader, dataName)
{
	public bool AppendCount(IPropertySymbol property, int count, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { Name: "Count", Type.SpecialType: SpecialType.System_Int32 }:
			{
				AppendProperty(builder, property,
					$"return {count};",
					"throw new NotSupportedException();");

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendLength(IPropertySymbol property, int count, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { Name: "Length", Type.SpecialType: SpecialType.System_Int32 }:
			{
				AppendProperty(builder, property,
					$"return {count};",
					"throw new NotSupportedException();");

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendIsReadOnly(IPropertySymbol property, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { Name: "IsReadOnly", Type.SpecialType: SpecialType.System_Boolean, IsReadOnly: true }:
				AppendProperty(builder, property,
					"return true;",
					"throw new NotSupportedException();");

				return true;
			default:
				return false;
		}

	}

	public bool AppendIndexer(IPropertySymbol property, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { IsIndexer: true, Parameters: [ { Type.SpecialType: SpecialType.System_Int32 } ] }
				when SymbolEqualityComparer.Default.Equals(property.Type, elementType):
			{
				builder.AppendLine();

				if (property.IsReadOnly)
				{
					builder.AppendLine($"public {elementType} this[int index] => {GetDataName()}[index];");

					// using (builder.AppendBlock($"public {elementType} this[int index] => index switch", "};"))
					// {
					// 	foreach (var item in items.Index().GroupBy(g => g.Value, g => g.Index))
					// 	{
					// 		builder.AppendLine($"{(LiteralString)String.Join(" or ", item.Select(SyntaxHelpers.CreateLiteral))} => {item.Key},");
					// 	}
					//
					// 	builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
					// }

					return true;
				}

				if (property.IsWriteOnly)
				{
					builder.AppendLine($"public {elementType} this[int index] => throw new NotSupportedException();");
					return true;
				}

				using (builder.AppendBlock($"public {elementType} this[int index]"))
				{
					// using (builder.AppendBlock("get => index switch", "};"))
					// {
					// 	var index = 0;
					//
					// 	foreach (var item in items.Index().GroupBy(g => g.Value, g => g.Index))
					// 	{
					// 		builder.AppendLine($"{(LiteralString)String.Join(" or ", item.Select(SyntaxHelpers.CreateLiteral))} => {item.Key},");
					// 	}
					//
					// 	builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
					// }
					builder.AppendLine($"get => {GetDataName()}[index];");
					builder.AppendLine("set => throw new NotSupportedException();");
				}

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCopyTo<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "CopyTo", Parameters.Length: 1, ReturnsVoid: true }
				when compilation.IsSpanType(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"{GetDataName()}.CopyTo({method.Parameters[0]});");
				});
				return true;
			}
			case { Name: "CopyTo", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateArrayTypeSymbol(elementType), compilation.CreateInt32()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"{GetDataName()}.CopyTo({method.Parameters[0]}.AsSpan({method.Parameters[1]}));");
				});
				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendAdd(IMethodSymbol method, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Add", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendClear(IMethodSymbol method, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Clear", Parameters.Length: 0, ReturnsVoid: true }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendRemove(IMethodSymbol method, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Remove", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndexOf<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder? builder)
	{
		switch (method)
		{
			case { Name: "IndexOf", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						var min = items.Min();
						var max = items.Max();

						if (elementType.IsInterger() && Comparer<object?>.Default.Compare(max.Subtract(min), 10.ToSpecialType(elementType.SpecialType)) <= 0)
						{
							var indexes = new List<int>();

							for (var i = min; !EqualityComparer<object?>.Default.Equals(i, max.Add(1.ToSpecialType(elementType.SpecialType))); i = (T) i.Add(1.ToSpecialType(elementType.SpecialType)))
							{
								indexes.Add(items.IndexOf(i));
							}

							builder.AppendLine($"ReadOnlySpan<int> map = [{(LiteralString) String.Join(", ", indexes)}];");
							builder.AppendLine();

							if (!EqualityComparer<object?>.Default.Equals(min, 0.ToSpecialType(elementType.SpecialType)))
							{
								builder.AppendLine($"{method.Parameters[0]} -= {min};");
								builder.AppendLine();
							}

							builder.AppendLine("return (uint)item < (uint)map.Length ? map[item] : -1;");
						}
						else
						{
							using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
							{
								var set = new HashSet<object?>();

								foreach (var (index, value) in items.Index())
								{
									if (set.Add(value))
									{
										builder.AppendLine($"{value} => {index},");
									}
								}

								builder.AppendLine("_ => -1,");
							}
						}
					}
					else
					{
						builder.AppendLine($"return {GetDataName()}.IndexOf({method.Parameters[0]});");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendInsert(IMethodSymbol method, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Insert", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(elementType, compilation.CreateInt32()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendRemoveAt(IMethodSymbol method, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "RemoveAt", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendContains<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder? builder)
	{
		switch (method)
		{
			case { Name: "Contains", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				items = items
					.Distinct()
					.OrderBy(s => s)
					.ToImmutableArray();

				if (elementType.IsInterger())
				{
					if (items.AsSpan().IsNumericSequence())
					{
						AppendMethod(builder, method, () =>
						{
							if (items.AsSpan(0, 1).IsZero() && compilation.TryGetUnsignedType(elementType, out var unsignedType))
							{
								builder.AppendLine($"return ({unsignedType}){method.Parameters[0]} <= {items[^1].ToSpecialType(unsignedType.SpecialType)};");
							}
							else
							{
								builder.AppendLine($"return {method.Parameters[0]} is >= {items[0]} and <= {items[^1]};");
							}
						});

						return true;
					}
				}

				if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => AppendIndexOf(m, items, null)))
				{
					AppendMethod(builder, method, () =>
					{
						builder.AppendLine($"return IndexOf({method.Parameters[0]}) >= 0;");
					});

					return true;
				}

				AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, size) =>
				{
					builder.AppendLine($"var {method.Parameters[0]}Vector = {vectorType}.Create({method.Parameters[0]});");
					builder.AppendLine();

					if (size * vectors.Count < items.Length)
					{
						var checks = items
							.Skip(vectors.Count * size)
							.ToList();
						// .Select(s => $"{method.Parameters[0].Name} == {s}");

						if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
						{
							builder.AppendLine(CreatePadding("|", $"return {vectorType}.AnyWhereAllBitsSet(", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false) + ")");
						}
						else
						{
							builder.AppendLine(CreatePadding("|", "return (", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false) + $") != {vectorType}<{compilation.GetMinimalString(elementType)}>.Zero");
						}

						builder.AppendLine($"     || {method.Parameters[0]} is {(LiteralString) String.Join(" or ", checks.Select(SyntaxHelpers.CreateLiteral))};");

						// builder.AppendLine(CreatePadding("|", "      |", checks));
					}
					else
					{
						if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
						{
							builder.AppendLine(CreatePadding("|", $"return {vectorType}.AnyWhereAllBitsSet(", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false) + ");");
						}
						else
						{
							builder.AppendLine(CreatePadding("|", "return (", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false) + $") != {vectorType}<{compilation.GetMinimalString(elementType)}>.Zero;");
						}
					}
				}, isPerformance =>
				{
					if (items.Length > 0)
					{
						if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                   && m.Parameters.AsSpan().EqualsTypes(elementType)))
						{
							builder.AppendLine($"return IndexOf({method.Parameters[0]}) >= 0;");
						}
						else if (method.ContainingType.HasMember<IMethodSymbol>("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                             && m.Parameters.AsSpan().EqualsTypes(elementType)
						                                                                             && elementType.HasMember<IMethodSymbol>("CompareTo", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                                                       && x.Parameters.AsSpan().EqualsTypes(elementType))))
						{
							builder.AppendLine($"return BinarySearch({method.Parameters[0]}) >= 0;");
						}
						// Check if the interface implements BinarySearch with a comparer
						else if (method.ContainingType.HasMember<IMethodSymbol>("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
						                                                                             && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType)
						                                                                             && m.Parameters[1].Type.HasMember<IMethodSymbol>("Compare", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                                                              && x.Parameters.AsSpan().EqualsTypes(elementType, elementType))))
						{
							builder.AppendLine($"return BinarySearch({method.Parameters[0]}, Comparer<{elementType}>.Default) >= 0;");
						}
						else
						{
							var literals = items
								.Select(SyntaxHelpers.CreateLiteral)
								.ToList();

							var length = literals.Sum(s => s.Span.Length) + items.Length * 2;
							var rowLength = length < 125 ? items.Length : (int) Math.Ceiling(Math.Sqrt(items.Length));
							
							var elements = literals
								.Chunk(rowLength)
								.Select(s => String.Join(" or ", s));

							builder.AppendLine(CreatePadding("or", $"return {method.Parameters[0].Name} is", elements));
						}
					}
					else
					{
						builder.AppendLine("return false;");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOverlaps<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Overlaps", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 1 }
				when compilation.IsEnumerableType(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (method.Parameters[0].Type.EqualsType(compilation.CreateIEnumerable(elementType)))
					{
						if (method.ContainingType.HasMember<IMethodSymbol>("Contains", m => AppendContains(m, items, null)))
						{
							builder.AppendLine($"return {method.Parameters[0]}.Any(Contains);");
							return;
						}

						if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => AppendIndexOf(m, items, null)))
						{
							builder.AppendLine($"return {method.Parameters[0]}.Any(item => IndexOf(item) >= 0);");
							return;
						}
					}

					using (builder.AppendBlock($"foreach (var item in {method.Parameters[0]})", WhitespacePadding.After))
					{
						if (method.ContainingType.HasMember<IMethodSymbol>("Contains", m => AppendContains(m, items, null)))
						{
							using (builder.AppendBlock("if (Contains(item))"))
							{
								builder.AppendLine("return true;");
							}
						}
						else if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => AppendIndexOf(m, items, null)))
						{
							using (builder.AppendBlock("if (IndexOf(item) >= 0)"))
							{
								builder.AppendLine("return true;");
							}
						}
						else
						{
							if (isPerformance && elementType.IsLiteralType())
							{
								using (builder.AppendBlock($"if (item is {(LiteralString) String.Join(" or ", items.Distinct().Select(SyntaxHelpers.CreateLiteral))})"))
								{
									builder.AppendLine("return true;");
								}
							}
							else
							{
								using (builder.AppendBlock($"if ({GetDataName()}.Contains(item))"))
								{
									builder.AppendLine("return true;");
								}
							}
						}
					}

					builder.AppendLine("return false;");
				});

				return true;
			}
			default:
				return false;
		}
	}
}