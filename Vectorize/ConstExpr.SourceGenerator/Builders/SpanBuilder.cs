using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class SpanBuilder(Compilation compilation, ITypeSymbol elementType) : BaseBuilder(elementType, compilation)
{
	public void AppendCommonPrefixLength(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod("CommonPrefixLength", compilation.CreateInt32(), [compilation.GetTypeByType(typeof(ReadOnlySpan<>), elementType)], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			for (var i = 0; i < items.Count; i++)
			{
				if (i != 0)
				{
					builder.AppendLine();
				}

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}.Length == {CreateLiteral(i + 1)} || !EqualityComparer<{compilation.GetMinimalString(elementType)}>.Default.Equals({CreateLiteral(items[i])}, {member.Parameters[0].Name}[{i}]))"))
				{
					builder.AppendLine($"return {CreateLiteral(i)};");
				}
			}

			if (items.Count > 0)
			{
				builder.AppendLine();
			}

			builder.AppendLine($"return {CreateLiteral(items.Count)};");
		}		
	}
}
