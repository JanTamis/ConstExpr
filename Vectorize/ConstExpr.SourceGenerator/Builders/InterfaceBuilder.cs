using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Builders;

public class InterfaceBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, int hashCode) : BaseBuilder(elementType, compilation, hashCode)
{
	public void AppendCount(ITypeSymbol typeSymbol, int count, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("Count", m => m.Type.SpecialType == SpecialType.System_Int32, out var member))
		{
			return;
		}

		AppendProperty(builder, member,
			$"return {count};",
			"throw new NotSupportedException();");
	}

	public void AppendLength(ITypeSymbol typeSymbol, int count, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("Length", m => m.Type.SpecialType == SpecialType.System_Int32, out var member))
		{
			return;
		}

		AppendProperty(builder, member,
			$"return {count};",
			"throw new NotSupportedException();");
	}

	public void AppendIsReadOnly(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("IsReadOnly", m => m.Type.SpecialType == SpecialType.System_Boolean, out var member) || !member.IsReadOnly)
		{
			return;
		}

		AppendProperty(builder, member,
			"return true;",
			"throw new NotSupportedException();");
	}

	public void AppendIndexer(ITypeSymbol typeSymbol, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("this[]", m => m.IsIndexer
		                                                             && m.Parameters is [ { Type.SpecialType: SpecialType.System_Int32 } ]
		                                                             && SymbolEqualityComparer.Default.Equals(m.Type, elementType), out var member))
		{
			return;
		}

		builder.AppendLine();

		if (member.IsReadOnly)
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

		if (member.IsWriteOnly)
		{
			builder.AppendLine($"public {elementType.Name} this[int index] => throw new NotSupportedException();");
			return;
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
	}

	public void AppendCopyTo(ITypeSymbol typeSymbol, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!(typeSymbol.CheckMembers<IMethodSymbol>("CopyTo", m => m.Parameters.Length is 1 or 2, out var member)
		      && IsIndexableType(member.Parameters[0].Type)
		      && (member.Parameters.Length == 1 || member.Parameters[1].Type.SpecialType == SpecialType.System_Int32)))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			if (member.Parameters.Length == 2)
			{
				if (member.Parameters[0].Type.IsReferenceType)
				{
					using (builder.AppendBlock($"if ({member.Parameters[0].Name} is null)"))
					{
						builder.AppendLine($"throw new ArgumentNullException(nameof({member.Parameters[0].Name}));");
					}

					builder.AppendLine();
				}
				
				using (builder.AppendBlock($"if ({member.Parameters[1].Name} < 0 || {member.Parameters[1].Name} + {items.Count()} >= {member.Parameters[0].Name}.{GetLengthPropertyName(member.Parameters[0].Type)})"))
				{
					builder.AppendLine($"throw new ArgumentOutOfRangeException(nameof({member.Parameters[1].Name}));");
				}

				builder.AppendLine();

				var index = 0;

				foreach (var item in items)
				{
					if (member.Parameters.Length == 1)
					{
						builder.AppendLine($"{member.Parameters[0].Name}[{index++}] = {SyntaxHelpers.CreateLiteral(item)};");
					}
					else
					{
						builder.AppendLine($"{member.Parameters[0].Name}[{member.Parameters[1].Name} + {index++}] = {SyntaxHelpers.CreateLiteral(item)};");
					}
				}
			}
			else
			{
				builder.AppendLine(GetDataName(typeSymbol));
				builder.AppendLine($"\t.CopyTo({member.Parameters[0].Name});");
			}
		}
	}

	public void AppendAdd(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("Add", [elementType], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public void AppendClear(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("Clear", [], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public void AppendRemove(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("Remove", compilation.GetSpecialType(SpecialType.System_Boolean), [ elementType ], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public void AppendIndexOf(ITypeSymbol typeSymbol, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("IndexOf", m => m.Parameters.Length == 1
		                                                            && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType)
		                                                            && m.ReturnType.SpecialType == SpecialType.System_Int32, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			using (builder.AppendBlock($"return {member.Parameters[0].Name} switch", "};"))
			{
				var index = 0;
				var hashSet = new HashSet<object?>();

				foreach (var item in items)
				{
					if (hashSet.Add(item))
					{
						builder.AppendLine($"{SyntaxHelpers.CreateLiteral(item)} => {index},");
					}

					index++;
				}

				builder.AppendLine("_ => -1,");
			}
		}
	}

	public void AppendInsert(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("Insert", [compilation.GetSpecialType(SpecialType.System_Int32), elementType], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public void AppendRemoveAt(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("RemoveAt", [ compilation.GetSpecialType(SpecialType.System_Int32) ], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public void AppendContains(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("Contains", compilation.CreateBoolean(), [ elementType ], out var member))
		{
			return;
		}

		items = items
			.Distinct()
			.OrderBy(o => o)
			.ToList();

		using (AppendMethod(builder, member))
		{
			var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out _);

			if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
			{
				using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
				{
					if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "Any"))
					{
						builder.AppendLine($"return {vectorType}.Any({vector}, {member.Parameters[0].Name});");
					}
					else
					{
						builder.AppendLine($"return {vectorType}.EqualsAny({vector}, {vectorType}.Create({member.Parameters[0].Name}));");	
					}
					
				}
				
				builder.AppendLine();
			}
			
			if (items.Count > 0)
			{
				var elements = items.Select(s => SyntaxHelpers.CreateLiteral(s).ToString());
				var length = elements.Sum(s => s.Length);

				if (length > 100)
				{
					builder.AppendLine($"return {member.Parameters[0].Name} is {SyntaxHelpers.CreateLiteral(items[0])}");

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
					builder.AppendLine($"return {member.Parameters[0].Name} is {String.Join(" or ", elements)};");
				}
			}
			else
			{
				builder.AppendLine("return false;");
			}
		}
	}
}