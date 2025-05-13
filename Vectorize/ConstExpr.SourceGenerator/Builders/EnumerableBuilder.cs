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

public class EnumerableBuilder(Compilation compilation, ITypeSymbol elementType, MetadataLoader loader, GenerationLevel level, int hashCode) : BaseBuilder(elementType, compilation, level, loader, hashCode)
{
	public bool AppendAggregate(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		// First overload: Aggregate<TSource>(Func<TSource, TSource, TSource> func)
		var funcType = compilation.CreateFunc(elementType, elementType, elementType);

		switch (method)
		{
			case { Name: nameof(Enumerable.Aggregate) }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType)
				     && method.Parameters.AsSpan().EqualsTypes(funcType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					switch (items.Count)
					{
						case 0:
							builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no elements\");");
							break;
						case 1 or 2:
							builder.AppendLine($"return {items};");
							break;
						default:
						{
							if (isPerformance)
							{
								builder.AppendLine($"var result = {method.Parameters[0]}({items[0]}, {items[1]});");

								for (var i = 2; i < items.Count; i++)
								{
									if (i < items.Count - 1)
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

								using (builder.AppendBlock($"for (var i = 1; i < {GetDataName(method.ContainingType)}.Length; i++)"))
								{
									builder.AppendLine($"result = {method.Parameters[0]}(result, {GetDataName(method.ContainingType)}[i]);");
								}

								builder.AppendLine();
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
				AppendMethod(builder, method, items, () =>
				{
					for (var i = 0; i < items.Count; i++)
					{
						var item = CreateLiteral(items[i]);

						if (i < items.Count - 1)
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
				AppendMethod(builder, method, items, () =>
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

	public bool AppendAny(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Any), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Boolean }:
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return {items.Any()};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.Any), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine(CreateReturnPadding("||", items.Select(s => $"{method.Parameters[0]}({s})")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAll(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.All), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine(CreateReturnPadding("&&", items.Select(s => $"{method.Parameters[0]}({s})")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendAsEnumerable(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.AsEnumerable) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items, () =>
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

	public bool AppendAverage(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
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


				AppendMethod(builder, method, items, () =>
				{
					var average = items.Average(elementType);

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
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IAdditionOperators").Construct(method.ReturnType, method.ReturnType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IDivisionOperators").Construct(method.ReturnType, method.ReturnType, method.ReturnType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"{CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0]}({s})"))} / {items.Count};");
				});

				return true;
			}
			default:
				return false;
		}

	}

	public bool AppendCast(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Cast), Parameters.Length: 0, TypeParameters.Length: 1 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(method.TypeArguments[0])):
			{
				AppendMethod(builder, method, items, () =>
				{
					foreach (var item in items)
					{
						builder.AppendLine($"yield return ({method.TypeParameters[0]})Convert.ChangeType({item}, typeof({method.TypeParameters[0]}));");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendCount(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Count), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Int32 }:
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return {items.Count};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.Count), ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine(CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0]}({s}) ? 1 : 0")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLongCount(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.LongCount), Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Int64 }:
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return {(long) items.Count};");
				});

				return true;
			}
			case { Name: nameof(Enumerable.LongCount), ReturnType.SpecialType: SpecialType.System_Int64 }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine(CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0]}({s}) ? 1L : 0L")));
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendDistinct(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Distinct) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, () =>
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

	public bool AppendElementAt(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ElementAt) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.AppendLine($"if ({method.Parameters[0]} < 0 || {method.Parameters[0]} >= {items.Count})");
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]}.GetOffset({items.Count}) switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
					else
					{
						builder.AppendLine($"var index = {method.Parameters[0]}.GetOffset({items.Count});");
						builder.AppendLine();
						builder.AppendLine($"if (index < 0 || index >= {items.Count})");
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

	public bool AppendElementAtOrDefault(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ElementAtOrDefault) }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]} switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine("_ => default");
						}
					}
					else
					{
						builder.AppendLine($"if ({method.Parameters[0]} < 0 || {method.Parameters[0]} >= {items.Count})");
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						using (builder.AppendBlock($"return {method.Parameters[0]}.GetOffset({items.Count}) switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{i} => {items[i]},");
							}

							builder.AppendLine($"_ => {elementType.GetDefaultValue()}");
						}
					}
					else
					{
						builder.AppendLine($"var index = {method.Parameters[0]}.GetOffset({items.Count});");
						builder.AppendLine();
						builder.AppendLine($"if (index < 0 || index >= {items.Count})");
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

	public bool AppendFirst(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.First), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					if (items.Count > 0)
					{
						builder.AppendLine($"return {items.First()};");
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Count; i++)
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
					else
					{
						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}(item))"))
							{
								builder.AppendLine("return item;");
							}
						}
						
						builder.AppendLine();
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendFirstOrDefault(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.FirstOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					if (items.Count > 0)
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Count; i++)
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

						if (items.Count > 0)
						{
							builder.AppendLine();
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						using (builder.AppendBlock($"foreach (var item in {GetDataName(method.ContainingType)})"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}(item))"))
							{
								builder.AppendLine("return item;");
							}
						}

						builder.AppendLine();
						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendIndex(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "Index" }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ValueTuple<,>), compilation.CreateInt32(), elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					for (var i = 0; i < items.Count; i++)
					{
						builder.AppendLine($"yield return ({i}, {items[i]});");
					}

					if (items.Count == 0)
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

	public bool AppendLast(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Last), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					if (items.Count > 0)
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = items.Count - 1; i >= 0; i--)
						{
							if (i != items.Count - 1)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
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
					else
					{
						using (builder.AppendBlock($"for (var i = {GetDataName(method.ContainingType)} - 1; i >= 0; i--)"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}({GetDataName(method.ContainingType)}[i]))"))
							{
								builder.AppendLine($"return {GetDataName(method.ContainingType)}[i];");
							}
						}

						builder.AppendLine();
						builder.AppendLine("throw new InvalidOperationException(\"Sequence contains no matching element\");");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLastOrDefault(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.LastOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					if (items.Count > 0)
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
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = items.Count - 1; i >= 0; i--)
						{
							if (i != items.Count - 1)
							{
								builder.AppendLine();
							}

							using (builder.AppendBlock($"if ({method.Parameters[0]}({items[i]}))"))
							{
								builder.AppendLine($"return {items[i]};");
							}
						}

						if (items.Count > 0)
						{
							builder.AppendLine();
						}

						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
					else
					{
						using (builder.AppendBlock($"for (var i = {GetDataName(method.ContainingType)} - 1; i >= 0; i--)"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}({GetDataName(method.ContainingType)}[i]))"))
							{
								builder.AppendLine($"return {GetDataName(method.ContainingType)}[i];");
							}
						}

						builder.AppendLine();
						builder.AppendLine($"return {elementType.GetDefaultValue()};");
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendOrder(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderBy), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items, () =>
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

	public bool AppendOrderDescending(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.OrderByDescending), }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items, () =>
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

	public bool AppendReverse(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Reverse), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items, () =>
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

	public bool AppendSelect(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Select), Parameters.Length: 0, ReturnType: INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(namedTypeSymbol.TypeArguments[0]))
				     && method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, namedTypeSymbol.TypeArguments[0])):
			{
				AppendMethod(builder, method, items, isPerformance =>
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
						using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(method.ContainingType)}.Length; i++)"))
						{
							builder.AppendLine($"yield return {method.Parameters[0]}({GetDataName(method.ContainingType)}[i]);");
						}
					}
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendSequenceEqual(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.SequenceEqual), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.AsSpan().EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (!isPerformance && compilation.HasMember<IMethodSymbol>(typeof(Enumerable), nameof(Enumerable.SequenceEqual)))
					{
						builder.AppendLine($"return {method.Parameters[0]}.SequenceEqual([{String.Join(", ", items.Select(CreateLiteral))}]);");
					}
					else
					{
						if (compilation.HasMember<IMethodSymbol>(typeof(Enumerable), "TryGetNonEnumeratedCount"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0]}.TryGetNonEnumeratedCount(out var count) && count != {items.Count})"))
							{
								builder.AppendLine("return false;");
							}

							builder.AppendLine();
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

	public bool AppendSingle(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Single), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					switch (items.Count)
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

	public bool AppendSingleOrDefault(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.SingleOrDefault), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				AppendMethod(builder, method, items, () =>
				{
					switch (items.Count)
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

	public bool AppendSum(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
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
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return {items.Sum()};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendTryGetNonEnumeratedCount(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "TryGetNonEnumeratedCount", Parameters: [ { RefKind: RefKind.Out, Type.SpecialType: SpecialType.System_Boolean } ] }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateBoolean()):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return {method.Parameters[0]} = {items.Count};");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendWhere(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.Where) }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateIEnumerable(elementType))
				     && method.Parameters.AsSpan().EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				AppendMethod(builder, method, items, isPerformance =>
				{
					if (isPerformance)
					{
						for (var i = 0; i < items.Count; i++)
						{
							if (i != 0)
							{
								builder.AppendLine();
							}

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

	public bool AppendToImmutableArray(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableArray.ToImmutableArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableArray<>), elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return ImmutableArray.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToArray(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToArray), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateArrayTypeSymbol(elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendImmutableList(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(ImmutableList.ToImmutableList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ImmutableList<>), elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return ImmutableList.Create({items});");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToList(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: nameof(Enumerable.ToList), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(List<>), elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return [{items}];");
				});

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendToHashSet(IMethodSymbol method, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (method)
		{
			case { Name: "ToHashSet", Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(HashSet<>), elementType)):
			{
				AppendMethod(builder, method, items, () =>
				{
					builder.AppendLine($"return new HashSet<{elementType}>({items.Distinct()});");
				});

				return true;
			}
			default:
				return false;
		}
	}
}