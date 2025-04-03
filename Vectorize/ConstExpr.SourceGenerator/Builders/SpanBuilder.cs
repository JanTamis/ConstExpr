using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
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
					var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

					if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
					{
						using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
						{
							if (vectorSize != items.Count)
							{
								builder.AppendLine($"var countVec = {vectorType}.Min({vectorType}.Create({member.Parameters[0].Name}.Length), {vector});");
							}
							else
							{
								builder.AppendLine($"var countVec = {vectorType}.Create({member.Parameters[0].Name}.Length);");
							}

							builder.AppendLine($"var otherVec = {vectorType}.LoadUnsafe(ref MemoryMarshal.GetReference({member.Parameters[0].Name}));");
							builder.AppendLine();

							if (compilation.HasMember<IPropertySymbol>(compilation.GetVectorType(vectorType, compilation.CreateInt32()), "Indices"))
							{
								builder.AppendLine($"var sequence = {vectorType}<{compilation.GetMinimalString(elementType)}>.Indices;");
							}
							else
							{
								builder.AppendLine($"var sequence = {vectorType}.Create({String.Join(", ", Enumerable.Range(0, vectorSize).Select(CreateLiteral))});");
							}

							builder.AppendLine($"var result = {vector};");
							builder.AppendLine();

							if (elementType.SpecialType != SpecialType.System_Int32)
							{
								builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, result).As{elementType.Name}() & {vectorType}.LessThan(sequence, countVec);");
							}
							else
							{
								builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, result) & {vectorType}.LessThan(sequence, countVec);");
							}

							if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "CountWhereAllBitsSet"))
							{
								builder.AppendLine();
								builder.AppendLine($"return {vectorType}.CountWhereAllBitsSet(mask);");
							}
							else
							{
								builder.AppendLine($"var matchBits = {vectorType}.ExtractMostSignificantBits(mask);");
								builder.AppendLine();
								builder.AppendLine("return BitOperations.PopCount(matchBits);");
							}
						}
					}

					builder.AppendLine();
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
				var isSequence = items.IsNumericSequence();
				var isZero = items[0] is 0 or 0L or (byte) 0 or (short) 0 or (sbyte) 0 or (ushort) 0 or (uint) 0 or (ulong) 0;
				var unsignedType = compilation.GetUnsignedType(elementType);
				var unsignedName = compilation.GetMinimalString(unsignedType);

				if (member.Parameters.Length > 1 && generationLevel == GenerationLevel.Performance)
				{
					var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

					if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
					{
						var vectorByteSize = vectorSize * elementSize;

						var elementName = compilation.GetMinimalString(elementType);
						var whiteSpace = new string(' ', 6);

						using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
						{
							if (items.Count == 1)
							{
								builder.AppendLine($"return {vectorType}.EqualsAny({vector}, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])});");
								return;
							}

							builder.AppendLine($"var input = {vector};");
							builder.AppendLine();

							if (isSequence)
							{
								if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
								{
									builder.AppendLine($"return {vectorType}.LessThanOrEqualAny(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({CreateLiteral(items[^1])}));");
								}
								else
								{
									builder.AppendLine($"return ({vectorType}.GreaterThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])}) & {vectorType}.LessThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[^1])})) != {vectorType}<{elementName}>.Zero;");
								}
							}
							else
							{
								var checks = member.Parameters
									.Select(s => $"{vectorType}.Equals(input, {vectorType}.Create({s.Name}))")
									.ToList();

								if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
								{
									builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet(");
									
									for (var i = 0; i < checks.Count; i++)
									{
										if (i == 0)
										{
											builder.AppendLine($"{whiteSpace}  {checks[i]}");
										}
										else if (i == checks.Count - 1)
										{
											builder.AppendLine($"{whiteSpace}| {checks[i]});");
										}
										else
										{
											builder.AppendLine($"{whiteSpace}| {checks[i]}");
										}
									}
								}
								else
								{
									builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementName}>.Zero;");
								}
							}
						}
						
						builder.AppendLine();
					}
				}

				if (typeSymbol.CheckMethod("Contains", compilation.CreateBoolean(), [ elementType ], out _))
				{
					var maxLength = member.Parameters.Max(m => m.Name.Length);
					
					builder.AppendLine($"return {String.Join($"\n{new string(' ', maxLength + "return ".Length - 11)}|| ", member.Parameters.Select(s => $"Contains({s.Name})"))};");
				}
				else if (generationLevel != GenerationLevel.Minimal)
				{
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
				else
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					
					switch (member.Parameters.Length)
					{
						case 1:
							builder.AppendLine($"\t.Contains({String.Join(", ", member.Parameters.Select(s => s.Name))});");
							break;
						case 2 or 3:
							builder.AppendLine($"\t.ContainsAny({String.Join(", ", member.Parameters.Select(s => s.Name))});");
							break;
						default:
							builder.AppendLine($"\t.ContainsAny([{String.Join(", ", member.Parameters.Select(s => s.Name))}]);");
							break;
					}
				}
			}
		}
	}
}