using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Builders;

public class InterfaceBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, int hashCode) : BaseBuilder(elementType, compilation, generationLevel, loader, hashCode)
{
	public bool AppendCount(IPropertySymbol property, int count, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { Name: "Count", Type.SpecialType: SpecialType.System_Int32 }:
				AppendProperty(builder, property,
					$"return {count};",
					"throw new NotSupportedException();");

				return true;
			default:
				return false;
		}

	}

	public bool AppendLength(IPropertySymbol property, int count, IndentedStringBuilder builder)
	{
		switch (property)
		{
			case { Name: "Length", Type.SpecialType: SpecialType.System_Int32 }:
				AppendProperty(builder, property,
					$"return {count};",
					"throw new NotSupportedException();");

				return true;
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
		if (property is not { Name: "this[]", IsIndexer: true, Parameters: [ { Type.SpecialType: SpecialType.System_Int32 } ] } && !SymbolEqualityComparer.Default.Equals(property.Type, elementType))
		{
			return false;
		}

		builder.AppendLine();

		if (property.IsReadOnly)
		{
			using (builder.AppendBlock($"public {elementType.ToDisplayString()} this[int index] => index switch", "};"))
			{
				foreach (var item in items.Index().GroupBy(g => g.Value, g => g.Index))
				{
					builder.AppendLine($"{String.Join(" or ", item.Select(SyntaxHelpers.CreateLiteral))} => {SyntaxHelpers.CreateLiteral(item.Key)},");
				}

				builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
			}
		}

		if (property.IsWriteOnly)
		{
			builder.AppendLine($"public {elementType.Name} this[int index] => throw new NotSupportedException();");
			return true;
		}

		using (builder.AppendBlock($"public {elementType.Name} this[int index]"))
		{
			using (builder.AppendBlock("get => index switch", "};"))
			{
				var index = 0;

				foreach (var item in items.Index().GroupBy(g => g.Value, g => g.Index))
				{
					builder.AppendLine($"{String.Join(" or ", item.Select(SyntaxHelpers.CreateLiteral))} => {SyntaxHelpers.CreateLiteral(item.Key)},");
				}

				builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
			}
			builder.AppendLine("set => throw new NotSupportedException();");
		}

		return true;
	}

	public bool AppendCopyTo(IMethodSymbol method, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "CopyTo", Parameters.Length: 1 or 2 }
				when SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType)
				     && (method.Parameters.Length != 2 || method.Parameters[1].Type.SpecialType == SpecialType.System_Int32):
			{
				using (AppendMethod(builder, method))
				{
					if (method.Parameters.Length == 2)
					{
						if (method.Parameters[0].Type.IsReferenceType)
						{
							using (builder.AppendBlock($"if ({method.Parameters[0].Name} is null)"))
							{
								builder.AppendLine($"throw new ArgumentNullException(nameof({method.Parameters[0].Name}));");
							}

							builder.AppendLine();
						}

						using (builder.AppendBlock($"if ({method.Parameters[1].Name} < 0 || {method.Parameters[1].Name} + {items.Count()} >= {method.Parameters[0].Name}.{GetLengthPropertyName(method.Parameters[0].Type)})"))
						{
							builder.AppendLine($"throw new ArgumentOutOfRangeException(nameof({method.Parameters[1].Name}));");
						}

						builder.AppendLine();

						var index = 0;

						foreach (var item in items)
						{
							if (method.Parameters.Length == 1)
							{
								builder.AppendLine($"{method.Parameters[0].Name}[{index++}] = {SyntaxHelpers.CreateLiteral(item)};");
							}
							else
							{
								builder.AppendLine($"{method.Parameters[0].Name}[{method.Parameters[1].Name} + {index++}] = {SyntaxHelpers.CreateLiteral(item)};");
							}
						}
					}
					else
					{
						builder.AppendLine(GetDataName(method.ContainingType));
						builder.AppendLine($"\t.CopyTo({method.Parameters[0].Name});");
					}
				}

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
			case { Name: "Add" }
				when method.Parameters.EqualsTypes(elementType):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				}

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
			case { Name: "Clear", Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Void }:
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				}

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
				when method.Parameters.EqualsTypes(elementType):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				}

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendIndexOf(IMethodSymbol method, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "IndexOf", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.EqualsTypes(elementType):
			{
				using (AppendMethod(builder, method))
				{
					using (builder.AppendBlock($"return {method.Parameters[0].Name} switch", "};"))
					{
						var hashSet = new HashSet<object?>();

						foreach (var (index, value) in items.Index())
						{
							if (hashSet.Add(value))
							{
								builder.AppendLine($"{SyntaxHelpers.CreateLiteral(value)} => {index},");
							}
						}

						builder.AppendLine("_ => -1,");
					}
				}

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
			case { Name: "Insert" }
				when method.Parameters.EqualsTypes(elementType, compilation.CreateInt32()):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				}

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
			case { Name: "RemoveAt" }
				when method.Parameters.EqualsTypes(compilation.CreateInt32()):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
				}

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendContains(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Contains", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.EqualsTypes(elementType):
			{
				items = items
					.Distinct()
					.OrderBy(o => o)
					.ToList();

				using (AppendMethod(builder, method))
				{
					var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out _);

					if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
					{
						using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
						{
							if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "Any"))
							{
								builder.AppendLine($"return {vectorType}.Any({vector}, {method.Parameters[0].Name});");
							}
							else
							{
								builder.AppendLine($"return {vectorType}.EqualsAny({vector}, {vectorType}.Create({method.Parameters[0].Name}));");
							}
						}

						builder.AppendLine();
					}

					if (items.Count > 0)
					{
						// Check if the interface implements BinarySearch
						if (method.ContainingType.HasMember<IMethodSymbol>("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                        && m.Parameters.EqualsTypes(elementType)
						                                                                        && elementType.HasMember<IMethodSymbol>("CompareTo", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                                                  && x.Parameters.EqualsTypes(elementType))))
						{
							builder.AppendLine($"return BinarySearch({method.Parameters[0].Name}) >= 0;");
						}
						// Check if the interface implements BinarySearch with a comparer
						else if (method.ContainingType.HasMember<IMethodSymbol>("BinarySearch", m => m is { ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
						                                                                             && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType)
						                                                                             && m.Parameters[1].Type.HasMember<IMethodSymbol>("Compare", x => x is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                                                                                              && x.Parameters.EqualsTypes(elementType, elementType))))
						{
							builder.AppendLine($"return BinarySearch({method.Parameters[0].Name}, Comparer<{compilation.GetMinimalString(elementType)}>.Default) >= 0;");
						}
						else
						{
							var elements = items.Select(s => SyntaxHelpers.CreateLiteral(s).ToString());
							var length = elements.Sum(s => s.Length);

							if (length > 100)
							{
								builder.AppendLine($"return {method.Parameters[0].Name} is {SyntaxHelpers.CreateLiteral(items[0])}");

								for (var i = 1; i < items.Count; i++)
								{
									if (i == items.Count - 1)
									{
										builder.AppendLine($"\tor {SyntaxHelpers.CreateLiteral(items[i])};");
									}
									else
									{
										builder.AppendLine($"\tor {SyntaxHelpers.CreateLiteral(items[i])}");
									}
								}
							}
							else
							{
								builder.AppendLine($"return {method.Parameters[0].Name} is {String.Join(" or ", elements)};");
							}
						}
					}
					else
					{
						builder.AppendLine("return false;");
					}
				}

				return true;
			}
			default:
				return false;
		}

	}
}