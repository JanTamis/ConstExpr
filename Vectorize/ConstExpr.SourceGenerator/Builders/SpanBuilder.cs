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
			using (AppendMethod(builder, member))
			{
				if (generationLevel != GenerationLevel.Minimal && elementType.SpecialType != SpecialType.None)
				{
					var elementSize = compilation.GetByteSize(loader, elementType);
					var size = elementSize * items.Count;

					switch (size)
					{
						case 0:
							break;
						case <= 8:
							AppendVector("Vector64", 8 / elementSize);
							builder.AppendLine();
							break;
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
				}

				if (!IsPerformance(generationLevel, items.Count))
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					builder.AppendLine($"\t.CommonPrefixLength({member.Parameters[0].Name});");
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
			for (var i = 0; i < items.Count; i++)
			{
				builder.AppendLine($"if ({method.Parameters[0].Name}.Length <= {CreateLiteral(i)} || {String.Format(comparerFormat, CreateLiteral(items[i]), $"{method.Parameters[0].Name}[{CreateLiteral(i)}]")}) return {CreateLiteral(i)};");
			}

			builder.AppendLine($"return {CreateLiteral(items.Count)};");
		}

		void AppendVector(string vectorType, int maxCount)
		{
			using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated && {vectorType}<{compilation.GetMinimalString(elementType)}>.IsSupported)"))
			{
				if (maxCount != items.Count)
				{
					builder.AppendLine($"var countVec = {vectorType}.Min({vectorType}.Create({member.Parameters[0].Name}.Length), {compilation.GetCreateVector(vectorType, elementType, loader, items)});");
				}
				else
				{
					builder.AppendLine($"var countVec = {vectorType}.Create({member.Parameters[0].Name}.Length);");
				}

				builder.AppendLine($"var otherVec = {vectorType}.LoadUnsafe(ref MemoryMarshal.GetReference({member.Parameters[0].Name}));");
				builder.AppendLine();

				if (compilation.HasMember<IPropertySymbol>(compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType}`1").Construct(compilation.CreateInt32()), "Indices"))
				{
					builder.AppendLine($"var sequence = {vectorType}<{compilation.GetMinimalString(elementType)}>.Indices;");
				}
				else
				{
					builder.AppendLine($"var sequence = {vectorType}.Create({String.Join(", ", Enumerable.Range(0, maxCount).Select(CreateLiteral))});");
				}

				builder.AppendLine($"var result = {compilation.GetCreateVector(vectorType, elementType, loader, items)};");
				builder.AppendLine();

				if (elementType.SpecialType != SpecialType.System_Int32)
				{
					builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, result).As{elementType.Name}() & {vectorType}.LessThan(sequence, countVec);");
				}
				else
				{
					builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, result) & {vectorType}.LessThan(sequence, countVec);");
				}

				builder.AppendLine($"var matchBits = {vectorType}.ExtractMostSignificantBits(mask);");
				builder.AppendLine();
				builder.AppendLine("return BitOperations.PopCount(matchBits);");
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
				items = items.Distinct().OrderBy(o => o).ToList();

				var elementSize = compilation.GetByteSize(loader, member.Parameters[0].Type);
				var size = elementSize * member.Parameters.Length;
				var isSequence = items.IsNumericSequence();
				var isZero = items[0] is 0 or 0L or (byte) 0 or (short) 0 or (sbyte) 0 or (ushort) 0 or (uint) 0 or (ulong) 0;
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
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} == {CreateLiteral(items[0])}");
				}

				else if (isSequence)
				{
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is >= {CreateLiteral(items[0])} and <= {CreateLiteral(items[^1])}");

					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						checks = member.Parameters.Select(s => $"({unsignedName}){s.Name}{new string(' ', maxLength - s.Name.Length)} <= {CreateLiteral(items[^1])}");
					}
				}
				else
				{
					checks = member.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is {String.Join(" or ", items.Select(CreateLiteral))}");
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
					builder.AppendLine($"return {vectorType}.EqualsAny({GetInputVector(vectorType, vectorSize)}, {compilation.GetCreateVector(vectorType, elementType, loader, items[0])});");
					return;
				}

				builder.AppendLine($"var input = {GetInputVector(vectorType, vectorSize)};");
				builder.AppendLine();

				if (isSequence)
				{
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						builder.AppendLine($"return {vectorType}.LessThanOrEqualAny(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({CreateLiteral(items[^1])}));");
					}
					else
					{
						builder.AppendLine($"return ({vectorType}.GreaterThanOrEqual(input, {compilation.GetCreateVector(vectorType, elementType, loader, items[0])}) & {vectorType}.LessThanOrEqual(input, {compilation.GetCreateVector(vectorType, elementType, loader, items[^1])})) != {vectorType}<{elementName}>.Zero;");
					}

				}
				else
				{
					var checks = items.Select(s => $"{vectorType}.Equals(input, {compilation.GetCreateVector(vectorType, elementType, loader, s)})");

					builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementName}>.Zero;");
				}
			}
		}

		string GetInputVector(string vectorType, int vectorSize)
		{
			if (member.Parameters.Length == 1)
			{
				return compilation.GetCreateVector(vectorType, elementType, loader, items);
			}

			return $"{vectorType}.Create({String.Join(", ", member.Parameters.Select(p => p.Name).Repeat(vectorSize))})";
		}
	}
}