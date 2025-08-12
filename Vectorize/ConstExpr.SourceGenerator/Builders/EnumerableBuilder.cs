using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SourceGen.Utilities.Helpers;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Builders;

public class EnumerableBuilder(Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, GenerationLevel level, string dataName) : BaseBuilder(elementType, compilation, level, loader, dataName)
{
	public bool AppendAggregate<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
							builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
							break;
						case 1 or 2:
							builder.WriteLine($"return {method.Parameters[0]}({items});");
							break;
						default:
						{
							if (isPerformance)
							{
								builder.WriteLine($"var result = {method.Parameters[0]}({items[0]}, {items[1]});");

								for (var i = 2; i < items.Length; i++)
								{
									if (i < items.Length - 1)
									{
										builder.WriteLine($"result = {method.Parameters[0]}(result, {items[i]});");
									}
									else
									{
										builder.WriteLine($"return {method.Parameters[0]}(result, {items[i]});");
									}
								}
							}
							else
							{
								builder.WriteLine($$"""
									var result = {{DataName:literal}}[0];

									for (var i = 1; i < {{DataName:literal}}.Length; i++)
									{
										result = {{method.Parameters[0]}}(result, {{DataName:literal}}[i]);
									}

									return result;
									""");
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
							builder.WriteLine($"{method.Parameters[0]} = {method.Parameters[1]}({method.Parameters[0]}, {item});");
						}
						else
						{
							builder.WriteLine($"return {method.Parameters[1]}({method.Parameters[0]}, {item});");
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
					builder.WriteLine($"var result = {method.Parameters[0]};");

					foreach (var item in items)
					{
						builder.WriteLine($"result = {method.Parameters[1]}(result, {item});");
					}

					builder.WriteLine($"return {method.Parameters[2]}(result);");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAny<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Any), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Boolean }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return {items.Any()};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.Any), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					CreateReturnPadding(builder, "||", items.Select(s => $"{method.Parameters[0]}({s})")).WriteLine();
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAll<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.All), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					CreateReturnPadding(builder, "&&", items.Select(s => $"{method.Parameters[0]}({s})")).WriteLine();
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAsEnumerable<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendAverage<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Average), Parameters.Length: 0 }
				when elementType.SpecialType == SpecialType.System_Int32 && method.ReturnType.SpecialType == SpecialType.System_Double
				     || elementType.SpecialType == SpecialType.System_Int64 && method.ReturnType.SpecialType == SpecialType.System_Double
				     || elementType.SpecialType == SpecialType.System_Single && method.ReturnType.SpecialType == SpecialType.System_Single
				     || elementType.SpecialType == SpecialType.System_Double && method.ReturnType.SpecialType == SpecialType.System_Double
				     || elementType.SpecialType == SpecialType.System_Decimal && method.ReturnType.SpecialType == SpecialType.System_Decimal:
			{
				AppendMethod(builder, method, () =>
				{
					var average = items
						.AsSpan()
						.Average(elementType);

					if (average is null)
					{
						builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.WriteLine($"return {average};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.Average) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByName("System.Numerics.IAdditionOperators", method.ReturnType, method.ReturnType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByName("System.Numerics.IDivisionOperators", method.ReturnType, method.ReturnType, method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					CreatePadding(builder, "+", "return", items.Select(s => $"{method.Parameters[0]}({s})"), isEnding: false).WriteLine($" / {items.Length};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCast<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"yield return ({method.TypeParameters[0]}){item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Count), Parameters.Length: 0 }
				when method.ReturnType.EqualsType(compilation.GetTypeByName("System.Numerics.INumberBase", method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.ReturnType is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true })
					{
						builder.WriteLine($"return {method.ReturnType}.CreateChecked({items.Length});");
					}
					else
					{
						builder.WriteLine($"return {items.Length.ToSpecialType(method.ReturnType.SpecialType)};");
					}
				});

				return true;
			}
			case { Name: nameof(Enumerable.Count) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && method.ReturnType.EqualsType(compilation.GetTypeByName("System.Numerics.INumberBase", method.ReturnType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.ReturnType is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true })
					{
						builder.WriteLine($"var result = {method.ReturnType}.Zero;");
					}
					else
					{
						builder.WriteLine($"var result = {0.ToSpecialType(method.ReturnType.SpecialType)};");
					}

					builder.WriteLine($$"""

						foreach (var item in {{DataName:literal}})
						{
							if ({{method.Parameters[0]}}(item))
							{
								result++;
							}
						}

						return result;
						""");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLongCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.LongCount), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Int64 }:
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return {(long) items.Length};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.LongCount), ReturnType.SpecialType: SpecialType.System_Int64 }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, () =>
				{
					CreateReturnPadding(builder, "+", items.Select(s => $"{method.Parameters[0]}({s}) ? 1L : 0L")).WriteLine();
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendDistinct<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendDistinctBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"var seen = new HashSet<{funcReturnType}>({items.Distinct().Count()});");
					}
					else
					{
						builder.WriteLine($"var seen = new HashSet<{funcReturnType}>({method.Parameters[1]});");
					}

					builder.WriteLine($$"""

						for (var i = 0; i < {{DataName:literal}}.Length; i++)
						{
							var item = {{DataName:literal}}[i];
							
							if (seen.Add({{method.Parameters[0]}}(item)))
							{
								yield return item;
							}
						}
						""");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendElementAt<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						using (builder.WriteBlock($"return {method.Parameters[0]} switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.WriteLine($"{i} => {items[i]},");
							}

							builder.WriteLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.WriteLine($$"""
							if ({method.Parameters[0]} < 0 || {method.Parameters[0]} >= {{items.Length}})
							{
								throw new ArgumentOutOfRangeException("Index out of range");
							}

							return {{items}}[{{method.Parameters[0]}}];
							""");
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
						using (builder.WriteBlock($"return {method.Parameters[0]}.GetOffset({items.Length}) switch", "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.WriteLine($"{i} => {items[i]},");
							}

							builder.WriteLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.WriteLine($$"""
							var index = {{method.Parameters[0]}}.GetOffset({{items.Length}});

							if (index < 0 || index >= {{items.Length}})
							{
								throw new ArgumentOutOfRangeException("Index out of range");
							}

							return {{items}}[index];
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendElementAtOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						using (builder.WriteBlock($"return {method.Parameters[0]} switch", end: "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.WriteLine($"{i} => {items[i]},");
							}

							builder.WriteLine("_ => default");
						}
					}
					else
					{
						builder.WriteLine($$"""
							if ({{method.Parameters[0]}} < 0 || {{method.Parameters[0]}} >= {{items.Length}})
							{
								return default;
							}

							return {{items}}[{{method.Parameters[0]}}];
							""");
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
						using (builder.WriteBlock($"return {method.Parameters[0]}.GetOffset({items.Length}) switch", end: "};"))
						{
							for (var i = 0; i < items.Length; i++)
							{
								builder.WriteLine($"{i} => {items[i]},");
							}

							builder.WriteLine($"_ => {elementType.GetDefaultValue()}");
						}
					}
					else
					{
						builder.WriteLine($$"""
							var index = {{method.Parameters[0]}}.GetOffset({{items.Length}});

							if (index < 0 || index >= {{items.Length}})
							{
								return {{elementType.GetDefaultValue()}};
							}

							return {{items}}[index];
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendFirst<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return {items[0]};");
					}
					else
					{
						builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
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
							builder.WriteLineIf(i != 0, true);

							using (builder.WriteBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.WriteLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.WriteLine(true);
							builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
						}
						else
						{
							builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
						}
					}
					else
					{
						builder.WriteLine($$"""
							foreach (var item in {{DataName:literal}})
							{
								if ({{method.Parameters[0]}}(item))
								{
									return item;
								}
							}
							
							throw new InvalidOperationException("Sequence contains no matching element");
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendFirstOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return {items[0]};");
					}
					else
					{
						builder.WriteLine($"return {elementType.GetDefaultValue()};");
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
							builder.WriteLineIf(i != 0);

							using (builder.WriteBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.WriteLine($"return {items[i]};");
							}
						}

						builder.WriteLineIf(items.Length > 0);
						builder.WriteLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						builder.WriteLine($$"""
							foreach (var item in {{DataName:literal}})
							{
								if ({{method.Parameters[0]}}(item))
								{
									return item;
								}
							}
							
							return {{elementType.GetDefaultValue()}};
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndex<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"yield return ({i}, {items[i]});");
					}

					builder.WriteLineIf(items.Length == 0, "yield break;");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLast<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return {items[^1]};");
					}
					else
					{
						builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
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
							builder.WriteLineIf(i != items.Length - 1);

							using (builder.WriteBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.WriteLine($"return {items[i]};");
							}
						}

						if (items.Length > 0)
						{
							builder.WriteLine();
							builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
						}
						else
						{
							builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
						}
					}
					else
					{
						builder.WriteLine($$"""
							for (var i = {{DataName:literal}}.Length - 1; i >= 0; i--)
							{
								if ({{method.Parameters[0]}}({{DataName:literal}}[i]))
								{
									return {{DataName:literal}}[i];
								}
							}

							throw new InvalidOperationException("Sequence contains no matching element");
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLastOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return {items[^1]};");
					}
					else
					{
						builder.WriteLine($"return {elementType.GetDefaultValue()};");
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
							builder.WriteLineIf(i != items.Length - 1);

							using (builder.WriteBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.WriteLine($"return {items[i]};");
							}
						}

						builder.WriteLineIf(items.Length > 0);
						builder.WriteLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						builder.WriteLine($$"""
							for (var i = {{DataName:literal}}.Length - 1; i >= 0; i--)
							{
								if ({{method.Parameters[0]}}({{DataName:literal}}[i]))
								{
									return {{DataName:literal}}[i];
								}
							}
							
							return {{elementType.GetDefaultValue()}};
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOrder<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderBy), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.OrderBy(o => o))
					{
						builder.WriteLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOrderDescending<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderByDescending), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					foreach (var item in items.OrderByDescending(o => o))
					{
						builder.WriteLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendReverse<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"yield return {item};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSelect<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
							builder.WriteLine($"yield return {method.Parameters[0]}({item});");
						}
					}
					else
					{
						builder.WriteLine($$"""
							foreach (var item in {{DataName:literal}})
							{
								yield return {{method.Parameters[0]}}(item);
							}
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSequenceEqual<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.SequenceEqual), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items.AsSpan(), isPerformance =>
				{
					if (!isPerformance && compilation.HasMethod(typeof(Enumerable), nameof(Enumerable.SequenceEqual)))
					{
						builder.WriteLine($"return {method.Parameters[0]}.SequenceEqual([{items}]);");
					}
					else
					{
						if (compilation.HasMethod(typeof(Enumerable), "TryGetNonEnumeratedCount"))
						{
							using (builder.WriteBlock($"if ({method.Parameters[0]}.TryGetNonEnumeratedCount(out var count) && count != {items.Length})"))
							{
								builder.WriteLine("return false;");
							}
							
							builder.WriteLine();
						}

						builder.WriteLine($"using var e = {method.Parameters[0]}.GetEnumerator();");
						builder.WriteLine();

						if (!items.Any())
						{
							builder.WriteLine("return !e.MoveNext();");
						}

						CreateReturnPadding(builder, "&&", items.Select(s => $"e.MoveNext() && {s} == e.Current").Append("!e.MoveNext()")).WriteLine();
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSingle<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
							builder.WriteLine("throw new InvalidOperationException(\"The input sequence is empty\");");
							break;
						case 1:
							builder.WriteLine($"return {items[0]};");
							break;
						default:
							builder.WriteLine("throw new InvalidOperationException(\"The input sequence contains more than one element\");");
							break;
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSingleOrDefault<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
							builder.WriteLine($"return {items[0]};");
							break;
						default:
							builder.WriteLine($"return {elementType.GetDefaultValue()};");
							break;
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSum<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
					builder.WriteLine($"return {items.AsSpan().Sum()};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendTryGetNonEnumeratedCount<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "TryGetNonEnumeratedCount", Parameters: [ { RefKind: RefKind.Out, Type.SpecialType: SpecialType.System_Boolean } ] }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateBoolean()):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"""
						{method.Parameters[0]} = {items.Length};
						return true
						""");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendWhere<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
							builder.WriteLine($"if ({method.Parameters[0]}({items[i]})) \tyield return {items[i]};");
						}
					}
					else
					{
						builder.WriteLine($$"""
							for (var i = 0; i < {{DataName:literal}}.Length; i++)
							{
								if ({{method.Parameters[0]}}({{DataName:literal}}[i]))
								{
									yield return {{DataName:literal}}[i];
								}
							}
							""");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToImmutableArray<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableArray.ToImmutableArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableArray<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return ImmutableArray.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToArray<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateArrayTypeSymbol(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendImmutableList<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableList.ToImmutableList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableList<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return ImmutableList.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToList<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(List<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToHashSet<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "ToHashSet", Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(HashSet<>), elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($"return [{items.Distinct()}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendMin<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Min), Parameters.Length: 0 }:
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.WriteLine($"return {items.Min()};");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendMax<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Max), Parameters.Length: 0 }:
			{
				AppendMethod(builder, method, () =>
				{
					if (items.Length == 0)
					{
						builder.WriteLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
					}
					else
					{
						builder.WriteLine($"return {items.Max()};");
					}
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSkip<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Skip) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($$"""
						for (var i = Math.Min({{method.Parameters[0]}}, {{items.Length}}) i < {{items.Length}}; i++)
						{
							yield return {{DataName:literal}}[i];
						} 
						""");
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendTake<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Take) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, () =>
				{
					builder.WriteLine($$"""
						for (var i = 0; i < Math.Min({{method.Parameters[0]}}, {{items.Length}}); i++) i < {{items.Length}}; i++)
						{
							yield return {{DataName:literal}}[i];
						}
						""");
				});
				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCountBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		switch (method)
		{
			case { Name: "CountBy", TypeArguments: [ var keyType, .. ] }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, keyType))
				     && method.ReturnType is INamedTypeSymbol { Arity: 1 } enumerableType
				     && enumerableType.TypeArguments[0] is INamedTypeSymbol { TypeArguments: [ _, var numberType ] } keyValuePairType
				     && keyValuePairType.EqualsType(compilation.CreateKeyValuePair(keyType, numberType))
				     && enumerableType.EqualsType(compilation.CreateIEnumerable(keyValuePairType))
				     && numberType.EqualsType(compilation.GetTypeByName("System.Numerics.INumberBase", numberType)):
			{
				AppendMethod(builder, method, () =>
				{
					// Use fully qualified names to avoid namespace conflicts in generated code
					builder.WriteLine($"var counts = new Dictionary<{keyType}, {numberType}>({items.Length});");
					builder.WriteLine();

					// GetDataName(method.ContainingType) should resolve to the name of the const array field
					using (builder.WriteBlock($"foreach (var item in {DataName:literal})"))
					{
						builder.WriteLine($"var key = {method.Parameters[0]}(item);");
						builder.WriteLine();

						if (compilation.GetTypeByName("System.Runtime.InteropServices.CollectionsMarshal", ReadOnlySpan<ITypeSymbol>.Empty)?.HasMethod("GetValueRefOrAddDefault") == true)
						{
							builder.WriteLine("ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);");
							builder.WriteLine("count++;");
						}
						else
						{
							using (builder.WriteBlock("if (counts.TryGetValue(key, out var currentCount))"))
							{
								if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
								{
									builder.WriteLine($"counts[key] = currentCount + {numberType}.One;");
								}
								else
								{
									builder.WriteLine("counts[key] = currentCount + 1;");
								}
							}

							using (builder.WriteBlock("else"))
							{
								if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
								{
									builder.WriteLine($"counts.Add(key, {numberType}.One);");
								}
								else
								{
									builder.WriteLine("counts.Add(key, 1);");
								}
							}
						}
					}

					builder.WriteLine();
					builder.WriteLine("return counts;");
				});

				return true;
			}
			case { Name: "CountBy", Parameters.Length: 0 }
				when method.ReturnType is INamedTypeSymbol { Arity: 1 } enumerableType
				     && enumerableType.TypeArguments[0] is INamedTypeSymbol { TypeArguments: [ _, var numberType ] } keyValuePairType
				     && keyValuePairType.EqualsType(compilation.CreateKeyValuePair(elementType, numberType))
				     && enumerableType.EqualsType(compilation.CreateIEnumerable(keyValuePairType))
				     && numberType.EqualsType(compilation.GetTypeByName("System.Numerics.INumberBase", numberType)):
			{
				AppendMethod(builder, method, () =>
				{
					if (method.TypeArguments.Contains(numberType, SymbolEqualityComparer.Default))
					{
						foreach (var item in items.CountBy())
						{
							if (item.Value == 1)
							{
								builder.WriteLine($"yield return {keyValuePairType.Name:literal}.Create({item.Key}, {numberType}.One);");
							}
							else
							{
								builder.WriteLine($"yield return {keyValuePairType.Name:literal}.Create({item.Key}, {numberType}.CreateChecked({item.Value}));");
							}
						}
					}
					else
					{
						foreach (var item in items.CountBy())
						{
							builder.WriteLine($"yield return {keyValuePairType.Name:literal}.Create({item.Key}, {item.Value};");
						}
					}
				});

				return true;
			}

			default:
				return false;
		}
	}

	public bool AppendZip<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
	{
		var types = method.Parameters
			.Where(x => compilation.TryGetIEnumerableType(x.Type, true, out _))
			.Select(x => { compilation.TryGetIEnumerableType(x.Type, true, out var result); return result; })
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
						builder.WriteLine($"using var {parameter}Enumerator = {parameter}.GetEnumerator();");
					}

					builder.WriteLine($$"""

						for (var i = 0; i < {{DataName:literal}}.Length && {{String.Join(" && ", method.Parameters.Select(p => $"{p.Name}Enumerator.MoveNext()")):literal}}; i++)
						{
							yield return ({{DataName:literal}}[i], {{String.Join(", ", method.Parameters.Select(p => $"{p.Name}Enumerator.Current")):literal}});
						}
						""");
				});

				return true;
			}
		}

		return false;
	}

	public bool AppendChunk<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine("yield break;");
					}
					else
					{
						builder.WriteLine($$"""
							for (var i = 0; i < {{DataName:literal}}.Length; i += {{method.Parameters[0]}})
							{
									yield return {{DataName:literal}}.Slice(i, Math.Min({{method.Parameters[0]}}, {{DataName:literal}}.Length - i)).ToArray();
							}
							""");
					}
				});

				return true;
			}
		}

		return false;
	}

	public bool AppendExcept<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return Enumerable.Empty<{elementType}>();");

						return;
					}

					builder.WriteLine($$"""
						var set = new HashSet<{{elementType}}>({{items}});

						foreach (var item in {{method.Parameters[0]}})
						{
							if (set.Add(item))
							{
								yield return item;
							}
						}
						""");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendExceptBy<T>(IMethodSymbol method, ImmutableArray<T> items, IndentedCodeWriter builder)
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
						builder.WriteLine($"return Enumerable.Empty<{elementType}>();");

						return;
					}

					if (method.Parameters.Length == 2)
					{
						builder.WriteLine($"var set = new HashSet<{keyType}>();");
					}
					else
					{
						builder.WriteLine($"var set = new HashSet<{keyType}>({method.Parameters[2]});");
					}

					builder.WriteLine($$"""
						
						foreach (var item in {{method.Parameters[0]}})
						{
							if (set.Add({{method.Parameters[1]}}(item)))
							{
								yield return item;
							}
						}
						""");
				});

				return true;
			}
			default:
				return false;
		}
	}
}