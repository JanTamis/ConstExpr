using System;
using System.Collections.Generic;
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

		if (member!.IsReadOnly)
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
			builder.AppendLine($"set => throw new NotSupportedException();");
		}
	}

	public static void AppendIsReadOnly(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>("IsReadOnly", m => m.Type.SpecialType == SpecialType.System_Boolean, out var member) || !member!.IsReadOnly)
		{
			return;
		}

		builder.AppendLine();

		if (member!.IsReadOnly)
		{
			builder.AppendLine("public bool IsReadOnly => true;");
		}
	}

	public static void AppendIndexer(ITypeSymbol typeSymbol, string typeName, IEnumerable<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IPropertySymbol>(m => m.IsIndexer && m.Parameters is [ { Type.SpecialType: SpecialType.System_Int32 } ], out var member))
		{
			return;
		}

		builder.AppendLine();

		if (member!.IsReadOnly)
		{
			builder.AppendLine($"public {typeName} this[int index] => index switch");
			{
				using (builder.AppendBlock("};"))
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
		}

		if (member.IsWriteOnly)
		{
			builder.AppendLine($"public {typeName} this[int index] => throw new NotSupportedException();");
			return;
		}

		using (builder.AppendBlock($"public {typeName} this[int index]"))
		{
			builder.AppendLine("get => index switch");
			{
				using (builder.AppendBlock("};"))
				{
					var index = 0;

					foreach (var item in items)
					{
						builder.AppendLine($"{index++} => {SyntaxHelpers.CreateLiteral(item)},");
					}

					builder.AppendLine("_ => throw new ArgumentOutOfRangeException(),");
				}
			}
			builder.AppendLine("set => throw new NotSupportedException();");
		}
	}

	public static void AppendCopyTo(ITypeSymbol typeSymbol, IEnumerable<object?> items, ITypeSymbol elementType, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("CopyTo", m => m.Parameters.Length == 2, out var member)
		    && (member.Parameters[0].Type.SpecialType == SpecialType.System_Array
		        || member.Parameters[0].Type.CheckMembers<IPropertySymbol>(m => m.IsIndexer && m is { IsReadOnly: false, Parameters: [ { Type.SpecialType: SpecialType.System_Int32 } ] }, out _)
		        && member.Parameters[1].Type.SpecialType == SpecialType.System_Int32))
		{
			return;
		}
		INamedTypeSymbol
	}
}