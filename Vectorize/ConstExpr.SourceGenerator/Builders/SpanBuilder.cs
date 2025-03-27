using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class SpanBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType) : BaseBuilder(elementType, compilation)
{
	public void AppendCommonPrefixLength(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMembers<IMethodSymbol>("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                                      && m.Parameters.Length == 1
		                                                                      && compilation.IsSpanType(m.Parameters[0].Type, elementType), out var member))
		{
			if (elementType.SpecialType != SpecialType.None)
			{
				Append(member, "{1} != {0}");
			}
			else
			{
				Append(member, $"!EqualityComparer<{compilation.GetMinimalString(elementType)}>.Default.Equals({{0}}, {{1}})");
			}
			
		}

		if (typeSymbol.CheckMembers("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                       && m.Parameters.Length == 2
		                                                       && compilation.IsSpanType(m.Parameters[0].Type, elementType)
		                                                       && IsEqualSymbol(m.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)), out member))
		{
			Append(member, $"!{member.Parameters[1].Name}.Equals({{0}}, {{1}})");
		}

		void Append(IMethodSymbol method, string comparerFormat)
		{
			using (AppendMethod(builder, method))
			{
				for (var i = 0; i < items.Count; i++)
				{
					builder.AppendLine($"if ({method.Parameters[0].Name}.Length == {CreateLiteral(i)} || {String.Format(comparerFormat, CreateLiteral(items[i]), $"{method.Parameters[0].Name}[{CreateLiteral(i)}]")}) return {CreateLiteral(i)};");
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
				items = items.Distinct().ToList();

				var elementSize = compilation.GetByteSize(loader, member.Parameters[0].Type);
				var size = elementSize * member.Parameters.Length;

				switch (size)
				{
					case 0:
						break;
					// case <= 8:
					// 	AppendVector("Vector64", 8 / elementSize);
					// 	builder.AppendLine();
					// 	break;
					case <= 16:
						AppendVector("Vector128", 16 / elementSize);
						builder.AppendLine();
						break;
					case <= 32:
						AppendVector("Vector256", 32 / elementSize);
						builder.AppendLine();
						break;
					case <= 64:
						AppendVector("Vector512", 64 / elementSize);
						builder.AppendLine();
						break;
				}
				
				var checks = items.Distinct().Select(s => String.Join(" || ", member.Parameters.Select(p => $"{p.Name} == {CreateLiteral(s)}")));
				
				builder.AppendLine($"return {String.Join("\n\t|| ", checks)};");
			}
		}

		void AppendVector(string vectorType, int vectorSize)
		{
			var elementName = compilation.GetMinimalString(elementType);
			
			using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated && {vectorType}<{elementName}>.IsSupported)"))
			{
				var checks = items.Select(s => $"{vectorType}.Equals(input, {vectorType}.Create({CreateLiteral(s)}))");
				
				builder.AppendLine($"var input = {vectorType}.Create({String.Join(", ", member.Parameters.Select(p => p.Name).Repeat(vectorSize))});");
				builder.AppendLine();
				builder.AppendLine($"return ({String.Join("\n\t| ", checks)}) != {vectorType}<{elementName}>.Zero;");
			}
		}
	}
}