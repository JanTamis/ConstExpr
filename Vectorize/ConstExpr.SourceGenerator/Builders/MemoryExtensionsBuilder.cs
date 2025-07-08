using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class MemoryExtensionsBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, string dataName) : BaseBuilder(elementType, compilation, generationLevel, loader, dataName)
{
	public bool AppendBinarySearch<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "BinarySearch", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(elementType)
				     && elementType.HasMember<IMethodSymbol>("CompareTo", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
				                                                               && m.Parameters.AsSpan().EqualsTypes(elementType)):
			{
				items = items
					.OrderBy(o => o)
					.ToImmutableArray();

				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					BinarySearch(0, items.Length - 1, true, "{1}.CompareTo({0})");
				});

				return true;
			}
			case { Name: "BinarySearch", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
				when SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType)
				     && method.Parameters[1].Type.HasMember<IMethodSymbol>("Compare", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
				                                                                           && m.Parameters.AsSpan().EqualsTypes(elementType, elementType)):
			{
				items = items
					.OrderBy(o => o)
					.ToImmutableArray();

				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					BinarySearch(0, items.Length - 1, true, "{2}.Compare({1}, {0})");
				});

				return true;
			}

			default:
				return false;
		}

		void BinarySearch(int low, int high, bool isFirst, string compareFormat)
		{
			var set = new List<KeyValuePair<T, int>>();

			PrepareBinarySearch(low, high);

			// First build a decision tree structure
			var tree = BuildBinarySearchTree(0, set.Count - 1, set.FindIndex(f => f.Value == high / 2), set, TreeNode<T>.NodeState.None, null, items);

			// Then generate code from the tree
			GenerateCodeFromTree(builder, tree, compareFormat, isFirst, method, set);

			void PrepareBinarySearch(int tempLow, int tempHigh)
			{
				if (tempLow > tempHigh)
				{
					return;
				}

				var index = (int) ((uint) tempHigh + (uint) tempLow >> 1);
				var value = items[index];

				var result = new KeyValuePair<T, int>(value, index);

				var temp = set.BinarySearch(result, Comparer<KeyValuePair<T, int>>.Create((x, y) => Comparer<T>.Default.Compare(x.Key, y.Key)));

				if (temp < 0)
				{
					set.Insert(~temp, result);
				}

				PrepareBinarySearch(tempLow, index - 1);
				PrepareBinarySearch(index + 1, tempHigh);
			}
		}
	}

	public bool AppendCommonPrefixLength<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "CommonPrefixLength", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
				when compilation.IsSpanLikeType(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance && elementType.IsVectorSupported())
					{
						var cast = (LiteralString) (elementType.EqualsType(compilation.CreateInt32())
							? String.Empty
							: $"({elementType})");

						var byteSize = compilation.GetByteSize(loader, elementType);

						builder.AppendLine($$"""
							if (other.IsEmpty)
							{
								return 0;
							}

							if (Vector.IsHardwareAccelerated)
							{
								var position = 0;
								
								var indexes = Vector<{{elementType}}>.Indices;
								var lengthVector = Vector.Create(Math.Min({{cast}}other.Length, {{items.Length.ToSpecialType(elementType.SpecialType)}}));
								var countVector = Vector.Create({{cast}}Vector<{{elementType}}>.Count);

								while (true)
								{
									var thisVec = Vector.LoadUnsafe(ref MemoryMarshal.GetReference({{GetDataName()}}), (nuint)position);
									var otherVec = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(other), (nuint)position);

									var equalMask = Vector.Equals(thisVec, otherVec) & Vector.LessThan(indexes, lengthVector);

									if (equalMask != Vector<{{elementType}}>.AllBitsSet)
									{
										return position + Vector<{{elementType}}>.Count switch
										{
											{{16 / byteSize}} => BitOperations.TrailingZeroCount(~Vector128.ExtractMostSignificantBits(equalMask.AsVector128())),
											{{32 / byteSize}} => BitOperations.TrailingZeroCount(~Vector256.ExtractMostSignificantBits(equalMask.AsVector256())),
											{{64 / byteSize}} => BitOperations.TrailingZeroCount(~Vector512.ExtractMostSignificantBits(equalMask.AsVector512())),
											_ => {{GetDataName()}}
												.Slice(position)
												.CommonPrefixLength(other),
										};
									}

									position += Vector<{{elementType}}>.Count;
									indexes += countVector;
								}
							}
								
							return {{GetDataName()}}.CommonPrefixLength(other);
							""");
					}
					else
					{
						builder.AppendLine($"return {GetDataName()}.CommonPrefixLength(other);");
					}
				});

				return true;
			}
			case { Name: "CommonPrefixLength", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
				when compilation.IsSpanLikeType(method.Parameters[0].Type, elementType)
				     && IsEqualSymbol(method.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						builder.AppendLine($"return {GetDataName()}.CommonPrefixLength({method.Parameters});");
					}
					else
					{
						Append(method, $"!{method.Parameters[1]}.Equals({{0}}, {{1}})");
					}
				});

				return true;
			}
			default:
				return false;
		}

		// Helper method to append prefix length calculation logic
		void Append(IMethodSymbol method, string comparerFormat)
		{
			for (var i = 0; i < items.Length; i++)
			{
				builder.AppendLine($"if ({method.Parameters[0]}.Length <= {i} || {(LiteralString) String.Format(comparerFormat, CreateLiteral(items[i]), $"{method.Parameters[0].Name}[{CreateLiteral(i)}]")}) return {i};");
			}

			builder.AppendLine($"return {items.Length};");
		}
	}

	public bool AppendContainsAny<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 1 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				items = items
					.Distinct()
					.ToImmutableArray();

				if (items.Length == 0)
				{
					AppendMethod(builder, method, () =>
					{
						builder.AppendLine("return false;");
					});
				}
				else
				{
					AppendMethod(builder, method, items.AsSpan(), true, (vectorType, vectors, vectorSize) =>
					{
						foreach (var parameter in method.Parameters)
						{
							builder.AppendLine($"var {(LiteralString) parameter.Name}Vector = {vectorType}.Create({(LiteralString) parameter.Name});");
						}

						builder.AppendLine();

						var checks = method.Parameters
							.SelectMany(s => vectors
								.Select(x => $"{vectorType}.Equals({s.Name}Vector, {x})"));

						if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("AnyWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Boolean }))
						{
							builder.AppendLine(CreatePadding("|", $"return {vectorType}.AnyWhereAllBitsSet(", checks, false, false) + ");");
						}
						else
						{
							builder.AppendLine(CreatePadding("|", "return (", checks, false, false) + $") != {vectorType}<{elementType}>.Zero;");
						}
					}, isPerformance =>
					{
						if (method.ContainingType.HasMember<IMethodSymbol>("Contains", m => m is { ReturnType.SpecialType: SpecialType.System_Boolean }
						                                                                    && m.Parameters.AsSpan().EqualsTypes(elementType)))
						{
							var checks = method.Parameters
								.Select(s => $"Contains({s.Name})");

							builder.AppendLine(CreateReturnPadding("||", checks));
						}
						else if (method.Parameters.Length <= 3)
						{
							builder.AppendLine($"return {GetDataName()}.ContainsAny({method.Parameters});");
						}
						else
						{
							builder.AppendLine($"return {GetDataName()}.ContainsAny([{method.Parameters}]);");
						}
					});
				}

				return true;
			}
			default:
				return false;
		}
	}
	//
	// public bool AppendContainsAnyExcept<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 0 }
	// 			when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
	// 		{
	// 			AppendContainsAny(method.ContainingType, method, false, items, builder);
	//
	// 			return true;
	// 		}
	// 		case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 0 }
	// 			when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), isPerformance =>
	// 			{
	// 				var checks = items
	// 					.Select(s => $"{method.Parameters[0].Name}.Contains({CreateLiteral(s)})");
	//
	// 				builder.AppendLine(CreateReturnPadding("||", checks));
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	// }

	public bool AppendContainsAnyInRange<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		if (!elementType.EqualsType(compilation.GetTypeByMetadataName("System.IComparable`1")?.Construct(elementType)))
		{
			return false;
		}

		switch (method)
		{
			case { Name: "ContainsAnyInRange", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType, elementType):
			{
				items = items
					.Distinct()
					.OrderBy(o => o)
					.ToImmutableArray();

				if (items.AsSpan().IsNumericSequence())
				{
					AppendMethod(builder, method, () =>
					{
						builder.AppendLine($"return {method.Parameters[0]} <= {method.Parameters[1]} && {method.Parameters[0]} <= {items[^1]} && {method.Parameters[1]} >= {items[0]};");
					});

					return true;
				}

				AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
				{
					var checks = Enumerable.Range(0, vectors.Count)
						.Select(s => $"{vectorType}.GreaterThanOrEqual(vec{s}, {method.Parameters[0].Name}Vector) & {vectorType}.LessThanOrEqual(vec{s}, {method.Parameters[1].Name}Vector)");

					var remainingChecks = items.Skip(vectors.Count * vectorSize)
						.Select(s => $"{s} >= {method.Parameters[0].Name} && {s} <= {method.Parameters[1].Name}")
						.ToList();

					var hasRemainingChecks = remainingChecks.Any();

					for (var i = 0; i < vectors.Count; i++)
					{
						builder.AppendLine($"var vec{i} = {(LiteralString) vectors[i]};");
					}

					builder.AppendLine($$"""

						var {{method.Parameters[0]}}Vector = {{vectorType}}.Create({{method.Parameters[0]}});
						var {{method.Parameters[1]}}Vector = {{vectorType}}.Create({{method.Parameters[1]}});

						""");

					if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
					{
						var padding = $"return {vectorType}.AnyWhereAllBitsSet(";

						builder.AppendLine(CreatePadding("|", padding, checks, false, false) + (hasRemainingChecks ? ")" : ");"));

						if (hasRemainingChecks)
						{
							builder.AppendLine(CreatePadding("||", new string(' ', padding.Length - 3) + "||", remainingChecks));
						}
					}
					else
					{
						const string padding = "return (";

						builder.AppendLine(CreatePadding("|", padding, checks, false, false) + $") != {vectorType}<{elementType}>.Zero" + (hasRemainingChecks ? String.Empty : ";"));

						if (hasRemainingChecks)
						{
							builder.AppendLine(CreatePadding("||", new string(' ', padding.Length - 3) + "||", remainingChecks));
						}
					}
				}, _ =>
				{
					if (compilation.HasMember<IMethodSymbol>(compilation.GetTypeByMetadataName("System.MemoryExtensions"), "ContainsAnyInRange"))
					{
						builder.AppendLine($"return {GetDataName()}.ContainsAnyInRange({method.Parameters});");
					}
					else
					{
						builder.AppendLine($$"""
							foreach (var item in {{GetDataName()}})
							{
								if (item >= {{method.Parameters[0]}} && item <= {{method.Parameters[1]}})
								{
									return true;
								}
							}

							return false;
							""");
					}

					// var checks = items
					// 	.Select(s => $"{s} >= {method.Parameters[0].Name} && {s} <= {method.Parameters[1].Name}");
					//
					// builder.AppendLine(CreateReturnPadding("||", checks));
				});

				return true;
			}
			default:
				return false;
		}
	}

	// public bool AppendContainsAnyExceptInRange<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "ContainsAnyExceptInRange", ReturnType.SpecialType: SpecialType.System_Boolean }
	// 			when method.Parameters.AsSpan().EqualsTypes(elementType, elementType):
	// 		{
	// 			items = items
	// 				.Distinct()
	// 				.OrderBy(o => o)
	// 				.ToImmutableArray();
	//
	// 			AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
	// 			{
	// 				builder.AppendLine($"var items = {(LiteralString) vectors};");
	// 				builder.AppendLine();
	//
	// 				if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
	// 				{
	// 					builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet({vectorType}.LessThan(items, {vectorType}.Create({method.Parameters[0]})) | {vectorType}.GreaterThan(items, {vectorType}.Create({method.Parameters[1]}));");
	// 				}
	// 				else
	// 				{
	// 					builder.AppendLine($"return ({vectorType}.LessThan(items, {vectorType}.Create({method.Parameters[0]})) | {vectorType}.GreaterThan(items, {vectorType}.Create({method.Parameters[1]}))) != {vectorType}<{elementType}>.Zero;");
	// 				}
	// 			}, _ =>
	// 			{
	// 				var checks = items
	// 					.Select(s => $"{s} < {method.Parameters[0]} || {s} > {method.Parameters[1]}");
	//
	// 				builder.AppendLine(CreateReturnPadding("||", checks));
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	// }

	public bool AppendCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Count", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
						{
							foreach (var count in items.CountBy(x => x).GroupBy(g => g.Value))
							{
								builder.AppendLine($"{(LiteralString) String.Join(" or ", count.Select(s => CreateLiteral(s.Key)))} => {count.Key},");
							}

							builder.AppendLine("_ => 0,");
						}
					}
					else if (compilation.HasMember<IMethodSymbol>(compilation.GetTypeByMetadataName("System.MemoryExtensions"), "Count"))
					{
						builder.AppendLine($"return {GetDataName()}.Count({method.Parameters[0]});");
					}
					else
					{
						builder.AppendLine($$"""
							var result = 0;

							foreach (var item in {{GetDataName()}})
							{
								if (item == {{method.Parameters[0]}})
								{
									result++;
								}
							}

							return result;
							""");
					}
				});

				return true;
			}
			case { Name: "Count", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
				when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					var checks = items.Select(s => $"{method.Parameters[0].Name}.Contains({CreateLiteral(s)}) ? 1 : 0");

					builder.AppendLine(CreateReturnPadding("+", checks));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendEndsWith<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "EndsWith", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 0 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						var checks = method.Parameters
							.Index()
							.Select(s => $"{s.Value.Name} == {items[^(s.Index + 1)]}")
							.Take(items.Length)
							.Reverse();

						builder.AppendLine(CreateReturnPadding("&&", checks));
					}
					else
					{
						builder.AppendLine($"return {GetDataName()}.EndsWith({method.Parameters});");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendEnumerateLines(IMethodSymbol method, string? data, IndentedStringBuilder builder)
	{
		data ??= String.Empty;

		switch (method)
		{
			case { Name: "EnumerateLines" }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(compilation.CreateString())):
			{
				AppendMethod(builder, method, data.AsSpan(), isPerformance =>
				{
					var remaining = data.AsSpan();

					while (!remaining.IsEmpty)
					{
						var idx = remaining.IndexOfAny("\n\r\f\u0085\u2028\u2029".AsSpan());

						if ((uint) idx < (uint) remaining.Length)
						{
							var stride = 1;

							if (remaining[idx] == '\r' && (uint) (idx + 1) < (uint) remaining.Length && remaining[idx + 1] == '\n')
							{
								stride = 2;
							}

							var current = remaining.Slice(0, idx);

							builder.AppendLine($"yield return {current.ToString()};");

							remaining = remaining.Slice(idx + stride);
						}
						else
						{
							builder.AppendLine($"yield return {remaining.ToString()};");

							break;
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendEnumerableRunes(IMethodSymbol method, string? data, IndentedStringBuilder builder)
	{
		if (data is null)
		{
			return false;
		}

		switch (method)
		{
			case { Name: "EnumerateRunes" }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(compilation.GetTypeByMetadataName("System.Text.Rune"))):
			{
				AppendMethod(builder, method, data.AsSpan(), isPerformance =>
				{
					var span = data.AsSpan();

					while (TryDecodeFromUtf16(span, out var result, out var charsConsumed))
					{
						builder.AppendLine($"yield return new Rune({result}); \t// {(LiteralString) span.Slice(0, charsConsumed).ToString()}");
						span = span.Slice(charsConsumed);
					}
				});

				return true;
			}
			default:
				return false;
		}

		bool TryDecodeFromUtf16(ReadOnlySpan<char> source, out uint result, out int charsConsumed)
		{
			if (source.Length == 0)
			{
				result = 0;
				charsConsumed = 0;
				return false;
			}

			var first = source[0];

			if (first < 0xD800 || first > 0xDFFF)
			{
				// Single UTF-16 code unit
				result = first;
				charsConsumed = 1;
				return true;
			}

			if (first > 0xDBFF || source.Length < 2)
			{
				// Invalid surrogate or insufficient data
				result = 0;
				charsConsumed = 0;
				return false;
			}

			var second = source[1];

			if (second < 0xDC00 || second > 0xDFFF)
			{
				// Invalid trailing surrogate
				result = 0;
				charsConsumed = 0;
				return false;
			}

			// Valid surrogate pair
			result = 0x10000u + ((uint) (first - 0xD800) << 10) + (uint) (second - 0xDC00);
			charsConsumed = 2;
			return true;
		}
	}

	public bool AppendIsWhiteSpace(IMethodSymbol method, string? data, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "IsWhiteSpace", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 0 }:
			{
				AppendMethod(builder, method, data.AsSpan(), isPerformance =>
				{
					builder.AppendLine($"return {String.IsNullOrWhiteSpace(data)};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	// public bool AppendIndexOfAny<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "IndexOfAny", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: > 1 }
	// 			when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
	// 			{
	// 				builder.AppendLine($"var vector = {(LiteralString) vectors};");
	// 				builder.AppendLine();
	//
	// 				builder.AppendLine(CreatePadding("|", "var resultVector =", method.Parameters.Select(s => $"{vectorType}.Equals(vector, {vectorType}.Create({s.Name}))")));
	//
	// 				builder.AppendLine();
	//
	// 				if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("IndexOfWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }))
	// 				{
	// 					builder.AppendLine($"return {vectorType}.IndexOfWhereAllBitsSet(resultVector);");
	// 				}
	// 				else
	// 				{
	// 					using (builder.AppendBlock($"if (resultVector == {vectorType}<{elementType}>.Zero)"))
	// 					{
	// 						builder.AppendLine($"return {-1};");
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine($"return BitOperations.TrailingZeroCount({vectorType}.ExtractMostSignificantBits(resultVector));");
	// 				}
	//
	// 			}, isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
	// 					                                                                   && m.Parameters.AsSpan().EqualsTypes(elementType)))
	// 					{
	// 						foreach (var (index, parameter) in method.Parameters.Index())
	// 						{
	// 							builder.AppendLine($"var index{index} = IndexOf({parameter.Name});");
	// 						}
	//
	// 						builder.AppendLine();
	// 						builder.AppendLine($"var result = Math.Min((uint)index0, {CreateMinChain(1, method.Parameters.Length)});");
	// 						builder.AppendLine();
	// 						builder.AppendLine("return Unsafe.BitCast<uint, int>(result);");
	// 					}
	// 					else
	// 					{
	// 						var values = new HashSet<object?>();
	//
	// 						foreach (var (index, item) in items.Index())
	// 						{
	// 							if (values.Add(item))
	// 							{
	// 								var checks = method.Parameters
	// 									.Select(s => $"{s.Name} == {item}");
	//
	// 								builder.AppendLine($"if ({(LiteralString) String.Join(" || ", checks)}) return {index};");
	// 							}
	// 						}
	//
	// 						builder.AppendLine($"return {-1};");
	// 					}
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {(LiteralString) collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({(LiteralString) String.Join(" || ", method.Parameters.Select(s => $"{s.Name} == {collectionName}[i]"))})"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	//
	// 		case { Name: "IndexOfAny", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
	// 			when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					for (var i = 0; i < items.Length; i++)
	// 					{
	// 						using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({items[i]}))"))
	// 						{
	// 							builder.AppendLine($"return {i};");
	// 						}
	//
	// 						builder.AppendLine();
	// 					}
	//
	// 					builder.AppendLine($"return {-1};");
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {(LiteralString) collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({(LiteralString) collectionName}[i]))"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	//
	// 	string CreateMinChain(int index, int count)
	// 	{
	// 		if (index == count - 1)
	// 		{
	// 			return $"(uint)index{index}";
	// 		}
	//
	// 		return $"Math.Min((uint)index{index}, {CreateMinChain(index + 1, count)})";
	// 	}
	// }

	// public bool AppendIndexOfAnyExcept<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "IndexOfAnyExcept", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: > 1 }
	// 			when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
	// 			{
	// 				builder.AppendLine($"var vector = {(LiteralString) vectors};");
	// 				builder.AppendLine();
	//
	// 				builder.AppendLine(CreatePadding("&", "var resultVector =", method.Parameters.Select(s => $"{vectorType}.Negate({vectorType}.Equals(vector, {vectorType}.Create({s.Name})))")));
	//
	// 				builder.AppendLine();
	//
	// 				if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("IndexOfWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }))
	// 				{
	// 					builder.AppendLine($"return {vectorType}.IndexOfWhereAllBitsSet(resultVector);");
	// 				}
	// 				else
	// 				{
	// 					using (builder.AppendBlock($"if (resultVector == {vectorType}<{elementType}>.Zero)"))
	// 					{
	// 						builder.AppendLine($"return {-1};");
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine($"return BitOperations.TrailingZeroCount({vectorType}.ExtractMostSignificantBits(resultVector));");
	// 				}
	//
	// 			}, isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					var values = new HashSet<object?>();
	//
	// 					foreach (var (index, item) in items.Index())
	// 					{
	// 						if (values.Add(item))
	// 						{
	// 							var checks = method.Parameters
	// 								.Select(s => $"{s.Name} != {item}");
	//
	// 							builder.AppendLine($"if ({(LiteralString) String.Join(" && ", checks)}) return {index};");
	// 						}
	// 					}
	//
	// 					builder.AppendLine($"return {-1};");
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {(LiteralString) collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({(LiteralString) String.Join(" && ", method.Parameters.Select(s => $"{s.Name} != {collectionName}[i]"))})"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	//
	// 		case { Name: "IndexOfAnyExcept", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
	// 			when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					for (var i = 0; i < items.Length; i++)
	// 					{
	// 						using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({items[i]}))"))
	// 						{
	// 							builder.AppendLine($"return {i};");
	// 						}
	//
	// 						builder.AppendLine();
	// 					}
	//
	// 					builder.AppendLine("return -1;");
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {(LiteralString) collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({(LiteralString) collectionName}[i]))"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	// }

	// public bool AppendIndexOfAnyExceptInRange<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "IndexOfAnyExceptInRange", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
	// 			when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
	// 			{
	// 				builder.AppendLine($"var vector = {(LiteralString) vectors};");
	// 				builder.AppendLine($"var {(LiteralString) method.Parameters[0].Name}Vector = {vectorType}.Create({(LiteralString) method.Parameters[0].Name});");
	// 				builder.AppendLine($"var {(LiteralString) method.Parameters[1].Name}Vector = {vectorType}.Create({(LiteralString) method.Parameters[1].Name});");
	// 				builder.AppendLine();
	// 				builder.AppendLine($"var resultVector = {vectorType}.LessThan(vector, {(LiteralString) method.Parameters[0].Name}Vector) | {vectorType}.GreaterThan(vector, {(LiteralString) method.Parameters[1].Name}Vector);");
	// 				builder.AppendLine();
	//
	// 				if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("IndexOfWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }))
	// 				{
	// 					builder.AppendLine($"return {vectorType}.IndexOfWhereAllBitsSet(resultVector);");
	// 				}
	// 				else
	// 				{
	// 					using (builder.AppendBlock($"if (resultVector == {vectorType}<{elementType}>.Zero)"))
	// 					{
	// 						builder.AppendLine($"return {-1};");
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine($"return BitOperations.TrailingZeroCount({vectorType}.ExtractMostSignificantBits(resultVector));");
	// 				}
	//
	// 			}, isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					var values = new HashSet<object?>();
	//
	// 					foreach (var (index, item) in items.Index())
	// 					{
	// 						if (values.Add(item))
	// 						{
	// 							builder.AppendLine($"if ({item} < {(LiteralString) method.Parameters[0].Name} || {item} > {(LiteralString) method.Parameters[1].Name}) return {index};");
	// 						}
	// 					}
	//
	// 					builder.AppendLine($"return -1;");
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({(LiteralString) collectionName}[i] < {(LiteralString) method.Parameters[0].Name} || {(LiteralString) collectionName}[i] > {(LiteralString) method.Parameters[1].Name})"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	// }

	// public bool AppendIndexOfAnyInRange<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "IndexOfAnyInRange", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
	// 			when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, vectorSize) =>
	// 			{
	// 				builder.AppendLine($"var vector = {(LiteralString) vectors};");
	// 				builder.AppendLine($"var {(LiteralString) method.Parameters[0].Name}Vector = {vectorType}.Create({(LiteralString) method.Parameters[0].Name});");
	// 				builder.AppendLine($"var {(LiteralString) method.Parameters[1].Name}Vector = {vectorType}.Create({(LiteralString) method.Parameters[1].Name});");
	// 				builder.AppendLine();
	// 				builder.AppendLine($"var resultVector = {vectorType}.GreaterThanOrEqual(vector, {(LiteralString) method.Parameters[0].Name}Vector) & {vectorType}.LessThanOrEqual(vector, {(LiteralString) method.Parameters[1].Name}Vector);");
	// 				builder.AppendLine();
	//
	// 				if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("IndexOfWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }))
	// 				{
	// 					builder.AppendLine($"return {vectorType}.IndexOfWhereAllBitsSet(resultVector);");
	// 				}
	// 				else
	// 				{
	// 					using (builder.AppendBlock($"if (resultVector == {vectorType}<{elementType}>.Zero)"))
	// 					{
	// 						builder.AppendLine($"return {-1};");
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine($"return BitOperations.TrailingZeroCount({vectorType}.ExtractMostSignificantBits(resultVector));");
	// 				}
	//
	// 			}, isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					var values = new HashSet<object?>();
	//
	// 					foreach (var (index, item) in items.Index())
	// 					{
	// 						if (values.Add(item))
	// 						{
	// 							builder.AppendLine($"if ({item} >= {(LiteralString) method.Parameters[0].Name} && {item} <= {(LiteralString) method.Parameters[1].Name}) return {index};");
	// 						}
	// 					}
	//
	// 					builder.AppendLine($"return -1;");
	// 				}
	// 				else
	// 				{
	// 					var collectionName = GetDataName(method.ContainingType);
	//
	// 					using (builder.AppendBlock($"for (var i = 0; i < {(LiteralString) collectionName}.Length; i++)"))
	// 					{
	// 						using (builder.AppendBlock($"if ({(LiteralString) collectionName}[i] >= {(LiteralString) method.Parameters[0].Name} && {(LiteralString) collectionName}[i] <= {(LiteralString) method.Parameters[1].Name})"))
	// 						{
	// 							builder.AppendLine("return i;");
	// 						}
	// 					}
	//
	// 					builder.AppendLine();
	// 					builder.AppendLine("return -1;");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	// }

	public bool AppendReplace<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		if (!elementType.EqualsType(compilation.GetTypeByMetadataName("System.IEquatable`1")?.Construct(elementType)))
		{
			return false;
		}

		switch (method)
		{
			case { Name: "Replace", ReturnsVoid: true, Parameters.Length: 3 }
				when compilation.IsReadonlySpanType(method.Parameters[0].Type, elementType) && method.Parameters.AsSpan(1, method.Parameters.Length - 1).EqualsTypes(elementType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), false, () =>
				{
					if (compilation.HasMember<IMethodSymbol>(typeof(ArgumentOutOfRangeException), "ThrowIfLessThan"))
					{
						builder.AppendLine($"ArgumentOutOfRangeException.ThrowIfLessThan((uint){method.Parameters[0]}.Length, {(uint) items.Length});");
					}
					else
					{
						using (builder.AppendBlock($"if ((uint){method.Parameters[0]}.Length < {(uint) items.Length})"))
						{
							builder.AppendLine($"throw new ArgumentOutOfRangeException(nameof({method.Parameters[0]}), \"The length of the span is less than {items.Length}\");");
						}
					}

					builder.AppendLine();
				}, (vectorType, vectors, size) =>
				{
					var index = vectors.Count * size;

					builder.AppendLine($"""
						ref var {method.Parameters[0]}Reference = ref MemoryMarshal.GetReference({method.Parameters[0]});

						var {method.Parameters[1]}Vector = {vectorType}.Create({method.Parameters[1]});
						var {method.Parameters[2]}Vector = {vectorType}.Create({method.Parameters[2]});

						""");

					for (var i = 0; i < vectors.Count; i++)
					{
						builder.AppendLine($"var vec{i} = {(LiteralString) vectors[i]};");
					}

					builder.AppendLine();

					for (var i = 0; i < vectors.Count; i++)
					{
						if (i == 0)
						{
							builder.AppendLine($"{vectorType}.ConditionalSelect({vectorType}.Equals(vec{i}, {method.Parameters[1]}Vector), {method.Parameters[2]}Vector, vec{i}).StoreUnsafe(ref {method.Parameters[0]}Reference);");
						}
						else
						{
							builder.AppendLine($"{vectorType}.ConditionalSelect({vectorType}.Equals(vec{i}, {method.Parameters[1]}Vector), {method.Parameters[2]}Vector, vec{i}).StoreUnsafe(ref {method.Parameters[0]}Reference, {i * size});");
						}
					}

					if (index < items.Length)
					{
						builder.AppendLine();
					}

					for (; index < items.Length; index++)
					{
						builder.AppendLine($"{method.Parameters[0]}[{index}] = {method.Parameters[1]} == {items[index]} ? {method.Parameters[2]} : {items[index]};");
					}

					builder.AppendLine();
					builder.AppendLine("return;");
				}, isPerformance =>
				{
					if (compilation.HasMember<IMethodSymbol>(compilation.GetTypeByMetadataName("System.MemoryExtensions"), "Replace"))
					{
						builder.AppendLine($"{GetDataName()}.Replace({method.Parameters});");
					}
					else
					{
						using (builder.AppendBlock($"for (var i = 0; i < {method.Parameters[0]}.Length; i++)"))
						{
							if (elementType.SpecialType != SpecialType.None)
							{
								builder.AppendLine($"{method.Parameters[0]}[i] = {GetDataName()}[i] == {method.Parameters[1]} ? {method.Parameters[2]} : {GetDataName()}[i];");
							}
							else
							{
								builder.AppendLine($"{method.Parameters[0]}[i] = EqualityComparer<{elementType}>.Default.Equals({GetDataName()}[i], {method.Parameters[1]}) ? {method.Parameters[2]} : {GetDataName()}[i];");
							}
						}
					}
				});

				return true;
			}
		}

		return false;
	}

	// public bool AppendSequenceCompareTo<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	switch (method)
	// 	{
	// 		case { Name: "SequenceCompareTo", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
	// 			when compilation.IsSpanLikeType(method.Parameters[0].Type, elementType)
	// 			     && elementType.EqualsType(compilation.GetTypeByMetadataName("System.IComparable`1").Construct(elementType)):
	// 		{
	// 			AppendMethod(builder, method, items.AsSpan(), isPerformance =>
	// 			{
	// 				if (isPerformance)
	// 				{
	// 					builder.AppendLine("var cmp = 0;");
	// 					builder.AppendLine();
	//
	// 					for (var i = 0; i < items.Length; i++)
	// 					{
	// 						builder.AppendLine($"if ({(LiteralString) method.Parameters[0].Name}.Length > {i} && (cmp = {items[i]}.CompareTo({(LiteralString) method.Parameters[0].Name}[{i}])) != 0) return cmp;");
	// 					}
	//
	// 					if (items.Any())
	// 					{
	// 						builder.AppendLine();
	// 					}
	//
	// 					builder.AppendLine($"return {items.Length}.CompareTo({(LiteralString) method.Parameters[0].Name}.Length);");
	// 				}
	// 				else
	// 				{
	// 					builder.AppendLine($"return {(LiteralString) GetDataName(method.ContainingType)}");
	// 					builder.AppendLine($"\t.SequenceCompareTo({method.Parameters});");
	// 				}
	// 			});
	//
	// 			return true;
	// 		}
	// 		default:
	// 			return false;
	// 	}
	//
	// 	void AppendItems(int index)
	// 	{
	// 		if (index == 0)
	// 		{
	// 			using (builder.AppendBlock($"if ({(LiteralString) method.Parameters[0].Name}.Length > {index})"))
	// 			{
	// 				builder.AppendLine($"result = {items[index]}.CompareTo({(LiteralString) method.Parameters[0].Name}[{index}]);");
	//
	// 				if (index + 1 < items.Length)
	// 				{
	// 					builder.AppendLine();
	// 					AppendItems(index + 1);
	// 				}
	// 			}
	// 		}
	// 		else
	// 		{
	// 			using (builder.AppendBlock($"if (result == 0 && {(LiteralString) method.Parameters[0].Name}.Length > {index})"))
	// 			{
	// 				builder.AppendLine($"result = {items[index]}.CompareTo({(LiteralString) method.Parameters[0].Name}[{index}]);");
	//
	// 				if (index + 1 < items.Length)
	// 				{
	// 					builder.AppendLine();
	// 					AppendItems(index + 1);
	// 				}
	// 			}
	// 		}
	//
	// 		using (builder.AppendBlock("else"))
	// 		{
	// 			builder.AppendLine("return result;");
	// 		}
	// 	}
	// }

	// private void AppendContainsAny<T>(ITypeSymbol typeSymbol, IMethodSymbol method, bool result, ImmutableArray<T> items, IndentedStringBuilder builder)
	// {
	// 	var prefix = result ? String.Empty : "!";
	//
	// 	// Prepare items
	// 	items = items
	// 		.Distinct()
	// 		.OrderBy(o => o)
	// 		.ToImmutableArray();
	//
	// 	var elementSize = compilation.GetByteSize(loader, method.Parameters[0].Type);
	// 	var isSequence = items.AsSpan().IsNumericSequence();
	// 	var isZero = items[0] is 0 or 0L or (byte) 0 or (short) 0 or (sbyte) 0 or (ushort) 0 or (uint) 0 or (ulong) 0;
	// 	var unsignedType = compilation.GetUnsignedType(elementType);
	// 	var unsignedName = compilation.GetMinimalString(unsignedType);
	//
	// 	AppendMethod(builder, method, items.AsSpan(), false, (vectorType, vectors, size) =>
	// 	{
	// 		var whiteSpace = new string(' ', 6);
	//
	// 		// Special case for single item
	// 		if (items.Length == 1)
	// 		{
	// 			builder.AppendLine($"return {prefix}{vectorType}.EqualsAny({(LiteralString) vectors}, {compilation.GetCreateVector(vectorType, elementType, loader, false, items[0])});");
	// 			return;
	// 		}
	//
	// 		builder.AppendLine($"var input = {(LiteralString) vectors};");
	// 		builder.AppendLine();
	//
	// 		// Handle sequential items differently
	// 		if (isSequence)
	// 		{
	// 			if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
	// 			{
	// 				builder.AppendLine($"return {prefix}{vectorType}.LessThanOrEqualAny(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({items[^1]}));");
	// 			}
	// 			else
	// 			{
	// 				builder.AppendLine(
	// 					$"return {prefix}({vectorType}.GreaterThanOrEqual(input, {compilation.GetCreateVector(vectorType, elementType, loader, false, items[0])}) & " +
	// 					$"{vectorType}.LessThanOrEqual(input, {compilation.GetCreateVector(vectorType, elementType, loader, false, items[^1])})) != {vectorType}<{elementType}>.Zero;");
	// 			}
	// 		}
	// 		else
	// 		{
	// 			var checks = method.Parameters
	// 				.Select(s => $"{vectorType}.Equals(input, {vectorType}.Create({s.Name}))")
	// 				.ToList();
	//
	// 			if (result && compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
	// 			{
	// 				builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet(");
	//
	// 				for (var i = 0; i < checks.Count; i++)
	// 				{
	// 					if (i == 0)
	// 					{
	// 						builder.AppendLine($"{whiteSpace}  {checks[i]}");
	// 					}
	// 					else if (i == checks.Count - 1)
	// 					{
	// 						builder.AppendLine($"{whiteSpace}| {checks[i]});");
	// 					}
	// 					else
	// 					{
	// 						builder.AppendLine($"{whiteSpace}| {checks[i]}");
	// 					}
	// 				}
	// 			}
	// 			else if (!result && compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "NoneWhereAllBitsSet"))
	// 			{
	// 				builder.AppendLine($"return {vectorType}.NoneWhereAllBitsSet(");
	//
	// 				for (var i = 0; i < checks.Count; i++)
	// 				{
	// 					if (i == 0)
	// 					{
	// 						builder.AppendLine($"{whiteSpace}  {checks[i]}");
	// 					}
	// 					else if (i == checks.Count - 1)
	// 					{
	// 						builder.AppendLine($"{whiteSpace}| {checks[i]});");
	// 					}
	// 					else
	// 					{
	// 						builder.AppendLine($"{whiteSpace}| {checks[i]}");
	// 					}
	// 				}
	// 			}
	// 			else
	// 			{
	// 				if (result)
	// 				{
	// 					builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementType}>.Zero;");
	// 				}
	// 				else
	// 				{
	// 					builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) == {vectorType}<{elementType}>.Zero;");
	// 				}
	// 			}
	// 		}
	// 	}, isPerformance =>
	// 	{
	// 		// Use simple Contains check when available
	// 		if (typeSymbol.CheckMethod("Contains", compilation.CreateBoolean(), [ elementType ], out _))
	// 		{
	// 			var maxLength = method.Parameters.Max(m => m.Name.Length);
	// 			var padding = result
	// 				? new string(' ', maxLength + "return ".Length - 11)
	// 				: new string(' ', maxLength + "return ".Length - 10 + prefix.Length);
	//
	// 			if (result)
	// 			{
	// 				builder.AppendLine($"return {String.Join($"\n{padding}|| ", method.Parameters.Select(s => $"Contains({s.Name})"))};");
	// 			}
	// 			else
	// 			{
	// 				builder.AppendLine($"return !({String.Join($"\n{padding}|| ", method.Parameters.Select(s => $"Contains({s.Name})"))});");
	// 			}
	//
	// 		}
	// 		else if (generationLevel != GenerationLevel.Minimal)
	// 		{
	// 			var maxLength = method.Parameters.Max(m => m.Name.Length);
	// 			var padding = result
	// 				? new string(' ', maxLength + "return ".Length - 11)
	// 				: new string(' ', maxLength + "return ".Length - 10 + prefix.Length);
	//
	// 			IEnumerable<string> checks;
	//
	// 			if (items.Length == 1)
	// 			{
	// 				checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} == {items[0]}");
	// 			}
	// 			else if (isSequence)
	// 			{
	// 				if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
	// 				{
	// 					checks = method.Parameters.Select(s => $"({unsignedName}){s.Name}{new string(' ', maxLength - s.Name.Length)} <= {items[^1]}");
	// 				}
	// 				else
	// 				{
	// 					checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is >= {items[0]} and <= {items[^1]}");
	// 				}
	// 			}
	// 			else
	// 			{
	// 				checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is {String.Join(" or ", items.Select(CreateLiteral))}");
	// 			}
	//
	// 			if (result)
	// 			{
	// 				builder.AppendLine($"return {String.Join($"\n{padding}|| ", checks)};");
	// 			}
	// 			else
	// 			{
	// 				builder.AppendLine($"return !({String.Join($"\n{padding}|| ", checks)});");
	// 			}
	// 		}
	// 		else
	// 		{
	// 			builder.AppendLine($"return {prefix}{(LiteralString) GetDataName(typeSymbol)}");
	//
	// 			switch (method.Parameters.Length)
	// 			{
	// 				case 1:
	// 					builder.AppendLine($"\t.Contains({method.Parameters});");
	// 					break;
	// 				case 2 or 3:
	// 					builder.AppendLine($"\t.ContainsAny({method.Parameters});");
	// 					break;
	// 				default:
	// 					builder.AppendLine($"\t.ContainsAny([{method.Parameters}]);");
	// 					break;
	// 			}
	// 		}
	// 	});
	// }
}