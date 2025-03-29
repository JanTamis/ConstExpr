using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Enums;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class SpanBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, int hashCode) : BaseBuilder(elementType, compilation, hashCode)
{
	public void AppendCommonPrefixLength(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMembers<IMethodSymbol>("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                                      && m.Parameters.Length == 1
		                                                                      && compilation.IsSpanType(m.Parameters[0].Type, elementType), out var member))
		{
			if (!IsPerformance(generationLevel, items.Count))
			{
				using (AppendMethod(builder, member))
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					builder.AppendLine($"\t.CommonPrefixLength({member.Parameters[0].Name});");
				}
			}
			else
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
		}

		if (typeSymbol.CheckMembers("CommonPrefixLength", m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32)
		                                                       && m.Parameters.Length == 2
		                                                       && compilation.IsSpanType(m.Parameters[0].Type, elementType)
		                                                       && IsEqualSymbol(m.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)), out member))
		{
			if (IsPerformance(generationLevel, items.Count))
			{
				using (AppendMethod(builder, member))
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}.CommonPrefixLength({member.Parameters[0].Name}, {member.Parameters[1].Name});");
				}
			}
			else
			{
				Append(member, $"!{member.Parameters[1].Name}.Equals({{0}}, {{1}})");
			}
			
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
				items = items.Distinct().OrderBy(o=> o).ToList();

				var elementSize = compilation.GetByteSize(loader, member.Parameters[0].Type);
				var size = elementSize * member.Parameters.Length;
				var isSequence = items.IsNumericSequence();
				var isZero = items[0] is 0 or 0L or (byte)0 or (short)0 or (sbyte)0 or (ushort)0 or (uint)0 or (ulong)0;
				var unsignedType = compilation.GetUnsignedType(elementType);
				var unsignedName = compilation.GetMinimalString(unsignedType);

				if (member.Parameters.Length > 1 && generationLevel == GenerationLevel.Performance)
				{
					switch (size)
					{
						case 0:
							break;
						case <= 8:
							AppendVector("Vector64", 8 / elementSize, isSequence, isZero, unsignedType, unsignedName);
							builder.AppendLine();
							break;
						case <= 16:
							AppendVector("Vector128", 16 / elementSize, isSequence, isZero, unsignedType, unsignedName);
							builder.AppendLine();
							break;
						case <= 32:
							AppendVector("Vector256", 32 / elementSize, isSequence, isZero, unsignedType, unsignedName);
							builder.AppendLine();
							break;
						case <= 64:
							AppendVector("Vector512", 64 / elementSize, isSequence, isZero, unsignedType, unsignedName);
							builder.AppendLine();
							break;
					}
				}
				
				var maxLength = member.Parameters.Max(m => m.Name.Length);

				IEnumerable<string> checks;

				if (items.Count == 1)
				{
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length + 1)} == {CreateLiteral(items[0])}");
				}

				else if (isSequence)
				{
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length + 1)}is >= {CreateLiteral(items[0])} and <= {CreateLiteral(items[^1])}");
					
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						checks = member.Parameters.Select(s => $"({unsignedName}){s.Name}{new string(' ', maxLength - s.Name.Length + 1)}<= {CreateLiteral(items[^1])}");
					}
				}
				else
				{
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length + 1)}is {String.Join(" or ", items.Select(CreateLiteral))}");
				}

				builder.AppendLine($"return {String.Join($"\n{new string(' ', maxLength + "return ".Length - 11)}|| ", checks)};");
			}
		}

		void AppendVector(string vectorType, int vectorSize, bool isSequence, bool isZero, ITypeSymbol unsignedType, string unsignedName)
		{
			var elementName = compilation.GetMinimalString(elementType);
			var whiteSpace = new string(' ', 6);
			
			using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated && {vectorType}<{elementName}>.IsSupported)"))
			{
				if (items.Count == 1)
				{
					builder.AppendLine($"return {vectorType}.EqualsAny({GetInputVector(vectorType, vectorSize)}, {vectorType}.Create({CreateLiteral(items[0])}));");
					return;
				}

				builder.AppendLine($"var input = {GetInputVector(vectorType, vectorSize)};");
				builder.AppendLine();
				
				if (isSequence)
				{
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						builder.AppendLine($"return {vectorType}.LessThanOrEqual(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({CreateLiteral(items[^1])})) != {vectorType}<{unsignedName}>.Zero;");
					}
					else
					{
						builder.AppendLine($"return ({vectorType}.GreaterThanOrEqual(input, {vectorType}.Create({CreateLiteral(items[0])})) & {vectorType}.LessThanOrEqual(input, {vectorType}.Create({CreateLiteral(items[^1])}))) != {vectorType}<{elementName}>.Zero;");
					}
					
				}
				else
				{
					var checks = items.Select(s => $"{vectorType}.Equals(input, {vectorType}.Create({CreateLiteral(s)}))");

					builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementName}>.Zero;");
				}
			}
		}

		string GetInputVector(string vectorType, int vectorSize)
		{
			if (member.Parameters.Length == 1)
			{
				return $"{vectorType}.Create({member.Parameters[0].Name})";
			}
			
			return $"{vectorType}.Create({String.Join(", ", member.Parameters.Select(p => p.Name).Repeat(vectorSize))})";
		}
	}
}