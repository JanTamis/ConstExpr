using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class SpanBuilder(Compilation compilation, ITypeSymbol elementType) : BaseBuilder(elementType, compilation)
{
	public void AppendCommonPrefixLength(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMembers<IMethodSymbol>("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                                      && m.Parameters.Length == 1
		                                                                      && compilation.IsSpanType(m.Parameters[0].Type, elementType), out var member))
		{
			Append(member, $"EqualityComparer<{compilation.GetMinimalString(elementType)}>.Default");
		}

		if (typeSymbol.CheckMembers("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                       && m.Parameters.Length == 2
		                                                       && compilation.IsSpanType(m.Parameters[0].Type, elementType)
		                                                       && IsEqualSymbol(m.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)), out member))
		{
			Append(member, member.Parameters[1].Name);
		}

		void Append(IMethodSymbol method, string comparerName)
		{
			using (AppendMethod(builder, method))
			{
				for (var i = 0; i < items.Count; i++)
				{
					if (i != 0)
					{
						builder.AppendLine();
					}

					// TODO check if condition is correct
					builder.AppendLine($"if ({method.Parameters[0].Name}.Length == {CreateLiteral(i)} || !{comparerName}.Equals({CreateLiteral(items[i])}, {method.Parameters[0].Name}[{i}]))");
					builder.AppendLine($"\treturn {CreateLiteral(i)};");
				}

				if (items.Count > 0)
				{
					builder.AppendLine();
				}

				builder.AppendLine($"return {CreateLiteral(items.Count)};");
			}
		}
	}

	public void AppendContainsAny(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMembers<IMethodSymbol>("ContainsAny", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Boolean)
		                                                               && m.Parameters.Length > 0
		                                                               && m.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)), out var member))
		{
			using (AppendMethod(builder, member))
			{
				var checks = items.Select(s => String.Join(" || ", member.Parameters.Select(p => $"{p.Name} == {CreateLiteral(s)}")));
				
				builder.AppendLine($"return {String.Join("\n\t|| ", checks)};");
			}
		}
	}
}