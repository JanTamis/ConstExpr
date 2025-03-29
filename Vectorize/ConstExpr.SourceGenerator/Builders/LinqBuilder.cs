using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConstExpr.SourceGenerator.Enums;
using Microsoft.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.SourceGenerator.Builders;

public class LinqBuilder(Compilation compilation, ITypeSymbol elementType, GenerationLevel level, int hashCode) : BaseBuilder(elementType, compilation, hashCode)
{
	public void AppendAggregate(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		// First overload: Aggregate<TSource>(Func<TSource, TSource, TSource> func)
		var funcType = compilation.CreateFunc(elementType, elementType, elementType);

		if (typeSymbol.CheckMethod(nameof(Enumerable.Aggregate), elementType, [ funcType ], out var member))
		{
			using (AppendMethod(builder, member))
			{
				switch (items.Count)
				{
					case 0:
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
						break;
					case 1:
						builder.AppendLine($"return {CreateLiteral(items[0])};");
						break;
					case 2:
						builder.AppendLine($"return {member.Parameters[0].Name}({CreateLiteral(items[0])}, {CreateLiteral(items[1])});");
						break;
					default:
					{
						if (IsPerformance(level, items.Count))
						{
							builder.AppendLine($"var result = {CreateLiteral(items[0])};");

							for (var i = 1; i < items.Count; i++)
							{
								if (i < items.Count - 1)
								{
									builder.AppendLine($"result = {member.Parameters[0].Name}(result, {CreateLiteral(items[i])});");
								}
								else
								{
									builder.AppendLine($"return {member.Parameters[0].Name}(result, {CreateLiteral(items[i])});");
								}
							}
						}
						else
						{
							builder.AppendLine($"var result = {GetDataName(typeSymbol)}[0];");
							builder.AppendLine();

							using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(typeSymbol)}.Length; i++)"))
							{
								builder.AppendLine($"result = {member.Parameters[0].Name}(result, {GetDataName(typeSymbol)}[i]);");
							}

							builder.AppendLine();
							builder.AppendLine($"return result;");
						}
						break;
					}
				}
			}
			return;
		}

		// Second overload: Aggregate<TSource, TAccumulate>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
		if (typeSymbol.CheckMembers(nameof(Enumerable.Aggregate), m => m.Parameters.Length == 2
		                                                               && SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, compilation.CreateFunc(m.Parameters[0].Type, elementType, m.Parameters[0].Type))
		                                                               && SymbolEqualityComparer.Default.Equals(m.ReturnType, m.Parameters[0].Type),
			    out member))
		{
			using (AppendMethod(builder, member))
			{
				// builder.AppendLine($"var result = {member.Parameters[0].Name};");
				
				for (var i = 0; i < items.Count; i++)
				{
					var item = CreateLiteral(items[i]);

					if (i < items.Count - 1)
					{
						builder.AppendLine($"{member.Parameters[0].Name} = {member.Parameters[1].Name}({member.Parameters[0].Name}, {item});");
					}
					else
					{
						builder.AppendLine($"return {member.Parameters[1].Name}({member.Parameters[0].Name}, {item});");
					}
				}
			}
			
			return;
		}

		// Third overload: Aggregate<TSource, TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
		if (typeSymbol.CheckMembers(nameof(Enumerable.Aggregate), m => m.Parameters.Length == 3
		                                                               && m.Parameters[2].Type is INamedTypeSymbol { Arity: 2 } namedTypeSymbol
		                                                               && SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, compilation.CreateFunc(m.Parameters[0].Type, elementType, m.Parameters[0].Type))
		                                                               && SymbolEqualityComparer.Default.Equals(m.Parameters[2].Type, compilation.CreateFunc(m.Parameters[0].Type, namedTypeSymbol.TypeArguments[1]))
		                                                               && SymbolEqualityComparer.Default.Equals(m.ReturnType, namedTypeSymbol.TypeArguments[1]),
			                                                               out member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"var result = {member.Parameters[0].Name};");

				foreach (var item in items)
				{
					builder.AppendLine($"result = {member.Parameters[1].Name}(result, {CreateLiteral(item)});");
				}
				builder.AppendLine($"return {member.Parameters[2].Name}(result);");
			}
		}
	}

	public void AppendAny(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Any), compilation.CreateBoolean(), out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {CreateLiteral(items.Any())};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.Any), compilation.CreateBoolean(), [ selector ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {String.Join("\n\t || ", items.Select(s => $"{member.Parameters[0].Name}({CreateLiteral(s)})"))};");
		}
	}

	public void AppendAll(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.All), compilation.CreateBoolean(), [ selector ], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {String.Join("\n\t && ", items.Select(s => $"{member.Parameters[0].Name}({CreateLiteral(s)})"))};");
		}
	}

	public void AppendAsEnumerable(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.AsEnumerable), compilation.CreateIEnumerable(elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items)
			{
				builder.AppendLine($"yield return {CreateLiteral(item)};");
			}
		}
	}

	public void AppendAverage(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Average), elementType, out var member))
		{
			return;
		}

		var average = items.Average(elementType);

		using (AppendMethod(builder, member))
		{
			if (average is null)
			{
				builder.AppendLine("return default;");
			}
			else
			{
				builder.AppendLine($"return {CreateLiteral(average)};");
			}
		}
	}

	public void AppendCast(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>(nameof(Enumerable.Cast), m => m.TypeParameters.Length == 1
		                                                                          && SymbolEqualityComparer.Default.Equals(m.ReturnType, compilation.CreateIEnumerable(m.TypeArguments[0])), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items)
			{
				builder.AppendLine($"yield return ({member.TypeParameters[0].Name})Convert.ChangeType({CreateLiteral(item)}, typeof({member.TypeParameters[0].Name}));");
			}
		}
	}

	public void AppendContains(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod(nameof(Enumerable.Contains), compilation.CreateBoolean(), [ elementType ], out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {member.Parameters[0].Name} is {String.Join("\n\t||", items.Select(s => $"EqualityComparer<{elementType.Name}>.Default.Equals({CreateLiteral(s)}, {member.Parameters[0].Name})"))};");
			}

			return;
		}

		var comparerType = compilation.GetTypeByType(typeof(IEqualityComparer<>), elementType);

		if (!typeSymbol.CheckMethod(nameof(Enumerable.Contains), compilation.CreateBoolean(), [ elementType, comparerType ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {member.Parameters[0].Name} is {String.Join("\n\t||", items.Select(s => $"{member.Parameters[1].Name}.Equals({CreateLiteral(s)}, {member.Parameters[0].Name})"))};");
		}
	}

	public void AppendCount(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Count), compilation.CreateInt32(), out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {items.Count};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.Count), compilation.CreateInt32(), [ selector ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {String.Join("\n\t + ", items.Select(s => $"{member.Parameters[0].Name}({CreateLiteral(s)}) ? 1 : 0"))};");
		}
	}

	public void AppendLongCount(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.LongCount), compilation.CreateInt64(), out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {items.Count};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.LongCount), compilation.CreateInt64(), [ selector ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {String.Join("\n\t + ", items.Select(s => $"{member.Parameters[0].Name}({CreateLiteral(s)}) ? 1 : 0"))};");
		}
	}

	public void AppendDistinct(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Distinct), compilation.CreateIEnumerable(elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items.Distinct())
			{
				builder.AppendLine($"yield return {CreateLiteral(item)};");
			}
		}
	}

	public void AppendElementAt(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod(nameof(Enumerable.ElementAt), elementType, [ compilation.CreateInt32() ], out var member))
		{
			using (AppendMethod(builder, member))
			{
				using (builder.AppendBlock($"return {member.Parameters[0].Name} switch", "};"))
				{
					for (var i = 0; i < items.Count; i++)
					{
						builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
					}

					builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
				}
			}

			return;
		}

		if (!typeSymbol.CheckMethod(nameof(Enumerable.ElementAt), elementType, [ compilation.GetTypeByType(typeof(Index)) ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			using (AppendMethod(builder, member))
			{
				using (builder.AppendBlock($"return {member.Parameters[0].Name}.GetOffset({CreateLiteral(items.Count)}) switch", "};"))
				{
					for (var i = 0; i < items.Count; i++)
					{
						builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
					}

					builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
				}
			}
		}
	}

	public void AppendElementAtOrDefault(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethod(nameof(Enumerable.ElementAtOrDefault), elementType, [ compilation.CreateInt32() ], out var member))
		{
			using (AppendMethod(builder, member))
			{
				using (builder.AppendBlock($"return {member.Parameters[0].Name} switch", "};"))
				{
					for (var i = 0; i < items.Count; i++)
					{
						builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
					}

					builder.AppendLine("_ => default");
				}
			}

			return;
		}

		if (!typeSymbol.CheckMethod(nameof(Enumerable.ElementAtOrDefault), elementType, [ compilation.GetTypeByType(typeof(Index)) ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			using (AppendMethod(builder, member))
			{
				using (builder.AppendBlock($"return {member.Parameters[0].Name}.GetOffset({CreateLiteral(items.Count)}) switch", "};"))
				{
					for (var i = 0; i < items.Count; i++)
					{
						builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
					}

					builder.AppendLine("_ => default");
				}
			}
		}
	}

	public void AppendFirst(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.First), elementType, out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {CreateLiteral(items.First())};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.First), elementType, [ selector ], out member))
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

				var item = CreateLiteral(items[i]);

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}({item}))"))
				{
					builder.AppendLine($"return {item};");
				}
			}

			if (items.Count > 0)
			{
				builder.AppendLine();
				builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
			}
			else
			{
				builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
			}
		}
	}

	public void AppendFirstOrDefault(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.FirstOrDefault), elementType, out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {(items.Count > 0 ? CreateLiteral(items.First()) : "default")};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.FirstOrDefault), elementType, [ selector ], out member))
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

				var item = CreateLiteral(items[i]);

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}({item}))"))
				{
					builder.AppendLine($"return {item};");
				}
			}


			if (items.Count > 0)
			{
				builder.AppendLine();
			}
			builder.AppendLine("return default;");
		}
	}

	public void AppendIndex(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType("Index", compilation.GetTypeByType(typeof(ValueTuple<,>), compilation.CreateInt32(), elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			var i = 0;

			foreach (var item in items)
			{
				builder.AppendLine($"yield return ({i++}, {CreateLiteral(item)});");
			}
		}
	}

	public void AppendLast(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Last), elementType, out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {CreateLiteral(items.Last())};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.Last), elementType, [ selector ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			for (var i = items.Count - 1; i >= 0; i--)
			{
				if (i != items.Count - 1)
				{
					builder.AppendLine();
				}

				var item = CreateLiteral(items[i]);

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}({item}))"))
				{
					builder.AppendLine($"return {item};");
				}
			}

			if (items.Count > 0)
			{
				builder.AppendLine();
				builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
			}
			else
			{
				builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
			}
		}
	}

	public void AppendLastOrDefault(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.LastOrDefault), elementType, out var member))
		{
			using (AppendMethod(builder, member))
			{
				builder.AppendLine($"return {(items.Count > 0 ? CreateLiteral(items.Last()) : "default")};");
			}

			return;
		}

		var selector = compilation.CreateFunc(elementType, compilation.CreateBoolean());

		if (!typeSymbol.CheckMethod(nameof(Enumerable.LastOrDefault), elementType, [ selector ], out member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			for (var i = items.Count - 1; i >= 0; i--)
			{
				if (i != items.Count - 1)
				{
					builder.AppendLine();
				}

				var item = CreateLiteral(items[i]);

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}({item}))"))
				{
					builder.AppendLine($"return {item};");
				}
			}

			if (items.Count > 0)
			{
				builder.AppendLine();
			}

			builder.AppendLine("return default;");
		}
	}

	public void AppendOrder(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType("Order", compilation.CreateIEnumerable(elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items.OrderBy(s => s))
			{
				builder.AppendLine($"yield return {CreateLiteral(item)};");
			}
		}
	}

	public void AppendOrderDescending(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType("OrderDescending", compilation.CreateIEnumerable(elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items.OrderByDescending(s => s))
			{
				builder.AppendLine($"yield return {CreateLiteral(item)};");
			}
		}
	}

	public void AppendReverse(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Reverse), compilation.CreateIEnumerable(elementType), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			foreach (var item in items.Reverse())
			{
				builder.AppendLine($"yield return {CreateLiteral(item)};");
			}
		}
	}

	public void AppendSelect(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>(nameof(Enumerable.Select), m => m.ReturnType is INamedTypeSymbol namedTypeSymbol
		                                                                            && IsIEnumerable(namedTypeSymbol)
		                                                                            && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, compilation.CreateFunc(elementType, namedTypeSymbol.TypeArguments[0])), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			if (!IsPerformance(level, items.Count))
			{
				using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(typeSymbol)}.Length; i++)"))
				{
					builder.AppendLine($"yield return {member.Parameters[0].Name}({GetDataName(typeSymbol)}[i]);");
				}
			}
			else
			{
				foreach (var item in items)
				{
					builder.AppendLine($"yield return {member.Parameters[0].Name}({CreateLiteral(item)});");
				}
			}
		}
	}

	public void AppendSequenceEqual(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod(nameof(Enumerable.SequenceEqual), compilation.CreateBoolean(), [ compilation.CreateIEnumerable(elementType) ], out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			if (level != GenerationLevel.Minimal && compilation.HasMember<IMethodSymbol>(typeof(Enumerable), "TryGetNonEnumeratedCount"))
			{
				using (builder.AppendBlock($"if ({member.Parameters[0].Name}.TryGetNonEnumeratedCount(out var count) && count != {items.Count})"))
				{
					builder.AppendLine($"return false;");
				}

				builder.AppendLine();
			}
			
			builder.AppendLine($"using var e = {member.Parameters[0].Name}.GetEnumerator();");
			builder.AppendLine();

			if (!items.Any())
			{
				builder.AppendLine($"return !e.MoveNext();");
			}

			var whitespace = new string(' ', "return ".Length - 3);

			builder.AppendLine("return " + String.Join($"\n{whitespace}&& ", items.Select(s => $"e.MoveNext() && {CreateLiteral(s)} == e.Current")) + $"\n{whitespace}&& !e.MoveNext();");
		}
	}

	public void AppendSingle(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Single), elementType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			switch (items.Count)
			{
				case 0:
					builder.AppendLine("throw new InvalidOperationException(\"The input sequence is empty\");");
					break;
				case 1:
					builder.AppendLine($"return {CreateLiteral(items[0])};");
					break;
				default:
					builder.AppendLine("throw new InvalidOperationException(\"The input sequence contains more than one element\");");
					break;
			}
		}
	}

	public void AppendSingleOrDefault(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.SingleOrDefault), elementType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			switch (items.Count)
			{
				case 1:
					builder.AppendLine($"return {CreateLiteral(items[0])};");
					break;
				default:
					builder.AppendLine("return default;");
					break;
			}
		}
	}

	public void AppendSum(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.Sum), elementType, out var member))
		{
			return;
		}

		var sum = items.Sum();

		using (AppendMethod(builder, member))
		{
			if (sum is null)
			{
				builder.AppendLine("return default;");
			}
			else
			{
				builder.AppendLine($"return {CreateLiteral(sum)};");
			}
		}
	}

	public void AppendTryGetNonEnumeratedCount(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMembers<IMethodSymbol>("TryGetNonEnumeratedCount", m => SymbolEqualityComparer.Default.Equals(m.ReturnType, compilation.CreateBoolean())
		                                                                             && m.Parameters is [ { RefKind: RefKind.Out } ]
		                                                                             && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, compilation.CreateInt32()), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return {items.Count};");
		}
	}

	public void AppendWhere(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethod(nameof(Enumerable.Where), compilation.CreateIEnumerable(elementType), [ compilation.CreateFunc(elementType, compilation.CreateBoolean()) ], out var member))
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
				
				var item = CreateLiteral(items[i]);

				using (builder.AppendBlock($"if ({member.Parameters[0].Name}({item}))"))
				{
					builder.AppendLine($"yield return {item};");
				}
			}
		}
	}

	public void AppendToImmutableArray(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var immutableArrayType = compilation.GetTypeByType(typeof(ImmutableArray<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToImmutableArray", immutableArrayType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return ImmutableArray.Create({String.Join(", ", items.Select(CreateLiteral))});");
		}
	}

	public void AppendToArray(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var arrayType = compilation.CreateArrayTypeSymbol(elementType);

		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.ToArray), arrayType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return [{String.Join(", ", items.Select(CreateLiteral))}];");
		}
	}

	public void AppendImmutableList(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var immutableListType = compilation.GetTypeByType(typeof(ImmutableList<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToImmutableList", immutableListType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return ImmutableList.Create({String.Join(", ", items.Select(CreateLiteral))});");
		}
	}

	public void AppendToList(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var listType = compilation.GetTypeByType(typeof(List<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType(nameof(Enumerable.ToList), listType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return new List<{elementType.Name}>({String.Join(", ", items.Select(CreateLiteral))});");
		}
	}

	public void AppendToHashSet(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var hashSetType = compilation.GetTypeByType(typeof(HashSet<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToHashSet", hashSetType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return new HashSet<{elementType.Name}>({String.Join(", ", items.Distinct().Select(CreateLiteral))});");
		}
	}
}