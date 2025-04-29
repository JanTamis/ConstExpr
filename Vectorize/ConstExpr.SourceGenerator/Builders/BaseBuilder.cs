using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
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

	protected TreeNode BuildBinarySearchTree(int low, int high, int index, IList<KeyValuePair<object?, int>> items, TreeNode.NodeState state, TreeNode? parentNode, IList<object?> values)
	{
		if (low > high && parentNode is not null)
		{
			var value = parentNode.Value;
			var i = parentNode.Index;

			switch (state)
			{
				case TreeNode.NodeState.LessThan:
				{
					for (; i >= 0; i--)
					{
						if (Comparer<object?>.Default.Compare(values[i], value) < 0)
						{
							break;
						}
					}
					break;
				}
				case TreeNode.NodeState.GreaterThan:
				{
					for (; i < values.Count; i++)
					{
						if (Comparer<object?>.Default.Compare(values[i], value) > 0)
						{
							break;
						}
					}
					break;
				}
			}

			return new TreeNode { IsLeaf = true, ReturnValue = ~Math.Max(0, Math.Min(i, values.Count - 1)), State = state, Parent = parentNode };
		}

		var item = new TreeNode
		{
			Index = items[index].Value,
			Value = items[index].Key,
			IsLeaf = false,
			ReturnValue = items[index].Value,
			State = state,
			Parent = parentNode
		};

		item.LessThan = BuildBinarySearchTree(low, index - 1, (int) ((uint) (index - 1) + (uint) low >> 1), items, TreeNode.NodeState.LessThan, item, values);
		item.GreaterThan = BuildBinarySearchTree(index + 1, high, (int) ((uint) high + (uint) (index + 1) >> 1), items, TreeNode.NodeState.GreaterThan, item, values);

		if (compilation.IsInterger(elementType) && item.LessThan.IsLeaf && CompareLessThan(item, ConstExprOperationVisitor.Subtract(item.Value, 1)))
		{
			item.LessThan = null;
		}
		
		if (compilation.IsInterger(elementType) && item.GreaterThan.IsLeaf && CompareGreaterThan(item, ConstExprOperationVisitor.Add(item.Value, 1)))
		{
			item.GreaterThan = null;
		}
		
		return item;

		bool CompareLessThan(TreeNode node, object? value)
		{
			return node is { State: TreeNode.NodeState.GreaterThan, Parent: not null } && EqualityComparer<object?>.Default.Equals(node.Parent.Value, value) || node.Parent is not null && CompareLessThan(node.Parent, value);
		}

		bool CompareGreaterThan(TreeNode node, object? value)
		{
			if (EqualityComparer<object?>.Default.Equals(node.Value, value))
			{
				return true;
			}

			return node.Parent is not null && CompareGreaterThan(node.Parent, value);
		}
	}

	protected void GenerateCodeFromTree(IndentedStringBuilder builder, TreeNode node, string compareFormat, bool isFirst, IMethodSymbol method, IList<KeyValuePair<object?, int>> items)
	{
		if (node.IsLeaf)
		{
			builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(node.ReturnValue)};");
			return;
		}

		if (node.LessThan is null && node.GreaterThan is null)
		{
			if (node.State == TreeNode.NodeState.LessThan && node.Value != items[0].Key
			    || node.State == TreeNode.NodeState.GreaterThan && node.Value != items[^1].Key)
			{
				builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(node.ReturnValue)};");
				return;
			}
		}

		var checkVarName = "check";

		if (!compilation.HasComparison(elementType))
		{
			// Generate comparison code only once
			builder.AppendLine($"{(isFirst ? "var " : String.Empty)}{checkVarName} = " +
			                   $"{String.Format(compareFormat, method.Parameters.Select<IParameterSymbol, object>(s => s.Name).Prepend(SyntaxHelpers.CreateLiteral(node.Value)).ToArray())};");
			builder.AppendLine();
		}
		

		if (node.LessThan is not null)
		{
			if (compilation.HasComparison(elementType))
			{
				// Generate branch logic
				using (builder.AppendBlock($"if ({method.Parameters[0].Name} < {node.Value})"))
				{
					GenerateCodeFromTree(builder, node.LessThan, compareFormat, false, method, items);
				}
			}
			else
			{
				// Generate branch logic
				using (builder.AppendBlock($"if ({checkVarName} < 0)"))
				{
					GenerateCodeFromTree(builder, node.LessThan, compareFormat, false, method, items);
				}
			}
		}

		if (node.GreaterThan is not null)
		{
			if (node.LessThan is not null)
			{
				if (compilation.HasComparison(elementType))
				{
					using (builder.AppendBlock($"else if ({method.Parameters[0].Name} > {node.Value})"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
				else
				{
					using (builder.AppendBlock($"else if ({checkVarName} > 0)"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
			}
			else
			{
				if (compilation.HasComparison(elementType))
				{
					using (builder.AppendBlock($"if ({method.Parameters[0].Name} > {node.Value})"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
				else
				{
					using (builder.AppendBlock($"else"))
					{
						builder.AppendLine($"if ({checkVarName} > 0)");
					}

					builder.AppendLine();
				}
			}
		}

		builder.AppendLine();
		builder.AppendLine($"return {SyntaxHelpers.CreateLiteral(node.ReturnValue)};");
	}
}