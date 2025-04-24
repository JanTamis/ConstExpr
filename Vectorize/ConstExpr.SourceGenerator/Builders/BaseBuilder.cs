using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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

			if (isPerformance && compilation.IsVectorSupported(elementType))
			{
				var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);

				if (vectorType != VectorTypes.None)
				{
					using (builder.AppendBlock($"if ({vectorType}.IsHardwareAccelerated)"))
					{
						vectorAction(vectorType, vector, vectorSize);
					}

					builder.AppendLine();
				}
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

		return typeSymbol.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IList_T
																						 && i.TypeArguments.Length == 1
																						 && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], elementType));
	}

	protected string GetLengthPropertyName(ITypeSymbol typeSymbol)
	{
		if ((typeSymbol is IArrayTypeSymbol arrayType && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType))
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

	public static bool IsPerformance(GenerationLevel level, int count)
	{
		return level == GenerationLevel.Performance
					 || level == GenerationLevel.Balanced && count <= Threshold;
	}

	protected string CreateReturnPadding(string check, IEnumerable<string> checks)
	{
		var padding = new string(' ', "return".Length - check.Length);

		return $"return {String.Join("\n" + padding + $"{check} ", checks)};";
	}

	protected class TreeNode
	{
		public object? Value { get; set; }
		public int Index { get; set; }
		public int ReturnValue { get; set; }
		public bool IsLeaf { get; init; }
		public TreeNode? LessThan { get; set; }
		public TreeNode? GreaterThan { get; set; }

		public NodeState State { get; set; }

		public TreeNode? Parent { get; set; }

		public enum NodeState
		{
			None,
			LessThan,
			GreaterThan
		}
	}

	protected TreeNode BuildBinarySearchTree(int low, int high, IList<object?> items, TreeNode.NodeState state, TreeNode? parentNode)
	{
		var index = (int)((uint)high + (uint)low >> 1);

		// Check if all items are equal
		if (items.All(a => a.Equals(items[0])))
		{
			return new TreeNode
			{
				IsLeaf = false,
				ReturnValue = ~index,
				State = TreeNode.NodeState.None,
				Parent = parentNode,
				Index = index,
				Value = items[index],
				LessThan = new TreeNode
				{
					IsLeaf = true,
					ReturnValue = ~0,
					State = TreeNode.NodeState.LessThan,
					Parent = parentNode,
					Index = 0,
					Value = items[0]
				},
				GreaterThan = new TreeNode
				{
					IsLeaf = true,
					ReturnValue = ~(items.Count - 1),
					State = TreeNode.NodeState.GreaterThan,
					Parent = parentNode,
					Index = items.Count - 1,
					Value = items[^1]
				}
			};
		}

		if (low > high)
		{
			return new TreeNode { IsLeaf = true, ReturnValue = ~low, State = TreeNode.NodeState.None, Parent = parentNode };
		}

		var item = new TreeNode
		{
			Value = items[index],
			Index = index,
			IsLeaf = false,
			ReturnValue = index,
			State = state,
			Parent = parentNode
		};

		item.LessThan = BuildBinarySearchTree(low, index - 1, items, TreeNode.NodeState.LessThan, item);
		item.GreaterThan = BuildBinarySearchTree(index + 1, high, items, TreeNode.NodeState.GreaterThan, item);

		if (!item.LessThan.IsLeaf && Comparer<object?>.Default.Compare(item.LessThan.Value, item.Value) >= 0)
		{
			item.LessThan = null;
		}

		if (!item.GreaterThan.IsLeaf && Comparer<object?>.Default.Compare(item.GreaterThan.Value, item.Value) <= 0)
		{
			item.GreaterThan = null;
		}

		return item;
	}

	protected void GenerateCodeFromTree(IndentedStringBuilder builder, TreeNode node, string compareFormat, bool isFirst, IMethodSymbol method, int count)
	{
		if (node.IsLeaf)
		{
			builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(node.ReturnValue)};");
			return;
		}

		//if (node.Value == node.Parent?.Value && node.LessThan?.Index == node.GreaterThan?.Index)
		//{
		//	builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~node.LessThan.Index)};");
		//}

		var checkVarName = "check";

		// Generate comparison code only once
		builder.AppendLine($"{(isFirst ? "var " : String.Empty)}{checkVarName} = " +
											 $"{String.Format(compareFormat, method.Parameters.Select<IParameterSymbol, object>(s => s.Name).Prepend(SyntaxHelpers.CreateLiteral(node.Value)).ToArray())};");
		builder.AppendLine();

		// if (node.GreaterThan is null && node.State == TreeNode.NodeState.LessThan)
		// {
		// 	using (builder.AppendBlock($"if ({checkVarName} != 0)"))
		// 	{
		// 		builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~0)};");
		// 	}
		// }
		// else if (node is { LessThan: not null, GreaterThan: not null } && node.LessThan.Index == node.GreaterThan.Index)
		// {
		// 	using (builder.AppendBlock($"if ({checkVarName} != 0)"))
		// 	{
		// 		builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~node.LessThan.Index)};");
		// 	}
		// }
		// else if (node.LessThan is null && node.GreaterThan is null)
		// {
		// 	using (builder.AppendBlock($"if ({checkVarName} != 0)"))
		// 	{
		// 		builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~node.Index)};");
		// 	}
		// }
		// else
		// {
		if (node.LessThan is not null)
		{
			// Generate branch logic
			using (builder.AppendBlock($"if ({checkVarName} < 0)"))
			{
				GenerateCodeFromTree(builder, node.LessThan!, compareFormat, false, method, count);
			}
		}

		if (node.GreaterThan is not null)
		{
			if (node.LessThan is not null)
			{
				using (builder.AppendBlock($"else if ({checkVarName} > 0)"))
				{
					GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, count);
				}
			}
			else
			{
				using (builder.AppendBlock($"if ({checkVarName} > 0)"))
				{
					GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, count);
				}
			}
		}
		else
		{
			using (builder.AppendBlock($"else if ({checkVarName} > 0)"))
			{
				switch (node.State)
				{
					case TreeNode.NodeState.LessThan:
						builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~0)};");
						break;
					case TreeNode.NodeState.GreaterThan:
						builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(~(count - 1))};");
						break;
				}
			}
		}
		// }

		builder.AppendLine();
		builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(node.Index)};");
	}
}