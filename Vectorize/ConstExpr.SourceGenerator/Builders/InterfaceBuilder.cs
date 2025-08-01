using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SourceGen.Utilities.Extensions;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Builders;

public class InterfaceBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, string dataName) : BaseBuilder(elementType, compilation, generationLevel, loader, dataName)
{
	public bool AppendCount(IPropertySymbol property, int count, IndentedCodeWriter builder)
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

	public bool AppendLength(IPropertySymbol property, int count, IndentedCodeWriter builder)
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

	public bool AppendIsReadOnly(IPropertySymbol property, IndentedCodeWriter builder)
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

	public bool AppendIndexer(IPropertySymbol property, IEnumerable<object?> items, IndentedCodeWriter builder)
	{
		switch (property)
		{
			case { IsIndexer: true, Parameters: [ { Type.SpecialType: SpecialType.System_Int32 } ] }
				when SymbolEqualityComparer.Default.Equals(property.Type, elementType):
			{
				builder.WriteLine();

				if (property.IsReadOnly)
				{
					builder.WriteLine($"public {elementType} this[int index] => {DataName:literal}[index];");

					// using (builder.WriteBlock($"public {elementType} this[int index] => index switch", "};"))
					// {
					// 	foreach (var item in items.Index().GroupBy(g => g.Value, g => g.Index))
					// 	{
					// 		builder.WriteLine($"{(LiteralString)String.Join(" or ", item.Select(SyntaxHelpers.CreateLiteral))} => {item.Key},");
					// 	}
					//
					// 	builder.WriteLine("_ => throw new ArgumentOutOfRangeException(),");
					// }

					return true;
				}

				if (property.IsWriteOnly)
				{
					builder.WriteLine($"public {elementType} this[int index] => throw new NotSupportedException();");
					return true;
				}

				builder.WriteLine($$"""
					public {{elementType}} this[int index]
					{
						get => {{DataName:literal}}[index];
						set => throw new NotSupportedException();
					}
					""");

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCopyTo<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "CopyTo", Parameters.Length: 1, ReturnsVoid: true }
				when compilation.IsSpanType(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"{DataName:literal}.CopyTo({method.Parameters[0]});");
				});
				return true;
			}
			case { Name: "CopyTo", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateArrayTypeSymbol(elementType), compilation.CreateInt32()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"{DataName:literal}.CopyTo({method.Parameters[0]}.AsSpan({method.Parameters[1]}));");
				});
				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendAdd(IMethodSymbol method, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "Add", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendClear(IMethodSymbol method, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "Clear", Parameters.Length: 0, ReturnsVoid: true }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendRemove(IMethodSymbol method, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "Remove", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndexOf<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter? builder)
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
						// var min = items.Min();
						// var max = items.Max();

						// if (elementType.IsInterger() && Comparer<object?>.Default.Compare(max.Subtract(min).ToSpecialType(elementType.SpecialType), 10.ToSpecialType(elementType.SpecialType)) <= 0)
						// {
						// 	var indexes = new List<int>();
						//
						// 	for (var i = min; !EqualityComparer<object?>.Default.Equals(i, max.Add(1.ToSpecialType(elementType.SpecialType))); i = (T) i.Add(1.ToSpecialType(elementType.SpecialType)))
						// 	{
						// 		indexes.Add(items.IndexOf(i));
						// 	}
						//
						// 	builder!.WriteLine($"ReadOnlySpan<int> map = [{indexes}];");
						// 	builder.WriteLine();
						//
						// 	if (!EqualityComparer<object?>.Default.Equals(min, 0.ToSpecialType(elementType.SpecialType)))
						// 	{
						// 		builder.WriteLine($"{method.Parameters[0]} -= {min};");
						// 		builder.WriteLine();
						// 	}
						//
						// 	builder.WriteLine("return (uint)item < (uint)map.Length ? map[item] : -1;");
						// }
						// else
						// {
						// 	using (builder!.WriteBlock($"return {method.Parameters[0]} switch", end: "};"))
						// 	{
						// 		var set = new HashSet<object?>();
						//
						// 		foreach (var (index, value) in items.Index())
						// 		{
						// 			if (set.Add(value))
						// 			{
						// 				builder.WriteLine($"{value} => {index},");
						// 			}
						// 		}
						//
						// 		builder.WriteLine("_ => -1,");
						// 	}
						// }

						using (builder!.WriteBlock($"return {method.Parameters[0]} switch", end: "};"))
						{
							var set = new HashSet<object?>();

							foreach (var (index, value) in items.Index())
							{
								if (set.Add(value))
								{
									builder.WriteLine($"{value} => {index},");
								}
							}

							builder.WriteLine("_ => -1,");
						}
					}
					else
					{
						builder!.WriteLine($"return {DataName:literal}.IndexOf({method.Parameters[0]});");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendInsert(IMethodSymbol method, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "Insert", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32(), elementType):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendRemoveAt(IMethodSymbol method, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "RemoveAt", ReturnsVoid: true }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine("throw new NotSupportedException(\"Collection is read-only.\");");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendContains<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter? builder)
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
								if (unsignedType.EqualsType(unsignedType))
								{
									builder.WriteLine($"return {method.Parameters[0]} <= {items[^1].ToSpecialType(unsignedType.SpecialType)};");
								}
								else
								{
									builder.WriteLine($"return ({unsignedType}){method.Parameters[0]} <= {items[^1].ToSpecialType(unsignedType.SpecialType)};");
								}
							}
							else
							{
								builder.WriteLine($"return {method.Parameters[0]} is >= {items[0]} and <= {items[^1]};");
							}
						});

						return true;
					}
				}

				if (method.ContainingType.HasMethod("IndexOf", m => AppendIndexOf(m, items, null)))
				{
					AppendMethod(builder, method, () =>
					{
						builder.WriteLine($"return IndexOf({method.Parameters[0]}) >= 0;");
					});

					return true;
				}

				AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, size) =>
				{
					builder.WriteLine($"var {method.Parameters[0]}Vector = {vectorType}.Create({method.Parameters[0]});");
					builder.WriteLine();

					if (size * vectors.Count < items.Length)
					{
						var checks = items
							.Skip(vectors.Count * size)
							.ToList();
						// .Select(s => $"{method.Parameters[0].Name} == {s}");

						if (compilation.GetVectorType(vectorType).HasMethod("AnyWhereAllBitsSet"))
						{
							CreatePadding(builder, "|", $"return {vectorType}.AnyWhereAllBitsSet(", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false).WriteLine(");");
						}
						else
						{
							CreatePadding(builder, "|", "return (", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false).WriteLine($") != {vectorType}<{elementType}>.Zero");
						}

						builder.WriteLine($"     || {method.Parameters[0]} is {String.Join(" or ", checks.Select(SyntaxHelpers.CreateLiteral)):literal};");

						// builder.WriteLine(CreatePadding("|", "      |", checks));
					}
					else
					{
						if (compilation.GetVectorType(vectorType).HasMethod("AnyWhereAllBitsSet"))
						{
							CreatePadding(builder, "|", $"return {vectorType}.AnyWhereAllBitsSet(", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false).WriteLine(");");
						}
						else
						{
							CreatePadding(builder, "|", "return (", vectors.Select(s => $"{vectorType}.Equals({s}, {method.Parameters[0].Name}Vector)"), false, false).WriteLine($") != {vectorType}<{elementType}>.Zero");
						}
					}
				}, isPerformance =>
				{
					if (items.Length > 0)
					{
						if (method.ContainingType.HasMethod("IndexOf", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                    && m.Parameters.AsSpan().EqualsTypes(elementType)))
						{
							builder.WriteLine($"return IndexOf({method.Parameters[0]}) >= 0;");
						}
						else if (method.ContainingType.HasMethod("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                              && m.Parameters.AsSpan().EqualsTypes(elementType)
						                                                              && elementType.HasMethod("CompareTo", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                         && x.Parameters.AsSpan().EqualsTypes(elementType))))
						{
							builder.WriteLine($"return BinarySearch({method.Parameters[0]}) >= 0;");
						}
						// Check if the interface implements BinarySearch with a comparer
						else if (method.ContainingType.HasMethod("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
						                                                              && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType)
						                                                              && m.Parameters[1].Type.HasMethod("Compare", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                                && x.Parameters.AsSpan().EqualsTypes(elementType, elementType))))
						{
							builder.WriteLine($"return BinarySearch({method.Parameters[0]}, Comparer<{elementType}>.Default) >= 0;");
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

							CreatePadding(builder, "or", $"return {method.Parameters[0].Name} is", elements).WriteLine();
						}
					}
					else
					{
						builder.WriteLine("return false;");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOverlaps<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						if (method.ContainingType.HasMethod("Contains", m => AppendContains(m, items, null)))
						{
							builder.WriteLine($"return {method.Parameters[0]}.Any(Contains);");
							return;
						}

						if (method.ContainingType.HasMethod("IndexOf", m => AppendIndexOf(m, items, null)))
						{
							builder.WriteLine($"return {method.Parameters[0]}.Any(item => IndexOf(item) >= 0);");
							return;
						}
					}

					using (builder.WriteBlock($"foreach (var item in {method.Parameters[0]})", padding: WhitespacePadding.After))
					{
						if (method.ContainingType.HasMethod("Contains", m => AppendContains(m, items, null)))
						{
							using (builder.WriteBlock("if (Contains(item))"))
							{
								builder.WriteLine("return true;");
							}
						}
						else if (method.ContainingType.HasMethod("IndexOf", m => AppendIndexOf(m, items, null)))
						{
							using (builder.WriteBlock("if (IndexOf(item) >= 0)"))
							{
								builder.WriteLine("return true;");
							}
						}
						else
						{
							if (isPerformance && elementType.IsLiteralType())
							{
								using (builder.WriteBlock($"if (item is {String.Join(" or ", items.Distinct().Select(SyntaxHelpers.CreateLiteral)):literal})"))
								{
									builder.WriteLine("return true;");
								}
							}
							else
							{
								using (builder.WriteBlock($"if ({DataName:literal}.Contains(item))"))
								{
									builder.WriteLine("return true;");
								}
							}
						}
					}

					builder.WriteLine("return false;");
				});

				return true;
			}
			default:
				return false;
		}
	}
}