using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class MemoryExtensionsBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, int hashCode) : BaseBuilder(elementType, compilation, generationLevel, loader, hashCode)
{
	public bool AppendBinarySearch(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "BinarySearch", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(elementType)
				     && elementType.HasMember<IMethodSymbol>("CompareTo", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
				                                                               && m.Parameters.AsSpan().EqualsTypes(elementType)):
			{
				items = items.OrderBy(o => o).ToList();

				AppendMethod(builder, method, items, isPerformance =>
				{
					BinarySearch(0, items.Count - 1, true, "{1}.CompareTo({0})");
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
					.ToList();

				AppendMethod(builder, method, items, isPerformance =>
				{
					BinarySearch(0, items.Count - 1, true, "{2}.Compare({1}, {0})");
				});

				return true;
			}

			default:
				return false;
		}

		void BinarySearch(int low, int high, bool isFirst, string compareFormat)
		{
			var set = new List<KeyValuePair<object?, int>>();

			PrepareBinarySearch(low, high);

			// First build a decision tree structure
			var tree = BuildBinarySearchTree(0, set.Count - 1, set.FindIndex(f => f.Value == high / 2), set, TreeNode.NodeState.None, null, items);

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

				var result = new KeyValuePair<object?, int>(value, index);

				var temp = set.BinarySearch(result, Comparer<KeyValuePair<object?, int>>.Create((x, y) => Comparer<object>.Default.Compare(x.Key, y.Key)));

				if (temp < 0)
				{
					set.Insert(~temp, result);
				}

				PrepareBinarySearch(tempLow, index - 1);
				PrepareBinarySearch(index + 1, tempHigh);
			}
		}
	}

	public bool AppendCommonPrefixLength(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "CommonPrefixLength", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
				when compilation.IsSpanLikeType(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
				{
					// Calculate count vector based on vector size
					if (vectorSize != items.Length)
					{
						builder.AppendLine($"var countVec = {vectorType}.Min({vectorType}.Create({method.Parameters[0]}.Length), {vectorType}.Create({items.Length}));");
					}
					else
					{
						builder.AppendLine($"var countVec = {vectorType}.Create({method.Parameters[0]}.Length);");
					}

					builder.AppendLine($"var otherVec = {vectorType}.LoadUnsafe(ref MemoryMarshal.GetReference({method.Parameters[0]}));");
					builder.AppendLine();

					// Create sequence vector
					if (compilation.HasMember<IPropertySymbol>(compilation.GetVectorType(vectorType, compilation.CreateInt32()), "Indices"))
					{
						builder.AppendLine($"var sequence = {vectorType}<{elementType}>.Indices;");
					}
					else
					{
						builder.AppendLine($"var sequence = {vectorType}.Create({String.Join(", ", Enumerable.Range(0, vectorSize).Select(CreateLiteral))});");
					}

					builder.AppendLine($"var items = {(LiteralString) vector};");
					builder.AppendLine();

					// Create mask based on element type
					if (elementType.SpecialType != SpecialType.System_Int32)
					{
						builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, items).As{elementType}() & {vectorType}.LessThan(sequence, countVec);");
					}
					else
					{
						builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, items) & {vectorType}.LessThan(sequence, countVec);");
					}

					// Calculate items based on available methods
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
				}, isperfomance =>
				{
					// Non-vectorized implementation
					if (!isperfomance)
					{
						builder.AppendLine($"return {GetDataName(method.ContainingType)}");
						builder.AppendLine($"\t.CommonPrefixLength({method.Parameters});");
					}
					else
					{
						if (elementType.SpecialType != SpecialType.None)
						{
							Append(method, "{1} != {0}");
						}
						else
						{
							Append(method, $"!EqualityComparer<{elementType}>.Default.Equals({{0}}, {{1}})");
						}
					}
				});

				return true;
			}
			case { Name: "CommonPrefixLength", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 2 }
				when compilation.IsSpanLikeType(method.Parameters[0].Type, elementType)
				     && IsEqualSymbol(method.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						using (AppendMethod(builder, method))
						{
							builder.AppendLine($"return {GetDataName(method.ContainingType)}");
							builder.AppendLine($"\t.CommonPrefixLength({method.Parameters});");
						}
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

	public bool AppendContainsAny(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 0 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				AppendContainsAny(method.ContainingType, method, false, items, builder);
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendContainsAnyExcept(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 0 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				AppendContainsAny(method.ContainingType, method, false, items, builder);

				return true;
			}
			case { Name: "ContainsAny", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 0 }
				when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					var checks = items
						.Select(s => $"{method.Parameters[0].Name}.Contains({CreateLiteral(s)})");

					builder.AppendLine(CreateReturnPadding("||", checks));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendContainsAnyInRange(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ContainsAnyInRange", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType, elementType):
			{
				items = items
					.Distinct()
					.OrderBy(o => o)
					.ToImmutableArray();

				AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
				{
					builder.AppendLine($"var items = {(LiteralString)vector};");
					builder.AppendLine();

					if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
					{
						builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet({vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({method.Parameters[0]})) & {vectorType}.LessThanOrEqual(items, {vectorType}.Create({method.Parameters[1]})));");
					}
					else
					{
						builder.AppendLine($"return ({vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({method.Parameters[0]})) & {vectorType}.LessThanOrEqual(items, {vectorType}.Create({method.Parameters[1]}))) != {vectorType}<{elementType}>.Zero;");
					}
				}, _ =>
				{
					var checks = items
						.Select(s => $"{s} >= {method.Parameters[0]} && {s} <= {method.Parameters[1]}");

					builder.AppendLine(CreateReturnPadding("||", checks));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendContainsAnyExceptInRange(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ContainsAnyExceptInRange", ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(elementType, elementType):
			{
				items = items
					.Distinct()
					.OrderBy(o => o)
					.ToImmutableArray();

				AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
				{
					builder.AppendLine($"var items = {(LiteralString) vector};");
					builder.AppendLine();

					if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
					{
						builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet({vectorType}.LessThan(items, {vectorType}.Create({method.Parameters[0]})) | {vectorType}.GreaterThan(items, {vectorType}.Create({method.Parameters[1]}));");
					}
					else
					{
						builder.AppendLine($"return ({vectorType}.LessThan(items, {vectorType}.Create({method.Parameters[0]})) | {vectorType}.GreaterThan(items, {vectorType}.Create({method.Parameters[1]}))) != {vectorType}<{elementType}>.Zero;");
					}
				}, _ =>
				{
					var checks = items
						.Select(s => $"{s} < {method.Parameters[0]} || {s} > {method.Parameters[1]}");

					builder.AppendLine(CreateReturnPadding("||", checks));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCount(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Count", ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(elementType):
			{
				AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
				{
					if (vectorSize != items.Length)
					{
						return;
					}

					builder.AppendLine($"var items = {(LiteralString) vector};");
					builder.AppendLine();

					if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "Count"))
					{
						builder.AppendLine($"return {vectorType}.Count(items, {method.Parameters[0]});");
					}
					else
					{
						builder.AppendLine($"return BitOperations.PopCount({vectorType}.ExtractMostSignificantBits({vectorType}.Equals(items, {method.Parameters[0]})));");
					}
				}, isPerformance =>
				{
					if (isPerformance)
					{
						var checks = items
							.Select(s => $"{s} == {method.Parameters[0].Name} ? 1 : 0");

						builder.AppendLine(CreateReturnPadding("+", checks));
					}
					else if (compilation.HasMember<IMethodSymbol>(compilation.GetTypeByMetadataName("System.MemoryExtensions"), "Count"))
					{
						builder.AppendLine($"return {GetDataName(method.ContainingType)}");
						builder.AppendLine($"\t.Count({method.Parameters[0]});");
					}
					else
					{
						builder.AppendLine("var result = 0;");
						builder.AppendLine();

						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})"))
						{
							using (builder.AppendBlock($"if (item == {method.Parameters[0]})"))
							{
								builder.AppendLine("result++;");
							}
						}

						builder.AppendLine();
						builder.AppendLine("return result;");
					}
				});

				return true;
			}
			case { Name: "Count", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
				when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
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

	public bool AppendEndsWith(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "EndsWith", ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: > 0 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						var checks = method.Parameters
							.Index()
							.Select(s => $"{s.Value.Name} == {items[^(s.Index + 1)]}")
							.Take(items.Count)
							.Reverse();

						builder.AppendLine(CreateReturnPadding("&&", checks));
					}
					else
					{
						builder.AppendLine($"return {GetDataName(method.ContainingType)}");
						builder.AppendLine($"\t.EndsWith({method.Parameters});");
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
				AppendMethod(builder, method, data.Cast<object?>(), isPerformance =>
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
				AppendMethod(builder, method, data.Cast<object?>(), isPerformance =>
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
				AppendMethod(builder, method, data.Cast<object?>(), isPerformance =>
				{
					builder.AppendLine($"return {String.IsNullOrWhiteSpace(data)};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndexOfAny(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "IndexOfAny", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: > 1 }
				when method.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)):
			{
				AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
				{
					builder.AppendLine($"var vector = {(LiteralString) vector};");
					builder.AppendLine();

					builder.AppendLine(CreatePadding("|", "var resultVector =", method.Parameters.Select(s => $"{vectorType}.Equals(vector, {vectorType}.Create({s.Name}))")));
					
					builder.AppendLine();

					if (compilation.GetVectorType(vectorType).HasMember<IMethodSymbol>("IndexOfWhereAllBitsSet", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }))
					{
						builder.AppendLine($"return {vectorType}.IndexOfWhereAllBitsSet(resultVector);");
					}
					else
					{
						using (builder.AppendBlock($"if (resultVector == {vectorType}<{elementType}>.Zero)"))
						{
							builder.AppendLine($"return {-1};");
						}
						
						builder.AppendLine();
						builder.AppendLine($"return BitOperations.TrailingZeroCount({vectorType}.ExtractMostSignificantBits(resultVector));");
					}

				}, isPerformance =>
				{
					if (isPerformance)
					{
						if (method.ContainingType.HasMember<IMethodSymbol>("IndexOf", m => m is { ReturnType.SpecialType: SpecialType.System_Int32 }
						                                                                   && m.Parameters.AsSpan().EqualsTypes(elementType)))
						{
							foreach (var (index, parameter) in method.Parameters.Index())
							{
								builder.AppendLine($"var index{index} = IndexOf({parameter.Name});");
							}

							builder.AppendLine();
							builder.AppendLine($"var result = Math.Min((uint)index0, {CreateMinChain(1, method.Parameters.Length)});");
							builder.AppendLine();
							builder.AppendLine("return Unsafe.BitCast<uint, int>(result);");
						}
						else
						{
							var values = new HashSet<object?>();

							foreach (var (index, item) in items.Index())
							{
								if (values.Add(item))
								{
									var checks = method.Parameters
										.Select(s => $"{s.Name} == {item}");

									builder.AppendLine($"if ({(LiteralString) String.Join(" || ", checks)}) return {index};");
								}
							}

							builder.AppendLine($"return {-1};");
						}
					}
					else
					{
						var collectionName = GetDataName(method.ContainingType);

						using (builder.AppendBlock($"for (var i = 0; i < {collectionName}.Length; i++)"))
						{
							using (builder.AppendBlock($"if ({String.Join(" || ", method.Parameters.Select(s => $"{s.Name} == {collectionName}[i]"))})"))
							{
								builder.AppendLine("return i;");
							}
						}

						builder.AppendLine();
						builder.AppendLine("return -1;");
					}
				});

				return true;
			}

			case { Name: "IndexOfAny", ReturnType.SpecialType: SpecialType.System_Int32, Parameters.Length: 1 }
				when compilation.HasContainsMethod(method.Parameters[0].Type, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Length; i++)
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({items[i]}))"))
							{
								builder.AppendLine($"return {i};");
							}

							builder.AppendLine();
						}

						builder.AppendLine($"return {-1};");
					}
					else
					{
						var collectionName = GetDataName(method.ContainingType);

						using (builder.AppendBlock($"for (var i = 0; i < {collectionName}.Length; i++)"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}.Contains({collectionName}[i]))"))
							{
								builder.AppendLine("return i;");
							}
						}

						builder.AppendLine();
						builder.AppendLine("return -1;");
					}
				});

				return true;
			}
			default:
				return false;
		}

		string CreateMinChain(int index, int count)
		{
			if (index == count - 1)
			{
				return $"(uint)index{index}";
			}

			return $"Math.Min((uint)index{index}, {CreateMinChain(index + 1, count)})";
		}
	}

	public bool AppendReplace(IMethodSymbol method, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Replace", ReturnType.SpecialType: SpecialType.System_Void, Parameters.Length: 3 }
				when compilation.IsReadonlySpanType(method.Parameters[0].Type, elementType) && method.Parameters.AsSpan(1, method.Parameters.Length - 1).EqualsTypes(elementType, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					using (builder.AppendBlock($"if ({(uint) items.Length} > (uint){method.Parameters[0]}.Length)"))
					{
						builder.AppendLine($"throw new ArgumentOutOfRangeException(nameof({method.Parameters[0]}), \"The length of the span is less than {items.Length}\");");
					}

					builder.AppendLine();

					if (isPerformance && compilation.IsVectorSupported(elementType))
					{
						var vectors = new List<string>();

						var elements = items.AsSpan();
						var type = compilation.GetVector(elementType, loader, elements, VectorTypes.Vector512, out var vector, out var vectorSize);
						var index = 0;

						if (type != VectorTypes.None)
						{
							do
							{
								vectors.Add(vector);

								index += vectorSize;
								elements = elements.Slice(vectorSize);
							} while (index < items.Length && type == compilation.GetVector(elementType, loader, elements, VectorTypes.Vector512, out vector, out vectorSize));

							using (builder.AppendBlock($"if ({type}.IsHardwareAccelerated)"))
							{
								builder.AppendLine($"var {method.Parameters[1]}Vector = {type}.Create({method.Parameters[1]});");
								builder.AppendLine($"var {method.Parameters[2]}Vector = {type}.Create({method.Parameters[2]});");
								builder.AppendLine();

								for (var i = 0; i < vectors.Count; i++)
								{
									builder.AppendLine($"var vec{i} = {(LiteralString) vectors[i]};");
								}

								builder.AppendLine();

								for (var i = 0; i < vectors.Count; i++)
								{
									builder.AppendLine($"{type}.ConditionalSelect({type}.Equals(vec{i}, {method.Parameters[1]}Vector), {method.Parameters[2]}Vector, vec{i}).StoreUnsafe(ref MemoryMarshal.GetReference({method.Parameters[0]}), {i * vectorSize});");
								}

								if (index < items.Length)
								{
									builder.AppendLine();
								}

								for (; index < items.Length; index++)
								{
									builder.AppendLine($"{method.Parameters[0]}[{index}] = {items[index]} == {method.Parameters[1]} ? {method.Parameters[2]} : {items[index]};");
								}

								builder.AppendLine();
								builder.AppendLine("return;");
							}

							builder.AppendLine();
						}
					}

					if (isPerformance)
					{
						if (elementType.SpecialType != SpecialType.None)
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{method.Parameters[0]}[{i}] = {items[i]} == {method.Parameters[1]} ? {method.Parameters[2]} : {items[i]};");
							}
						}
						else
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{method.Parameters[0]}[{i}] = EqualityComparer<{elementType}>.Default.Equals({items[i]}, {method.Parameters[1]}) ? {method.Parameters[2]} : {items[i]};");
							}
						}
					}
					else
					{
						builder.AppendLine($"{GetDataName(method.ContainingType)}.Replace({method.Parameters[0]}, {method.Parameters[1]}, {method.Parameters[2]});");
					}
				});

				return true;
			}
		}

		return false;
	}

	private void AppendContainsAny(ITypeSymbol typeSymbol, IMethodSymbol method, bool result, ImmutableArray<object?> items, IndentedStringBuilder builder)
	{
		var prefix = result ? String.Empty : "!";

		// Prepare items
		items = items
			.Distinct()
			.OrderBy(o => o)
			.ToImmutableArray();

		var elementSize = compilation.GetByteSize(loader, method.Parameters[0].Type);
		var isSequence = items.IsNumericSequence();
		var isZero = items[0] is 0 or 0L or (byte) 0 or (short) 0 or (sbyte) 0 or (ushort) 0 or (uint) 0 or (ulong) 0;
		var unsignedType = compilation.GetUnsignedType(elementType);
		var unsignedName = compilation.GetMinimalString(unsignedType);

		AppendMethod(builder, method, items, (vectorType, vector, vectorSize) =>
		{
			var vectorByteSize = vectorSize * elementSize;
			var whiteSpace = new string(' ', 6);

			using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
			{
				// Special case for single item
				if (items.Length == 1)
				{
					builder.AppendLine($"return {prefix}{vectorType}.EqualsAny({(LiteralString) vector}, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])});");
					return;
				}

				builder.AppendLine($"var input = {(LiteralString) vector};");
				builder.AppendLine();

				// Handle sequential items differently
				if (isSequence)
				{
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						builder.AppendLine($"return {prefix}{vectorType}.LessThanOrEqualAny(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({items[^1]}));");
					}
					else
					{
						builder.AppendLine(
							$"return {prefix}({vectorType}.GreaterThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])}) & " +
							$"{vectorType}.LessThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[^1])})) != {vectorType}<{elementType}>.Zero;");
					}
				}
				else
				{
					var checks = method.Parameters
						.Select(s => $"{vectorType}.Equals(input, {vectorType}.Create({s.Name}))")
						.ToList();

					if (result && compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
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
					else if (!result && compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "NoneWhereAllBitsSet"))
					{
						builder.AppendLine($"return {vectorType}.NoneWhereAllBitsSet(");

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
						if (result)
						{
							builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementType}>.Zero;");
						}
						else
						{
							builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) == {vectorType}<{elementType}>.Zero;");
						}
					}
				}
			}

			builder.AppendLine();
		}, isPerformance =>
		{
			// Use simple Contains check when available
			if (typeSymbol.CheckMethod("Contains", compilation.CreateBoolean(), [ elementType ], out _))
			{
				var maxLength = method.Parameters.Max(m => m.Name.Length);
				var padding = result
					? new string(' ', maxLength + "return ".Length - 11)
					: new string(' ', maxLength + "return ".Length - 10 + prefix.Length);

				if (result)
				{
					builder.AppendLine($"return {String.Join($"\n{padding}|| ", method.Parameters.Select(s => $"Contains({s.Name})"))};");
				}
				else
				{
					builder.AppendLine($"return !({String.Join($"\n{padding}|| ", method.Parameters.Select(s => $"Contains({s.Name})"))});");
				}

			}
			else if (generationLevel != GenerationLevel.Minimal)
			{
				var maxLength = method.Parameters.Max(m => m.Name.Length);
				var padding = result
					? new string(' ', maxLength + "return ".Length - 11)
					: new string(' ', maxLength + "return ".Length - 10 + prefix.Length);

				IEnumerable<string> checks;

				if (items.Length == 1)
				{
					checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} == {items[0]}");
				}
				else if (isSequence)
				{
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						checks = method.Parameters.Select(s => $"({unsignedName}){s.Name}{new string(' ', maxLength - s.Name.Length)} <= {items[^1]}");
					}
					else
					{
						checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is >= {items[0]} and <= {items[^1]}");
					}
				}
				else
				{
					checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is {String.Join(" or ", items.Select(CreateLiteral))}");
				}

				if (result)
				{
					builder.AppendLine($"return {String.Join($"\n{padding}|| ", checks)};");
				}
				else
				{
					builder.AppendLine($"return !({String.Join($"\n{padding}|| ", checks)});");
				}
			}
			else
			{
				builder.AppendLine($"return {prefix}{GetDataName(typeSymbol)}");

				switch (method.Parameters.Length)
				{
					case 1:
						builder.AppendLine($"\t.Contains({method.Parameters});");
						break;
					case 2 or 3:
						builder.AppendLine($"\t.ContainsAny({method.Parameters});");
						break;
					default:
						builder.AppendLine($"\t.ContainsAny([{method.Parameters}]);");
						break;
				}
			}
		});
	}
}