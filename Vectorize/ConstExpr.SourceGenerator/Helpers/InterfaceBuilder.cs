using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

public static class InterfaceBuilder
{
	public static void AppendCount(ITypeSymbol typeSymbol, int count, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("Count", m => m.Type.SpecialType == SpecialType.System_Int32, out var member))
		{
			return;
		}

		builder.AppendLine();

		if (member.IsReadOnly)
		{
			builder.AppendLine($"public int Count => {count};");
			return;
		}

		if (member.IsWriteOnly)
		{
			builder.AppendLine("public int Count => throw new NotSupportedException();");
			return;
		}

		using (builder.AppendBlock("public int Count"))
		{
			builder.AppendLine($"get => {count};");
			builder.AppendLine("set => throw new NotSupportedException();");
		}
	}

	public static void AppendLength(ITypeSymbol typeSymbol, int count, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("Length", m => m.Type.SpecialType == SpecialType.System_Int32, out var member))
		{
			return;
		}

		builder.AppendLine();

		if (member.IsReadOnly)
		{
			builder.AppendLine($"public int Length => {count};");
			return;
		}

		if (member.IsWriteOnly)
		{
			builder.AppendLine("public int Length => throw new NotSupportedException();");
			return;
		}

		using (builder.AppendBlock("public int Length"))
		{
			builder.AppendLine($"get => {count};");
			builder.AppendLine("set => throw new NotSupportedException();");
		}
	}

	public static void AppendIsReadOnly(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("IsReadOnly", m => m.Type.SpecialType == SpecialType.System_Boolean, out var member) || !member.IsReadOnly)
		{
			return;
		}

		builder.AppendLine();

		if (member.IsReadOnly)
		{
			builder.AppendLine("public bool IsReadOnly => true;");
		}
	}

	public static void AppendIndexer(ITypeSymbol typeSymbol, string typeName, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("this[]", m => m.IsIndexer && m.Parameters is [ { Type.SpecialType: SpecialType.System_Int32 } ], out var member))
		{
			return;
		}

		builder.AppendLine();

		if (member.IsReadOnly)
		{
			using (builder.AppendBlock($"public {typeName} this[int index] => index switch", "};"))
			{
					var index = 0;

					foreach (var item in items)
					{
						builder.AppendLine($"{index} => {SyntaxHelpers.CreateLiteral(item)},");
						index++;
					}

					builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
			}
		}

		if (member.IsWriteOnly)
		{
			builder.AppendLine($"public {typeName} this[int index] => throw new NotSupportedException();");
			return;
		}

		using (builder.AppendBlock($"public {typeName} this[int index]"))
		{
			using (builder.AppendBlock("get => index switch", "};"))
			{
				var index = 0;

				foreach (var item in items)
				{
					builder.AppendLine($"{index++} => {SyntaxHelpers.CreateLiteral(item)},");
				}

				builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
			}
			builder.AppendLine("set => throw new NotSupportedException();");
		}
	}

	public static void AppendCopyTo(ITypeSymbol typeSymbol, IEnumerable<object?> items, ITypeSymbol elementType, IndentedStringBuilder builder)
	{
		if (!(typeSymbol.CheckMembers<IMethodSymbol>("CopyTo", m => m.Parameters.Length == 2, out var member)
		      && (member.Parameters[0].Type.SpecialType == SpecialType.System_Array
		          || member.Parameters[0].Type.CheckMembers<IPropertySymbol>(m => m.IsIndexer && m is { IsWriteOnly: false, Parameters: [ { Type.SpecialType: SpecialType.System_Int32 } ] }, out _)
		          && member.Parameters[1].Type.SpecialType == SpecialType.System_Int32)))
		{
			return;
		}

		var type = member.Parameters[0].Type;

		if (type is INamedTypeSymbol namedTypeSymbol)
		{
			type = namedTypeSymbol.Construct(elementType);
		}

		builder.AppendLine();

		using (builder.AppendBlock($"public void CopyTo({type.ToDisplayString()} {member.Parameters[0].Name}, int {member.Parameters[1].Name})"))
		{
			using (builder.AppendBlock($"if ({member.Parameters[0].Name} is null)"))
			{
				builder.AppendLine($"throw new ArgumentNullException(nameof({member.Parameters[0].Name}));");
			}

			builder.AppendLine();

			using (builder.AppendBlock($"if ({member.Parameters[1].Name} < 0 || {member.Parameters[1].Name} + {items.Count()} >= {member.Parameters[0].Name}.Length)"))
			{
				builder.AppendLine($"throw new ArgumentOutOfRangeException(nameof({member.Parameters[1].Name}));");
			}

			builder.AppendLine();

			var index = 0;

			foreach (var item in items)
			{
				builder.AppendLine($"{member.Parameters[0].Name}[{member.Parameters[1].Name} + {index++}] = {SyntaxHelpers.CreateLiteral(item)};");
			}
		}
	}

	public static void AppendAdd(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("Add", m => m.Parameters.Length == 1, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock($"public void Add({member.Parameters[0].ToDisplayString()})"))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public static void AppendClear(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("Clear", m => m.Parameters.Length == 0, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock("public void Clear()"))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public static void AppendRemove(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("Remove", m => m.Parameters.Length == 1, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock($"public bool Remove({member.Parameters[0].ToDisplayString()})"))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public static void AppendIndexOf(ITypeSymbol typeSymbol, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("IndexOf", m => m.Parameters.Length == 1, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock($"public int IndexOf({member.Parameters[0].ToDisplayString()})"))
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

	public static void AppendInsert(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("Insert", m => m.Parameters.Length == 2, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock($"public void Insert({member.Parameters[0].ToDisplayString()}, {member.Parameters[1].ToDisplayString()})"))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public static void AppendRemoveAt(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("RemoveAt", m => m.Parameters.Length == 1, out var member))
		{
			return;
		}

		builder.AppendLine();

		using (builder.AppendBlock("public void RemoveAt(int index)"))
		{
			builder.AppendLine("throw new NotSupportedException(\"Collection is read-only.\");");
		}
	}

	public static void AppendContains(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("Contains", m => m.Parameters.Length == 1, out var member))
		{
			return;
		}

		items = items.Distinct().ToList();

		builder.AppendLine();

		using (builder.AppendBlock($"public bool Contains({member.Parameters[0].ToDisplayString()})"))
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

	public static void AppendToImmutableArray(ITypeSymbol typeSymbol, IList<object?> items, string elementName, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("ToImmutableArray", m => m.Parameters.Length == 0 
		                                                                     && m.ReturnType.Name == "ImmutableArray" 
		                                                                     && m.ReturnType.ContainingNamespace.ToString() == "System.Collections.Immutable", out var member))
		{
			return;
		}

		builder.AppendLine($$"""
			
			public ImmutableArray<{{elementName}}> ToImmutableArray()
			{
				return ImmutableArray.Create({{String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))}});
			}
			""");
	}
}