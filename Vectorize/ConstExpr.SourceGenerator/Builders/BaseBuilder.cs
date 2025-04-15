using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Builders;

public abstract class BaseBuilder(ITypeSymbol elementType, Compilation compilation, GenerationLevel generationLevel, MetadataLoader loader, int hashCode)
{
	public const int Threshold = 5;
	
	protected IDisposable AppendMethod(IndentedStringBuilder builder, IMethodSymbol methodSymbol)
	{
		var prepend = "public ";

		if (methodSymbol.IsStatic)
		{
			prepend += "static ";
		}

		if (methodSymbol.IsAsync)
		{
			prepend += "async ";
		}

		builder.AppendLine();

		if (methodSymbol.TypeParameters.Any())
		{
			var constraints = methodSymbol.TypeParameters
				.Select(tp =>
				{
					var constraintsList = new List<string>();

					if (tp.HasReferenceTypeConstraint)
						constraintsList.Add("class");
					if (tp.HasValueTypeConstraint)
						constraintsList.Add("struct");
					if (tp.HasNotNullConstraint)
						constraintsList.Add("notnull");
					if (tp.HasUnmanagedTypeConstraint)
						constraintsList.Add("unmanaged");

					foreach (var constraintType in tp.ConstraintTypes)
						constraintsList.Add(compilation.GetMinimalString(constraintType));

					if (tp.HasConstructorConstraint)
						constraintsList.Add("new()");

					return constraintsList.Count > 0
						? $"where {tp.Name} : {String.Join(", ", constraintsList)}"
						: null;
				})
				.Where(c => c != null);
			
			return builder.AppendBlock($"{prepend}{compilation.GetMinimalString(methodSymbol.ReturnType)} {methodSymbol.Name}<{String.Join(", ", methodSymbol.TypeParameters.Select(compilation.GetMinimalString))}>({String.Join(", ", methodSymbol.Parameters.Select(compilation.GetMinimalString))}) {String.Join("\n\t", constraints)}");
		}

		return builder.AppendBlock($"{prepend}{compilation.GetMinimalString(methodSymbol.ReturnType)} {methodSymbol.Name}({String.Join(", ", methodSymbol.Parameters.Select(compilation.GetMinimalString))})");
	}

	protected void AppendMethod(IndentedStringBuilder builder, IMethodSymbol methodSymbol, IList<object?> items, Action<VectorTypes, string, int> vectorAction, Action<bool> action)
	{
		using (AppendMethod(builder, methodSymbol))
		{
			var isPerformance = IsPerformance(generationLevel, items.Count);
			
			if (isPerformance)
			{
				var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

				using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
				{
					vectorAction(vectorType, vector, vectorSize);
				}
				
				builder.AppendLine();
			}
			
			action(isPerformance);
		}
	}

	protected void AppendMethod(IndentedStringBuilder builder, IMethodSymbol methodSymbol, IEnumerable<object?> items, Action<bool> action)
	{
		using (AppendMethod(builder, methodSymbol))
		{
			action(IsPerformance(generationLevel, items.Count()));
		}
	}

	protected static void AppendProperty(IndentedStringBuilder builder, IPropertySymbol propertySymbol, string? get = null, string? set = null)
	{
		builder.AppendLine();

		if (propertySymbol.IsReadOnly)
		{
			if (IsMultiline(get))
			{
				using (builder.AppendBlock(GetHeader()))
				{
					using (builder.AppendBlock("get"))
					{
						builder.AppendLine(get);
					}
				}
			}
			else
			{
				if (get.StartsWith("return"))
				{
					get = get.Substring("return".Length);
				}

				builder.AppendLine($"{GetHeader()} => {get.Trim().TrimEnd(';')};");
			}
		}
		else if (propertySymbol.IsWriteOnly)
		{
			using (builder.AppendBlock(GetHeader()))
			{
				using (builder.AppendBlock("set"))
				{
					builder.AppendLine(set);
				}
			}
		}
		else
		{
			using (builder.AppendBlock(GetHeader()))
			{
				if (IsMultiline(get))
				{
					using (builder.AppendBlock("get"))
					{
						builder.AppendLine(get);
					}
				}
				else
				{
					builder.AppendLine($"get => {get.TrimEnd(';')};");
				}

				using (builder.AppendBlock("set"))
				{
					builder.AppendLine(set);
				}
			}
		}

		bool IsMultiline(string? text)
		{
			return text is not null && text.Contains('\n');
		}

		string GetHeader()
		{
			var prepend = "public ";

			if (propertySymbol.IsStatic)
			{
				prepend += "static ";
			}

			if (propertySymbol.IsIndexer)
			{
				return $"{prepend}{propertySymbol.Type.ToDisplayString()} this[{String.Join(", ", propertySymbol.Parameters.Select(p => p.ToString()))}]";
			}

			return $"{prepend}{propertySymbol.Type.ToDisplayString()} {propertySymbol.Name}";
		}
	}

	protected bool IsIndexableType(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is IArrayTypeSymbol arrayType)
		{
			return SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType);
		}

		if (SymbolEqualityComparer.Default.Equals(typeSymbol, compilation.GetTypeByType(typeof(Span<>), elementType)))
		{
			return true;
		}

		return typeSymbol.AllInterfaces.Any(i =>	i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IList_T 
			                                        && i.TypeArguments.Length == 1 
			                                        && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], elementType));
	}

	protected string GetLengthPropertyName(ITypeSymbol typeSymbol)
	{
		if ((typeSymbol is IArrayTypeSymbol arrayType && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType) )
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, compilation.GetTypeByType(typeof(Span<>), elementType))
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, compilation.GetTypeByType(typeof(ReadOnlySpan<>), elementType)))
		{
			return "Length";
		}

		return "Count";
	}

	protected string GetDataName(ISymbol type)
	{
		return $"{type.Name}_{hashCode}_Data";
	}

	protected bool IsPerformance(GenerationLevel level, int count)
	{
		return level == GenerationLevel.Performance 
		       || level == GenerationLevel.Balanced && count <= Threshold;
	}

	protected string CreateReturnPadding(string check, IEnumerable<string> checks)
	{
		var padding = new string(' ', "return".Length - check.Length);

		return $"return {String.Join("\n" + padding + $"{check} ", checks)};";
	}
}