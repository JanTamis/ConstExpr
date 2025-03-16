using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

public class InterfaceBuilder(Compilation compilation, ITypeSymbol elementType) : BaseBuilder(elementType, compilation)
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
				builder.AppendLine($"((ReadOnlySpan<{elementType.ToDisplayString()}>)[{String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))}])");
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
		if (!typeSymbol.CheckMethod("Contains", compilation.GetSpecialType(SpecialType.System_Boolean), [ elementType ], out var member))
		{
			return;
		}

		items = items.Distinct().ToList();

		using (AppendMethod(builder, member))
		{
			if (items.Count > 0)
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
				builder.AppendLine("return false;");
			}
		}
	}
}