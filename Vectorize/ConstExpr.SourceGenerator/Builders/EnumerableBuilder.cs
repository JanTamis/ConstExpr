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
				     && method.Parameters.EqualsTypes(funcType):
			{
				using (AppendMethod(builder, method))
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
							builder.AppendLine($"return {method.Parameters[0].Name}({CreateLiteral(items[0])}, {CreateLiteral(items[1])});");
							break;
						default:
						{
							if (IsPerformance(level, items.Count))
							{
								builder.AppendLine($"var result = {method.Parameters[0].Name}({CreateLiteral(items[0])}, {CreateLiteral(items[1])});");

								for (var i = 2; i < items.Count; i++)
								{
									if (i < items.Count - 1)
									{
										builder.AppendLine($"result = {method.Parameters[0].Name}(result, {CreateLiteral(items[i])});");
									}
									else
									{
										builder.AppendLine($"return {method.Parameters[0].Name}(result, {CreateLiteral(items[i])});");
									}
								}
							}
							else
							{
								builder.AppendLine($"var result = {GetDataName(method.ContainingType)}[0];");
								builder.AppendLine();

								using (builder.AppendBlock($"for (var i = 1; i < {GetDataName(method.ContainingType)}.Length; i++)"))
								{
									builder.AppendLine($"result = {method.Parameters[0].Name}(result, {GetDataName(method.ContainingType)}[i]);");
								}

								builder.AppendLine();
								builder.AppendLine($"return result;");
							}
							break;
						}
					}
				}

				return true;
			}
			// Second overload: Aggregate<TSource, TAccumulate>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
			case { Name: nameof(Enumerable.Aggregate) }
				when method.Parameters.EqualsTypes(method.ReturnType, compilation.CreateFunc(method.Parameters[0].Type, elementType, method.Parameters[0].Type)):
			{
				using (AppendMethod(builder, method))
				{
					// builder.AppendLine($"var result = {member.Parameters[0].Name};");

					for (var i = 0; i < items.Count; i++)
					{
						var item = CreateLiteral(items[i]);

						if (i < items.Count - 1)
						{
							builder.AppendLine($"{method.Parameters[0].Name} = {method.Parameters[1].Name}({method.Parameters[0].Name}, {item});");
						}
						else
						{
							builder.AppendLine($"return {method.Parameters[1].Name}({method.Parameters[0].Name}, {item});");
						}
					}
				}

				return true;
			}
			// Third overload: Aggregate<TSource, TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
			case { Name: nameof(Enumerable.Aggregate), Parameters.Length: 3 }
				when method.Parameters[2].Type is INamedTypeSymbol { Arity: 2 } namedTypeSymbol
				     && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, compilation.CreateFunc(method.Parameters[0].Type, elementType, method.Parameters[0].Type))
				     && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, compilation.CreateFunc(method.Parameters[0].Type, namedTypeSymbol.TypeArguments[1]))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, namedTypeSymbol.TypeArguments[1]):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"var result = {method.Parameters[0].Name};");

					foreach (var item in items)
					{
						builder.AppendLine($"result = {method.Parameters[1].Name}(result, {CreateLiteral(item)});");
					}

					builder.AppendLine($"return {method.Parameters[2].Name}(result);");
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.Any())};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.Any), ReturnType.SpecialType: SpecialType.System_Boolean }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine(CreateReturnPadding("||", items.Select(s => $"{method.Parameters[0].Name}({CreateLiteral(s)})")));
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine(CreateReturnPadding("&&", items.Select(s => $"{method.Parameters[0].Name}({CreateLiteral(s)})")));
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				using (AppendMethod(builder, method))
				{
					foreach (var item in items)
					{
						builder.AppendLine($"yield return {CreateLiteral(item)};");
					}
				}

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
				var average = items.Average(elementType);

				using (AppendMethod(builder, method))
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

				return true;
			}
			case { Name: nameof(Enumerable.Average) }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IAdditionOperators").Construct(method.ReturnType, method.ReturnType, method.ReturnType))
				     && IsEqualSymbol(method.ReturnType, compilation.GetTypeByMetadataName("System.Numerics.IDivisionOperators").Construct(method.ReturnType, method.ReturnType, method.ReturnType)):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"{CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0].Name}({CreateLiteral(s)})"))} / {CreateLiteral(items.Count)};");
				}

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
				using (AppendMethod(builder, method))
				{
					foreach (var item in items)
					{
						builder.AppendLine($"yield return ({method.TypeParameters[0].Name})Convert.ChangeType({CreateLiteral(item)}, typeof({method.TypeParameters[0].Name}));");
					}
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.Count)};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.Count), ReturnType.SpecialType: SpecialType.System_Int32 }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine(CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0].Name}({CreateLiteral(s)}) ? 1 : 0")));
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.Count)};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.LongCount), ReturnType.SpecialType: SpecialType.System_Int64 }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine(CreateReturnPadding("+", items.Select(s => $"{method.Parameters[0].Name}({CreateLiteral(s)}) ? 1L : 0L")));
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					foreach (var item in items.Distinct())
					{
						builder.AppendLine($"yield return {CreateLiteral(item)};");
					}
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.First())};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.ElementAt) }
				when method.Parameters.EqualsTypes(compilation.GetTypeByType(typeof(Index)))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					using (AppendMethod(builder, method))
					{
						using (builder.AppendBlock($"return {method.Parameters[0].Name}.GetOffset({CreateLiteral(items.Count)}) switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
							}

							builder.AppendLine("_ => throw new ArgumentOutOfRangeException(\"Index out of range\")");
						}
					}
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateInt32())
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					using (builder.AppendBlock($"return {method.Parameters[0].Name} switch", "};"))
					{
						for (var i = 0; i < items.Count; i++)
						{
							builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
						}

						builder.AppendLine("_ => default");
					}
				}

				return true;
			}
			case { Name: nameof(Enumerable.ElementAtOrDefault) }
				when method.Parameters.EqualsTypes(compilation.GetTypeByType(typeof(Index)))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					using (AppendMethod(builder, method))
					{
						using (builder.AppendBlock($"return {method.Parameters[0].Name}.GetOffset({CreateLiteral(items.Count)}) switch", "};"))
						{
							for (var i = 0; i < items.Count; i++)
							{
								builder.AppendLine($"{CreateLiteral(i)} => {CreateLiteral(items[i])},");
							}

							builder.AppendLine("_ => default");
						}
					}
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.First())};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.First) }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					for (var i = 0; i < items.Count; i++)
					{
						if (i != 0)
						{
							builder.AppendLine();
						}

						var item = CreateLiteral(items[i]);

						using (builder.AppendBlock($"if ({method.Parameters[0].Name}({item}))"))
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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.FirstOrDefault())};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.FirstOrDefault) }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					for (var i = 0; i < items.Count; i++)
					{
						if (i != 0)
						{
							builder.AppendLine();
						}

						var item = CreateLiteral(items[i]);

						using (builder.AppendBlock($"if ({method.Parameters[0].Name}({item}))"))
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
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByType(typeof(ValueTuple<,>), compilation.CreateInt32(), elementType)):
			{
				using (AppendMethod(builder, method))
				{
					var i = 0;

					foreach (var item in items)
					{
						builder.AppendLine($"yield return ({i++}, {CreateLiteral(item)});");
					}
				}

				return true;
			}
			default:
				return false;
		}
	}

	public bool AppendLast(IMethodSymbol methodSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		switch (methodSymbol)
		{
			case { Name: nameof(Enumerable.Last), Parameters.Length: 0 }
				when SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, elementType):
			{
				using (AppendMethod(builder, methodSymbol))
				{
					builder.AppendLine($"return {CreateLiteral(items.Last())};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.Last) }
				when methodSymbol.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, elementType):
			{
				using (AppendMethod(builder, methodSymbol))
				{
					for (var i = items.Count - 1; i >= 0; i--)
					{
						if (i != items.Count - 1)
						{
							builder.AppendLine();
						}

						var item = CreateLiteral(items[i]);

						using (builder.AppendBlock($"if ({methodSymbol.Parameters[0].Name}({item}))"))
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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {(items.Count > 0 ? CreateLiteral(items.Last()) : "default")};");
				}

				return true;
			}
			case { Name: nameof(Enumerable.LastOrDefault) }
				when method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean()))
				     && SymbolEqualityComparer.Default.Equals(method.ReturnType, elementType):
			{
				using (AppendMethod(builder, method))
				{
					for (var i = items.Count - 1; i >= 0; i--)
					{
						if (i != items.Count - 1)
						{
							builder.AppendLine();
						}

						var item = CreateLiteral(items[i]);

						using (builder.AppendBlock($"if ({method.Parameters[0].Name}({item}))"))
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
				using (AppendMethod(builder, method))
				{
					foreach (var item in items.OrderBy(s => s))
					{
						builder.AppendLine($"yield return {CreateLiteral(item)};");
					}
				}

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
				using (AppendMethod(builder, method))
				{
					foreach (var item in items.OrderByDescending(s => s))
					{
						builder.AppendLine($"yield return {CreateLiteral(item)};");
					}
				}

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
				using (AppendMethod(builder, method))
				{
					foreach (var item in items.Reverse())
					{
						builder.AppendLine($"yield return {CreateLiteral(item)};");
					}
				}

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
				     && method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, namedTypeSymbol.TypeArguments[0])):
			{
				using (AppendMethod(builder, method))
				{
					if (!IsPerformance(level, items.Count))
					{
						using (builder.AppendBlock($"for (var i = 0; i < {GetDataName(method.ContainingType)}.Length; i++)"))
						{
							builder.AppendLine($"yield return {method.Parameters[0].Name}({GetDataName(method.ContainingType)}[i]);");
						}
					}
					else
					{
						foreach (var item in items)
						{
							builder.AppendLine($"yield return {method.Parameters[0].Name}({CreateLiteral(item)});");
						}
					}
				}

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
				when method.Parameters.EqualsTypes(compilation.CreateIEnumerable(elementType)):
			{
				using (AppendMethod(builder, method))
				{
					if (!IsPerformance(level, items.Count) && compilation.HasMember<IMethodSymbol>(typeof(Enumerable), nameof(Enumerable.SequenceEqual)))
					{
						builder.AppendLine($"return {method.Parameters[0].Name}.SequenceEqual([{String.Join(", ", items.Select(CreateLiteral))}]);");
					}
					else
					{
						if (compilation.HasMember<IMethodSymbol>(typeof(Enumerable), "TryGetNonEnumeratedCount"))
						{
							using (builder.AppendBlock($"if ({method.Parameters[0].Name}.TryGetNonEnumeratedCount(out var count) && count != {items.Count})"))
							{
								builder.AppendLine($"return false;");
							}

							builder.AppendLine();
						}

						builder.AppendLine($"using var e = {method.Parameters[0].Name}.GetEnumerator();");
						builder.AppendLine();

						if (!items.Any())
						{
							builder.AppendLine($"return !e.MoveNext();");
						}

						var whitespace = new string(' ', "return ".Length - 3);

						builder.AppendLine("return " + String.Join($"\n{whitespace}&& ", items.Select(s => $"e.MoveNext() && {CreateLiteral(s)} == e.Current")) + $"\n{whitespace}&& !e.MoveNext();");
					}

				}

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
				using (AppendMethod(builder, method))
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
				using (AppendMethod(builder, method))
				{
					switch (items.Count)
					{
						case 0:
							builder.AppendLine("return default;");
							break;
						case 1:
							builder.AppendLine($"return {CreateLiteral(items[0])};");
							break;
						default:
							builder.AppendLine("throw new InvalidOperationException(\"The input sequence contains more than one element\");");
							break;
					}
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return {CreateLiteral(items.Sum())};");
				}

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
			case { Name: "TryGetNonEnumeratedCount", Parameters: [ { RefKind: RefKind.Out, Type.SpecialType: SpecialType.System_Boolean }] }
				when SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.CreateBoolean()):
			{
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"{method.Parameters[0].Name} == {CreateLiteral(items.Count)};");
					builder.AppendLine($"return true;");
				}

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
				     && method.Parameters.EqualsTypes(compilation.CreateFunc(elementType, compilation.CreateBoolean())):
			{
				using (AppendMethod(builder, method))
				{
					for (var i = 0; i < items.Count; i++)
					{
						if (i != 0)
						{
							builder.AppendLine();
						}

						var item = CreateLiteral(items[i]);

						builder.AppendLine($"if ({method.Parameters[0].Name}({item})) \tyield return {item};");
					}
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return ImmutableArray.Create({String.Join(", ", items.Select(CreateLiteral))});");
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return [{String.Join(", ", items.Select(CreateLiteral))}];");
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return ImmutableList.Create({String.Join(", ", items.Select(CreateLiteral))});");
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return [{String.Join(", ", items.Select(CreateLiteral))}];");
				}

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
				using (AppendMethod(builder, method))
				{
					builder.AppendLine($"return new HashSet<{elementType.Name}>({String.Join(", ", items.Select(CreateLiteral))});");
				}

				return true;
			}
			default:
				return false;
		}
	}
}