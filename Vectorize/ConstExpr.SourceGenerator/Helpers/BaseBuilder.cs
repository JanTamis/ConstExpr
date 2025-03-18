using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

public abstract class BaseBuilder(ITypeSymbol elementType, Compilation compilation)
{
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
			return builder.AppendBlock($"{prepend}{SyntaxHelpers.GetMinimalString(compilation, methodSymbol.ReturnType)} {methodSymbol.Name}<{String.Join(", ", methodSymbol.TypeParameters.Select(s => SyntaxHelpers.GetMinimalString(compilation, s)))}>({String.Join(", ", methodSymbol.Parameters.Select(s => SyntaxHelpers.GetMinimalString(compilation, s)))})");
		}

		return builder.AppendBlock($"{prepend}{SyntaxHelpers.GetMinimalString(compilation, methodSymbol.ReturnType)} {methodSymbol.Name}({String.Join(", ", methodSymbol.Parameters.Select(s => SyntaxHelpers.GetMinimalString(compilation, s)))})");
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

		if (SymbolEqualityComparer.Default.Equals(typeSymbol, SyntaxHelpers.GetTypeByType(compilation, typeof(Span<>), elementType)))
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
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, SyntaxHelpers.GetTypeByType(compilation, typeof(Span<>), elementType))
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, SyntaxHelpers.GetTypeByType(compilation, typeof(ReadOnlySpan<>), elementType)))
		{
			return "Length";
		}

		return "Count";
	}
}