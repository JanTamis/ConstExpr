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

public class EnumerableBuilder(Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, GenerationLevel level, string dataName) : BaseBuilder(elementType, compilation, level, loader, dataName)
{
	public bool AppendAggregate<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		// First overload: Aggregate<TSource>(Func<TSource, TSource, TSource> func)
		switch (method)
		{
			case { Name: nameof(Enumerable.Aggregate) }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType)
				     && method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, elementType, elementType)):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					switch (items.Length)
					{
						case 0:
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
							break;
						case 1 or 2:
							builder.AppendLine($"return {method.Parameters[0]}({items});");
							break;
						default:
						{
							if (isPerformance)
							{
								builder.AppendLine($"var result = {method.Parameters[0]}({items[0]}, {items[1]});");

								for (var i = 2; i < items.Length; i++)
								{
									if (i < items.Length - 1)
									{
										builder.AppendLine($"result = {method.Parameters[0]}(result, {items[i]});");
									}
									else
									{
										builder.AppendLine($"return {method.Parameters[0]}(result, {items[i]});");
									}
								}
							}
							else
							{
								builder.AppendLine($"var result = {GetDataName(method.ContainingType)}[0];");
								builder.AppendLine();

								using (builder.AppendBlock($"for (var i = 1; i < {GetDataName(method.ContainingType)}.Length; i++)", WhitespacePadding.After))
								{
									builder.AppendLine($"result = {method.Parameters[0]}(result, {GetDataName(method.ContainingType)}[i]);");
								}

								builder.AppendLine("return result;");
							}
							break;
						}
					}
				});

				return true;
			}
			// Second overload: Aggregate<TSource, TAccumulate>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
			case { Name: nameof(Enumerable.Aggregate) }
				when method.Parameters.AsSpan().EqualsTypes(method.ReturnType, compilation.CreateFunc(method.Parameters[0].Type, elementType, method.Parameters[0].Type)):
			{
				AppendMethod(builder, method, () =>
				{
					for (var i = 0; i < items.Length; i++)
					{
						var item = CreateLiteral(items[i]);

						if (i < items.Length - 1)
						{
							builder.AppendLine($"{method.Parameters[0]} = {method.Parameters[1]}({method.Parameters[0]}, {item});");
						}
						else
						{
							builder.AppendLine($"return {method.Parameters[1]}({method.Parameters[0]}, {item});");
						}
					}
				});

				return true;
			}
			// Third overload: Aggregate<TSource, TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
			case { Name: nameof(Enumerable.Aggregate), Parameters.Length: 3 }
				when method.Parameters[2].Type is INamedTypeSymbol { Arity: 2 } namedTypeSymbol
				     && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, compilation.CreateFunc(method.Parameters[0].Type, elementType, method.Parameters[0].Type))
				     && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, compilation.CreateFunc(method.Parameters[0].Type, namedTypeSymbol.TypeArguments[1]))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, namedTypeSymbol.TypeArguments[1]):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"var result = {method.Parameters[0]};");

					foreach (var item in items)
					{
						builder.AppendLine($"result = {method.Parameters[1]}(result, {item});");
					}

					builder.AppendLine($"return {method.Parameters[2]}(result);");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAny<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Any), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Boolean }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return {items.Any()};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.Any), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine(CreateReturnPadding("||", items.Select(s => $"{method.Parameters[0]}({s})")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAll<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.All), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine(CreateReturnPadding("&&", items.Select(s => $"{method.Parameters[0]}({s})")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAsEnumerable<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.AsEnumerable) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items)
					{
						builder.AppendLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendAverage<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Average), Parameters.Length: 0 }
				when (elementType.SpecialType == SpecialType.System_Int32 && method.ReturnType.SpecialType == SpecialType.System_Double
				      || elementType.SpecialType == SpecialType.System_Int64 && method.ReturnType.SpecialType == SpecialType.System_Double
				      || elementType.SpecialType == SpecialType.System_Single && method.ReturnType.SpecialType == SpecialType.System_Single
				      || elementType.SpecialType == SpecialType.System_Double && method.ReturnType.SpecialType == SpecialType.System_Double
				      || elementType.SpecialType == SpecialType.System_Decimal && method.ReturnType.SpecialType == SpecialType.System_Decimal):
			{
				AppendMethod(builder, method, () =>
				{
					var average = items
						.AsSpan()
						.Average(elementType);

					if (average is null)
					{
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.AppendLine($"return {CreateLiteral(average)};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.Average) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IAdditionOperators")?.Construct(method.ReturnType, method.ReturnType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IDivisionOperators")?.Construct(method.ReturnType, method.ReturnType, method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"{CreatePadding("+", "return", items.Select(s => $"{method.Parameters[0]}({s})"))} / {items.Length};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCast<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Cast), Parameters.Length: 0, TypeParameters.Length: 1 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(method.TypeArguments[0])):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items)
					{
						builder.AppendLine($"yield return ({method.TypeParameters[0]}){item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Count), Parameters.Length: 0 }
				when method.ReturnType.EqualsType(compilation.GetTypeByMetadataName("System.Numerics.INumberBase`1")?.Construct(method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.ReturnType is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true })
					{
						builder.AppendLine($"return {method.ReturnType}.CreateChecked({items.Length});");
					}
					else
					{
						builder.AppendLine($"return {items.Length.ToSpecialType(method.ReturnType.SpecialType)};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.Count) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && method.ReturnType.EqualsType(compilation.GetTypeByMetadataName("System.Numerics.INumberBase`1")?.Construct(method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.ReturnType is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true })
					{
						builder.AppendLine($"var result = {method.ReturnType}.Zero;");
					}
					else
					{
						builder.AppendLine($"var result = {0.ToSpecialType(method.ReturnType.SpecialType)};");
					}

					builder.AppendLine();

					using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})", WhitespacePadding.After))
					{
						using (builder.AppendBlock($"if ({method.Parameters[0]}(item))"))
						{
							builder.AppendLine("result++;");
						}
					}

					builder.AppendLine("return result;");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLongCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.LongCount), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Int64 }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return {(long) items.Length};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.LongCount), ReturnType.SpecialType: SpecialType.System_Int64 }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine(CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0]}({s}) ? 1L : 0L")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendDistinct<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Distinct), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.Distinct())
					{
						builder.AppendLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendDistinctBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "DistinctBy" }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType))
				     && compilation.TryGetFuncType(method.Parameters[0].Type, out var funcParameter, out var funcReturnType)
				     && funcParameter.EqualsType(elementType)
				     && (method.Parameters.AsSpan().EqualsTypes(method.Parameters[0].Type, compilation.CreateEqualityComparer(funcReturnType))
				         || method.Parameters.Length == 1):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.Parameters.Length == 1)
					{
						builder.AppendLine($"var seen = new HashSet<{funcReturnType}>({items.Distinct().Count()});");
					}
					else
					{
						builder.AppendLine($"var seen = new HashSet<{funcReturnType}>({method.Parameters[1]});");
					}

					builder.AppendLine();

					var dataName = GetDataName(method.ContainingType);

					using (builder.AppendBlock($"for (var i = 0; i < {dataName}.Length; i++)"))
					{
						builder.AppendLine($"var item = {dataName}[i];");
						builder.AppendLine();

						using (builder.AppendBlock("if (seen.Add(item))"))
						{
							builder.AppendLine("yield return item;");
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendElementAt<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ElementAt) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.AppendLine($"if ({method.Parameters[0]} < 0 || {method.Parameters[0]} >= {items.Length})");
						builder.AppendLine("\tthrow new ArgumentOutOfRangeException(\"Index out of range\");");
						builder.AppendLine();
						builder.AppendLine($"return {items}[{method.Parameters[0]}];");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.ElementAt) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.GetTypeByType(typeof(Index)))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]}.GetOffset({items.Length}) switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.AppendLine($"var index = {method.Parameters[0]}.GetOffset({items.Length});");
						builder.AppendLine();
						builder.AppendLine($"if (index < 0 || index >= {items.Length})");
						builder.AppendLine("\tthrow new ArgumentOutOfRangeException(\"Index out of range\");");
						builder.AppendLine();
						builder.AppendLine($"return {items}[index];");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendElementAtOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ElementAtOrDefault) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => default");
						}
					}
					else
					{
						builder.AppendLine($"if ({method.Parameters[0]} < 0 || {method.Parameters[0]} >= {items.Length})");
						builder.AppendLine("\treturn default;");
						builder.AppendLine();
						builder.AppendLine($"return {items}[{method.Parameters[0]}];");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.ElementAtOrDefault) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.GetTypeByType(typeof(Index)))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]}.GetOffset({items.Length}) switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine($"_ => {elementType.GetDefaultValue()}");
						}
					}
					else
					{
						builder.AppendLine($"var index = {method.Parameters[0]}.GetOffset({items.Length});");
						builder.AppendLine();
						builder.AppendLine($"if (index < 0 || index >= {items.Length})");
						builder.AppendLine($"\treturn {elementType.GetDefaultValue()};");
						builder.AppendLine();
						builder.AppendLine($"return {items}[index];");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendFirst<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.First), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length > 0)
					{
						builder.AppendLine($"return {items[0]};");
					}
					else
					{
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.First) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Length; i++)
						{
							if (i != 0)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.AppendLine();
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
						}
						else
						{
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
						}
					}
					else
					{
						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})", WhitespacePadding.After))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}(item))"))
							{
								builder.AppendLine("return item;");
							}
						}

						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendFirstOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.FirstOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length > 0)
					{
						builder.AppendLine($"return {items[0]};");
					}
					else
					{
						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.FirstOrDefault) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Length; i++)
						{
							if (i != 0)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.AppendLine();
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})", WhitespacePadding.After))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}(item))"))
							{
								builder.AppendLine("return item;");
							}
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndex<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Index" }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(compilation.GetTypeByType(typeof(ValueTuple<,>), compilation.CreateInt32(), elementType))):
			{
				AppendMethod(builder, method, () =>
				{
					for (var i = 0; i < items.Length; i++)
					{
						builder.AppendLine($"yield return ({i}, {items[i]});");
					}

					if (items.Length == 0)
					{
						builder.AppendLine("yield break;");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLast<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Last), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length > 0)
					{
						builder.AppendLine($"return {items[^1]};");
					}
					else
					{
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.Last) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = items.Length - 1; i >= 0; i--)
						{
							if (i != items.Length - 1)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.AppendLine();
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
						}
						else
						{
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
						}
					}
					else
					{
						using (builder.AppendBlock($"for (var i = {GetDataName(method.ContainingType)} - 1; i >= 0; i--)", WhitespacePadding.After))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}({GetDataName(method.ContainingType)}[i]))"))
							{
								builder.AppendLine($"return {GetDataName(method.ContainingType)}[i];");
							}
						}

						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLastOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.LastOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length > 0)
					{
						builder.AppendLine($"return {items[^1]};");
					}
					else
					{
						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.LastOrDefault) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = items.Length - 1; i >= 0; i--)
						{
							if (i != items.Length - 1)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.AppendLine();
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						using (builder.AppendBlock($"for (var i = {GetDataName(method.ContainingType)} - 1; i >= 0; i--)", WhitespacePadding.After))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}({GetDataName(method.ContainingType)}[i]))"))
							{
								builder.AppendLine($"return {GetDataName(method.ContainingType)}[i];");
							}
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOrder<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderBy), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.OrderBy(s => s))
					{
						builder.AppendLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOrderDescending<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderByDescending), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.OrderByDescending(s => s))
					{
						builder.AppendLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendReverse<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Reverse), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.Reverse())
					{
						builder.AppendLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSelect<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Select), Parameters.Length: 0, ReturnType: INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(namedTypeSymbol.TypeArguments[0]))
				     && method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, namedTypeSymbol.TypeArguments[0])):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						foreach (var item in items)
						{
							builder.AppendLine($"yield return {method.Parameters[0]}({item});");
						}
					}
					else
					{
						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})"))
						{
							builder.AppendLine($"yield return {method.Parameters[0]}(item);");
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSequenceEqual<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.SequenceEqual), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (!isPerformance && compilation.HasMember<IMethodSymbol>(typeof(Enumerable), nameof(Enumerable.SequenceEqual)))
					{
						builder.AppendLine($"return {method.Parameters[0]}.SequenceEqual([{String.Join(", ", items.Select(CreateLiteral))}]);");
					}
					else
					{
						if (compilation.HasMember<IMethodSymbol>(typeof(Enumerable), "TryGetNonEnumeratedCount"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}.TryGetNonEnumeratedCount(out var count) && count != {items.Length})", WhitespacePadding.After))
							{
								builder.AppendLine("return false;");
							}
						}

						builder.AppendLine($"using var e = {method.Parameters[0]}.GetEnumerator();");
						builder.AppendLine();

						if (!items.Any())
						{
							builder.AppendLine("return !e.MoveNext();");
						}

						builder.AppendLine(CreateReturnPadding("&&", items.Select(s => $"e.MoveNext() && {s} == e.Current").Append("!e.MoveNext()")));
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSingle<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Single), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					switch (items.Length)
					{
						case 0:
							builder.AppendLine("throw new InvalidOperationException(\"The input sequence is empty\");");
							break;
						case 1:
							builder.AppendLine($"return {items[0]};");
							break;
						default:
							builder.AppendLine("throw new InvalidOperationException(\"The input sequence contains more than one element\");");
							break;
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSingleOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.SingleOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, () =>
				{
					switch (items.Length)
					{
						case 1:
							builder.AppendLine($"return {items[0]};");
							break;
						default:
							builder.AppendLine($"return {elementType.GetDefaultValue()};");
							break;
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSum<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Sum), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(elementType, method.ReturnType)
				     && elementType.SpecialType is SpecialType.System_SByte
					     or SpecialType.System_Byte
					     or SpecialType.System_Int16
					     or SpecialType.System_UInt16
					     or SpecialType.System_Int32
					     or SpecialType.System_UInt32
					     or SpecialType.System_Single
					     or SpecialType.System_Double
					     or SpecialType.System_Decimal:
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return {items.AsSpan().Sum()};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendTryGetNonEnumeratedCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "TryGetNonEnumeratedCount", Parameters: [ { RefKind: RefKind.Out, Type.SpecialType: SpecialType.System_Boolean } ] }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateBoolean()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"{method.Parameters[0]} = {items.Length};");
					builder.AppendLine("return true;");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendWhere<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Where) }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType))
				     && method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Length; i++)
						{
							builder.AppendLine($"if ({method.Parameters[0]}({items[i]})) \tyield return {items[i]};");
						}
					}
					else
					{
						using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(method.ContainingType)}.Length; i++)"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}({GetDataName(method.ContainingType)}[i]))"))
							{
								builder.AppendLine($"yield return {GetDataName(method.ContainingType)}[i];");
							}
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToImmutableArray<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableArray.ToImmutableArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableArray<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return ImmutableArray.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToArray<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateArrayTypeSymbol(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendImmutableList<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableList.ToImmutableList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableList<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return ImmutableList.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToList<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(List<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToHashSet<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ToHashSet", Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(HashSet<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.AppendLine($"return [{items.Distinct()}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendMin<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Min), Parameters.Length: 0 }:
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.AppendLine($"return {items.Min()};");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendMax<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Max), Parameters.Length: 0 }:
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.AppendLine($"return {items.Max()};");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSkip<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Skip) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					using (builder.AppendBlock($"for (var i = Math.Min({method.Parameters[0]}, {items.Length}) i < {items.Length}; i++)"))
					{
						builder.AppendLine($"yield return {GetDataName(method.ContainingType)}[i];");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendTake<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Take) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					using (builder.AppendBlock($"for (var i = 0; i < Math.Min({method.Parameters[0]}, {items.Length}); i++) i < {items.Length}; i++)"))
					{
						builder.AppendLine($"yield return {GetDataName(method.ContainingType)}[i];");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCountBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "CountBy", TypeArguments: [ var keyType, .. ] }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, keyType))
				     && method.ReturnType is INamedTypeSymbol { Arity: 1 } enumerableType
				     && enumerableType.TypeArguments[0] is INamedTypeSymbol { TypeArguments: [ _, var numberType ] } keyValuePairType
				     && keyValuePairType.EqualsType(compilation.CreateKeyValuePair(keyType, numberType))
				     && enumerableType.EqualsType(compilation.CreateIEnumerable(keyValuePairType))
				     && numberType.EqualsType(compilation.GetTypeByMetadataName("System.Numerics.INumberBase`1")?.Construct(numberType)):
			{
				AppendMethod(builder, method, () =>
				{
					// Use fully qualified names to avoid namespace conflicts in generated code
					builder.AppendLine($"var counts = new Dictionary<{keyType}, {numberType}>({items.Length});");
					builder.AppendLine();

					// GetDataName(method.ContainingType) should resolve to the name of the const array field
					using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})", WhitespacePadding.After))
					{
						builder.AppendLine($"var key = {method.Parameters[0]}(item);");
						builder.AppendLine();

						if (compilation.GetTypeByMetadataName("System.Runtime.InteropServices.CollectionsMarshal")?.HasMember<IMethodSymbol>("GetValueRefOrAddDefault") == true)
						{
							builder.AppendLine("ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);");
							builder.AppendLine("count++;");
						}
						else
						{
							using (builder.AppendBlock("if (counts.TryGetValue(key, out var currentCount))"))
							{
								if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
								{
									builder.AppendLine($"counts[key] = currentCount + {numberType}.One;");
								}
								else
								{
									builder.AppendLine("counts[key] = currentCount + 1;");
								}
							}

							using (builder.AppendBlock("else"))
							{
								if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
								{
									builder.AppendLine($"counts.Add(key, {numberType}.One);");
								}
								else
								{
									builder.AppendLine("counts.Add(key, 1);");
								}
							}
						}
					}

					builder.AppendLine("return counts;");
				});

				return true;
			}
			case { Name: "CountBy", Parameters.Length: 0 }
				when method.ReturnType is INamedTypeSymbol { Arity: 1 } enumerableType
				     && enumerableType.TypeArguments[0] is INamedTypeSymbol { TypeArguments: [ _, var numberType ] } keyValuePairType
				     && keyValuePairType.EqualsType(compilation.CreateKeyValuePair(elementType, numberType))
				     && enumerableType.EqualsType(compilation.CreateIEnumerable(keyValuePairType))
				     && numberType.EqualsType(compilation.GetTypeByMetadataName("System.Numerics.INumberBase`1")?.Construct(numberType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
					{
						foreach (var item in items.CountBy())
						{
							if (item.Value == 1)
							{
								builder.AppendLine($"yield return {(LiteralString) keyValuePairType.Name}.Create({item.Key}, {numberType}.One);");
							}
							else
							{
								builder.AppendLine($"yield return {(LiteralString) keyValuePairType.Name}.Create({item.Key}, {numberType}.CreateChecked({item.Value}));");
							}
						}
					}
					else
					{
						foreach (var item in items.CountBy())
						{
							builder.AppendLine($"yield return {(LiteralString) keyValuePairType.Name}.Create({item.Key}, {item.Value};");
						}
					}
				});

				return true;
			}

			default:
				return false;
		}
	}

	public bool AppendZip<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		var types = method.Parameters
			.WhereSelect<IParameterSymbol, ITypeSymbol?>((x, out result) => compilation.TryGetIEnumerableType(x.Type, out result))
			.Prepend(elementType)
			.ToImmutableArray();

		switch (method)
		{
			case { Name: "Zip", Parameters.Length: > 0 }
				when method.ReturnType.EqualsType(compilation.CreateIEnumerable(compilation.CreateTupleTypeSymbol(types)))
				     && method.Parameters.Length == types.Length - 1:
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var parameter in method.Parameters)
					{
						builder.AppendLine($"using var {parameter}Enumerator = {parameter}.GetEnumerator();");
					}

					builder.AppendLine();

					using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(method.ContainingType)}.Length && {(LiteralString) String.Join(" && ", method.Parameters.Select(p => $"{p.Name}Enumerator.MoveNext()"))}; i++)"))
					{
						builder.AppendLine($"yield return ({GetDataName(method.ContainingType)}[i], {(LiteralString) String.Join(", ", method.Parameters.Select(p => $"{p.Name}Enumerator.Current"))});");
					}
				});


				return true;
			}
		}

		return false;
	}

	public bool AppendChunk<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Chunk", }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(compilation.CreateArrayTypeSymbol(elementType))):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.AppendLine("yield break;");
					}
					else
					{
						var dataName = GetDataName(method.ContainingType);

						using (builder.AppendBlock($"for (var i = 0; i < {dataName}.Length; i += {method.Parameters[0]})"))
						{
							builder.AppendLine($"yield return {dataName}.Slice(i, Math.Min({method.Parameters[0]}, {dataName}.Length - i)).ToArray();");
						}
					}
				});

				return true;
			}
		}

		return false;
	}

	public bool AppendExcept<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Except) }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType))
				     && (method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType))
				         || method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType), compilation.CreateEqualityComparer(elementType))):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.AppendLine("yield break;");

						return;
					}

					builder.AppendLine($"var set = new HashSet<{elementType}>({method.Parameters});");
					builder.AppendLine();

					using (builder.AppendBlock($"foreach (var item in {method.Parameters[0]})"))
					{
						using (builder.AppendBlock("if (set.Add(item))"))
						{
							builder.AppendLine("yield return item;");
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendExceptBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ExceptBy", Parameters.Length: 2 or 3 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType))
				     && method.Parameters[0].Type.EqualsType(compilation.CreateIEnumerable(elementType))
				     && compilation.TryGetFuncType(method.Parameters[1].Type, out var typeFunc, out var keyType)
				     && SymbolEqualityComparer.Default.Equals(typeFunc, elementType)
						 && (method.Parameters.Length == 2 || method.Parameters[2].Type.EqualsType(compilation.CreateEqualityComparer(keyType))):
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.AppendLine("yield break;");

						return;
					}

					if (method.Parameters.Length == 2)
					{
						builder.AppendLine($"var set = new HashSet<{keyType}>();");
					}
					else
					{
						builder.AppendLine($"var set = new HashSet<{keyType}>({method.Parameters[2]});");
					}
					
					builder.AppendLine();

					using (builder.AppendBlock($"foreach (var item in {method.Parameters[0]})"))
					{
						using (builder.AppendBlock($"if (set.Add({method.Parameters[1]}(item)))"))
						{
							builder.AppendLine("yield return item;");
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}
}