using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class SpanBuilder(Compilation compilation, MetadataLoader loader, ITypeSymbol elementType, GenerationLevel generationLevel, int hashCode) : BaseBuilder(elementType, compilation, generationLevel, loader, hashCode)
{
	public void AppendCommonPrefixLength(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		// Check if the type has a suitable CommonPrefixLength method with one parameter
		var hasMatchingMethod = typeSymbol.CheckMembers<IMethodSymbol>(
			"CommonPrefixLength",
			m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32) &&
					 m.Parameters.Length == 1 &&
					 compilation.IsSpanType(m.Parameters[0].Type, elementType),
			out var member);

		if (hasMatchingMethod)
		{
			using (AppendMethod(builder, member))
			{
				// Add hardware acceleration if not minimal generation and element type is a special type
				if (generationLevel != GenerationLevel.Minimal && elementType.SpecialType != SpecialType.None)
				{
					var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

					if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
					{
						using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
						{
							// Calculate count vector based on vector size
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

							// Create sequence vector
							if (compilation.HasMember<IPropertySymbol>(compilation.GetVectorType(vectorType, compilation.CreateInt32()), "Indices"))
							{
								builder.AppendLine($"var sequence = {vectorType}<{compilation.GetMinimalString(elementType)}>.Indices;");
							}
							else
							{
								builder.AppendLine($"var sequence = {vectorType}.Create({String.Join(", ", Enumerable.Range(0, vectorSize).Select(CreateLiteral))});");
							}

							builder.AppendLine($"var items = {vector};");
							builder.AppendLine();

							// Create mask based on element type
							if (elementType.SpecialType != SpecialType.System_Int32)
							{
								builder.AppendLine($"var mask = {vectorType}.Equals(otherVec, items).As{elementType.Name}() & {vectorType}.LessThan(sequence, countVec);");
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
						}
					}

					builder.AppendLine();
				}

				// Non-vectorized implementation
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

		// Check if the type has a suitable CommonPrefixLength method with two parameters
		var hasMatchingMethodWithComparer = typeSymbol.CheckMembers(
			"CommonPrefixLength",
			m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Int32) &&
					 m.Parameters.Length == 2 &&
					 compilation.IsSpanType(m.Parameters[0].Type, elementType) &&
					 IsEqualSymbol(m.Parameters[1].Type, compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType)),
			out member);

		if (hasMatchingMethodWithComparer)
		{
			if (IsPerformance(generationLevel, items.Count))
			{
				using (AppendMethod(builder, member))
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					builder.AppendLine($"\t.CommonPrefixLength({member.Parameters[0].Name}, {member.Parameters[1].Name});");
				}
			}
			else
			{
				Append(member, $"!{member.Parameters[1].Name}.Equals({{0}}, {{1}})");
			}
		}

		// Helper method to append prefix length calculation logic
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
		// Check if type has a suitable ContainsAny method
		var hasMatchingMethod = typeSymbol.CheckMembers<IMethodSymbol>(
			"ContainsAny",
			m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Boolean) &&
					 m.Parameters.Length > 0 &&
					 m.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)),
			out var member);

		if (hasMatchingMethod)
		{
			AppendContainsAny(typeSymbol, member!, true, items, builder);
		}
	}

	public void AppendContainsAnyExcept(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		// Check if type has a suitable ContainsAny method
		var hasMatchingMethod = typeSymbol.CheckMembers<IMethodSymbol>(
			"ContainsAnyExcept",
			m => compilation.IsSpecialType(m.ReturnType, SpecialType.System_Boolean) &&
					 m.Parameters.Length > 0 &&
					 m.Parameters.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)),
			out var member);

		if (hasMatchingMethod)
		{
			AppendContainsAny(typeSymbol, member!, false, items, builder);
		}
	}

	public void AppendContainsAnyInRange(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod("ContainsAnyInRange", compilation.CreateBoolean(), [elementType, elementType], out var member))
		{
			items = items.Distinct().OrderBy(o => o).ToList();

			AppendMethod(builder, member, items, (vectorType, vector, vectorSize) =>
			{
				builder.AppendLine($"var items = {vector};");
				builder.AppendLine();

				if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
				{
					builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet({vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({member.Parameters[0].Name})) & {vectorType}.LessThanOrEqual(items, {vectorType}.Create({member.Parameters[1].Name})));");
				}
				else
				{
					builder.AppendLine($"return ({vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({member.Parameters[0].Name})) & {vectorType}.LessThanOrEqual(items, {vectorType}.Create({member.Parameters[1].Name}))) != {vectorType}<{compilation.GetMinimalString(elementType)}>.Zero;");
				}
			}, _ =>
			{
				var checks = items
					.Select(s => $"{s} >= {member.Parameters[0].Name} && {s} <= {member.Parameters[1].Name}");

				builder.AppendLine(CreateReturnPadding("||", checks));
			});
		}
	}

	public void AppendContainsAnyExceptInRange(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod("ContainsAnyExceptInRange", compilation.CreateBoolean(), [elementType, elementType], out var member))
		{
			items = items.Distinct().OrderBy(o => o).ToList();

			AppendMethod(builder, member, items, (vectorType, vector, vectorSize) =>
			{
				builder.AppendLine($"var items = {vector};");
				builder.AppendLine();

				if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "AnyWhereAllBitsSet"))
				{
					builder.AppendLine($"return {vectorType}.AnyWhereAllBitsSet({vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({member.Parameters[0].Name})) | {vectorType}.LessThanOrEqual(items, {vectorType}.Create({member.Parameters[1].Name})));");
				}
				else
				{
					builder.AppendLine($"return ({vectorType}.LessThanOrEqual(items, {vectorType}.Create({member.Parameters[0].Name})) | {vectorType}.GreaterThanOrEqual(items, {vectorType}.Create({member.Parameters[1].Name}))) != {vectorType}<{compilation.GetMinimalString(elementType)}>.Zero;");
				}
			}, _ =>
			{
				var checks = items
					.Select(s => $"{s} <= {member.Parameters[0].Name} || {s} >= {member.Parameters[1].Name}");

				builder.AppendLine(CreateReturnPadding("||", checks));
			});
		}
	}

	public void AppendCount(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod("Count", compilation.CreateInt32(), [elementType], out var member))
		{
			AppendMethod(builder, member, items, (vectorType, vector, vectorSize) =>
			{
				if (vectorSize != items.Count)
				{
					return;
				}

				builder.AppendLine($"var items = {vector};");
				builder.AppendLine();

				if (compilation.HasMember<IMethodSymbol>(compilation.GetVectorType(vectorType), "Count"))
				{
					builder.AppendLine($"return {vectorType}.Count(items, {member.Parameters[0].Name});");
				}
				else
				{
					builder.AppendLine($"return BitOperations.PopCount({vectorType}.ExtractMostSignificantBits({vectorType}.Equals(items, {member.Parameters[0].Name})));");
				}
			}, isPerformance =>
			{
				if (isPerformance)
				{
					var checks = items
						.Select(s => $"{s} == {member.Parameters[0].Name} ? 1 : 0");

					builder.AppendLine(CreateReturnPadding("+", checks));
				}
				else if (compilation.HasMember<IMethodSymbol>(compilation.GetTypeByMetadataName("System.MemoryExtensions"), "Count"))
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					builder.AppendLine($"\t.Count({member.Parameters[0].Name});");
				}
				else
				{
					builder.AppendLine("var result = 0;");
					builder.AppendLine();

					using (builder.AppendBlock($"foreach (var item in {GetDataName(typeSymbol)})"))
					{
						using (builder.AppendBlock($"if (item == {member.Parameters[0].Name})"))
						{
							builder.AppendLine("result++;");
						}
					}

					builder.AppendLine();
					builder.AppendLine("return result;");
				}
			});
		}
	}

	public void AppendEndsWith(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMembers<IMethodSymbol>("EndsWith", m =>
					compilation.IsSpecialType(m.ReturnType, SpecialType.System_Boolean) && m.Parameters.Any() && m.Parameters
						.All(a => SymbolEqualityComparer.Default.Equals(a.Type, elementType)), out var member))
		{
			AppendMethod(builder, member, items, isPerformance =>
			{
				if (isPerformance)
				{
					var checks = member.Parameters
						.Index()
						.Select(s => $"{s.Value.Name} == {items[^(s.Index + 1)]}")
						.Take(items.Count)
						.Reverse();

					builder.AppendLine(CreateReturnPadding("&&", checks));
				}
				else
				{
					builder.AppendLine($"return {GetDataName(typeSymbol)}");
					builder.AppendLine($"\t.EndsWith({String.Join(", ", member.Parameters.Select(s => s.Name))});");
				}
			});
		}
	}

	private void AppendContainsAny(ITypeSymbol typeSymbol, IMethodSymbol method, bool result, IList<object?> items, IndentedStringBuilder builder)
	{
		var prefix = result ? String.Empty : "!";

		using (AppendMethod(builder, method))
		{
			// Prepare items
			items = items.Distinct().OrderBy(o => o).ToList();

			var elementSize = compilation.GetByteSize(loader, method.Parameters[0].Type);
			var isSequence = items.IsNumericSequence();
			var isZero = items[0] is 0 or 0L or (byte)0 or (short)0 or (sbyte)0 or (ushort)0 or (uint)0 or (ulong)0;
			var unsignedType = compilation.GetUnsignedType(elementType);
			var unsignedName = compilation.GetMinimalString(unsignedType);

			// Use vector operations for performance when appropriate
			if (method.Parameters.Length > 1 && generationLevel == GenerationLevel.Performance)
			{
				var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

				if (isSequence)
				{
					vectorType = compilation.GetVector(
						elementType,
						loader,
						method.Parameters.Select(s => (object?)s.Name).ToList(),
						true,
						out vector,
						out vectorSize);
				}

				if (vectorType != VectorTypes.None && compilation.IsVectorSupported(elementType))
				{
					var vectorByteSize = vectorSize * elementSize;
					var elementName = compilation.GetMinimalString(elementType);
					var whiteSpace = new string(' ', 6);

					using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
					{
						// Special case for single item
						if (items.Count == 1)
						{
							builder.AppendLine($"return {prefix}{vectorType}.EqualsAny({vector}, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])});");
							return;
						}

						builder.AppendLine($"var input = {vector};");
						builder.AppendLine();

						// Handle sequential items differently
						if (isSequence)
						{
							if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
							{
								builder.AppendLine($"return {prefix}{vectorType}.LessThanOrEqualAny(input.As{unsignedType.Name}(), {vectorType}.Create<{unsignedName}>({CreateLiteral(items[^1])}));");
							}
							else
							{
								builder.AppendLine(
									$"return {prefix}({vectorType}.GreaterThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[0])}) & " +
									$"{vectorType}.LessThanOrEqual(input, {compilation.GetCreateVector(vectorType.ToString(), vectorByteSize, elementType, loader, false, items[^1])})) != {vectorType}<{elementName}>.Zero;");
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
									builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) != {vectorType}<{elementName}>.Zero;");
								}
								else
								{
									builder.AppendLine($"return ({String.Join($"\n{whiteSpace}| ", checks)}) == {vectorType}<{elementName}>.Zero;");
								}
							}
						}
					}

					builder.AppendLine();
				}
			}

			// Use simple Contains check when available
			if (typeSymbol.CheckMethod("Contains", compilation.CreateBoolean(), [elementType], out _))
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

				if (items.Count == 1)
				{
					checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} == {CreateLiteral(items[0])}");
				}
				else if (isSequence)
				{
					if (isZero && !SymbolEqualityComparer.Default.Equals(elementType, unsignedType))
					{
						checks = method.Parameters.Select(s => $"({unsignedName}){s.Name}{new string(' ', maxLength - s.Name.Length)} <= {CreateLiteral(items[^1])}");
					}
					else
					{
						checks = method.Parameters.Select(s => $"{s.Name}{new string(' ', maxLength - s.Name.Length)} is >= {CreateLiteral(items[0])} and <= {CreateLiteral(items[^1])}");
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
						builder.AppendLine($"\t.Contains({String.Join(", ", method.Parameters.Select(s => s.Name))});");
						break;
					case 2 or 3:
						builder.AppendLine($"\t.ContainsAny({String.Join(", ", method.Parameters.Select(s => s.Name))});");
						break;
					default:
						builder.AppendLine($"\t.ContainsAny([{String.Join(", ", method.Parameters.Select(s => s.Name))}]);");
						break;
				}
			}
		}
	}
}