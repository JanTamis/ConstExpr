using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SourceGen.Utilities.Extensions;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Builders;

public abstract class BaseBuilder(ITypeSymbol elementType, Compilation compilation, GenerationLevel generationLevel, MetadataLoader loader, string dataName)
{
	public const int Threshold = 5;
	
	protected string DataName => dataName; // $"{type.Name}_{hashCode}_Data";

	private IndentedCodeWriter.Block AppendMethod(IndentedCodeWriter builder, IMethodSymbol methodSymbol)
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

		builder.WriteLine();

		if (methodSymbol.TypeParameters.Any())
		{
			var constraints = methodSymbol.TypeParameters
				.Select(tp =>
				{
					var constraintsList = new List<string>(tp.ConstraintTypes.Length + 4);

					if (tp.HasReferenceTypeConstraint)
					{
						constraintsList.Add("class");
					}

					if (tp.HasValueTypeConstraint)
					{
						constraintsList.Add("struct");
					}

					if (tp.HasNotNullConstraint)
					{
						constraintsList.Add("notnull");
					}

					if (tp.HasUnmanagedTypeConstraint)
					{
						constraintsList.Add("unmanaged");
					}

					foreach (var constraintType in tp.ConstraintTypes)
					{
						constraintsList.Add(compilation.GetMinimalString(constraintType));
					}

					if (tp.HasConstructorConstraint)
					{
						constraintsList.Add("new()");
					}

					return constraintsList.Count > 0
						? $"where {tp.Name} : {String.Join(", ", constraintsList)}"
						: null;
				})
				.Where(c => c != null);

			return builder.WriteBlock((string) $"{prepend}{compilation.GetMinimalString(methodSymbol.ReturnType)} {methodSymbol.Name}<{String.Join(", ", methodSymbol.TypeParameters.Select(compilation.GetMinimalString))}>({String.Join(", ", methodSymbol.Parameters.Select(s => $"{compilation.GetMinimalString(s.Type)} {s.Name}"))}) {String.Join("\n\t", constraints)}");
		}

		return builder.WriteBlock((string) $"{prepend}{compilation.GetMinimalString(methodSymbol.ReturnType)} {methodSymbol.Name}({String.Join(", ", methodSymbol.Parameters.Select(compilation.GetMinimalString))})");
	}

	// protected void AppendMethod<T>(IndentedCodeWriter builder, IMethodSymbol methodSymbol, ReadOnlySpan<T> items, Action<VectorTypes, string, int> vectorAction, Action<bool> action)
	// {
	// 	using (AppendMethod(builder, methodSymbol))
	// 	{
	// 		var isPerformance = IsPerformance(generationLevel, items.Length);
	//
	// 		if (isPerformance && compilation.IsVectorSupported(elementType))
	// 		{
	// 			var vectorType = compilation.GetVector(elementType, loader, items, true, out var vector, out var vectorSize);
	//
	// 			if (vectorType != VectorTypes.None)
	// 			{
	// 				using (builder.WriteBlock($"if ({vectorType}.IsHardwareAccelerated)"))
	// 				{
	// 					vectorAction(vectorType, vector, vectorSize);
	// 				}
	//
	// 				builder.WriteLine();
	// 			}
	// 		}
	//
	// 		action(isPerformance);
	// 	}
	// }

	protected void AppendMethod<T>(IndentedCodeWriter builder, IMethodSymbol methodSymbol, ReadOnlySpan<T> items, bool isRepeating, Action<VectorTypes, IList<string>, int> vectorAction, Action<bool> action)
	{
		AppendMethod<T>(builder, methodSymbol, items, isRepeating, () => { }, vectorAction, action);
	}

	protected void AppendMethod<T>(IndentedCodeWriter? builder, IMethodSymbol methodSymbol, ReadOnlySpan<T> items, bool isRepeating, Action check, Action<VectorTypes, IList<string>, int> vectorAction, Action<bool> action)
	{
		if (builder is null)
		{
			return;
		}

		using (AppendMethod(builder, methodSymbol))
		{
			var isPerformance = IsPerformance(generationLevel, items.Length);

			check();

			var type = compilation.GetBestVectorType(elementType, loader, items.Length, isRepeating);

			if (isPerformance && type != VectorTypes.None && elementType.IsVectorSupported())
			{
				var vectors = new List<string>();

				var vectorType = compilation.GetVector(elementType, loader, items, type, out var vector, out var vectorSize);
				var index = 0;

				while (vectorType == type)
				{
					vectors.Add(vector);

					index += vectorSize;
					vectorType = compilation.GetVector(elementType, loader, items.Slice(index, items.Length - index), type, out vector, out _);
				}

				if (isRepeating && index < items.Length)
				{
					vectors.Add(compilation.GetCreateVector(type, elementType, loader, true, items.Slice(index, items.Length - index)));
				}

				if (vectors.Count > 0)
				{
					using (builder.WriteBlock($"if ({type}.IsHardwareAccelerated)", padding: WhitespacePadding.After))
					{
						vectorAction(type, vectors, vectorSize);
					}
				}
			}

			action(isPerformance);
		}
	}

	protected void AppendMethod<T>(IndentedCodeWriter? builder, IMethodSymbol methodSymbol, ReadOnlySpan<T> items, Action<bool> action)
	{
		if (builder is null)
		{
			return;
		}

		using (AppendMethod(builder, methodSymbol))
		{
			action(IsPerformance(generationLevel, items.Length));
		}
	}

	protected void AppendMethod(IndentedCodeWriter? builder, IMethodSymbol methodSymbol, Action action)
	{
		if (builder is null)
		{
			return;
		}

		using (AppendMethod(builder, methodSymbol))
		{
			action();
		}
	}

	protected static void AppendProperty(IndentedCodeWriter builder, IPropertySymbol propertySymbol, string? get = null, string? set = null)
	{
		builder.WriteLine();

		if (propertySymbol.IsReadOnly)
		{
			if (IsMultiline(get))
			{
				using (builder.WriteBlock(GetHeader()))
				{
					using (builder.WriteBlock("get"))
					{
						builder.WriteLine(get);
					}
				}
			}
			else
			{
				if (get.StartsWith("return"))
				{
					get = get.Substring("return".Length);
				}

				builder.WriteLine($"{GetHeader():literal} => {get.Trim().TrimEnd(';'):literal};");
			}
		}
		else if (propertySymbol.IsWriteOnly)
		{
			using (builder.WriteBlock(GetHeader()))
			{
				using (builder.WriteBlock("set"))
				{
					builder.WriteLine(set);
				}
			}
		}
		else
		{
			using (builder.WriteBlock(GetHeader()))
			{
				if (IsMultiline(get))
				{
					using (builder.WriteBlock("get"))
					{
						builder.WriteLine(get);
					}
				}
				else
				{
					builder.WriteLine($"get => {get.TrimEnd(';'):literal};");
				}

				using (builder.WriteBlock("set"))
				{
					builder.WriteLine(set);
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
		if (typeSymbol is IArrayTypeSymbol arrayType && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType)
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, compilation.GetTypeByType(typeof(Span<>), elementType))
		    || SymbolEqualityComparer.Default.Equals(typeSymbol, compilation.GetTypeByType(typeof(ReadOnlySpan<>), elementType)))
		{
			return "Length";
		}

		return "Count";
	}

	public static bool IsPerformance(GenerationLevel level, int count)
	{
		return level == GenerationLevel.Performance
		       || level == GenerationLevel.Balanced && count <= Threshold;
	}

	protected string CreateReturnPadding(string check, IEnumerable<string> checks)
	{
		return CreatePadding(check, "return", checks);
	}

	protected string CreatePadding(string check, string prefix, IEnumerable<string> checks, bool isEnding = true, bool addWhitespace = true)
	{
		var padding = new string(' ', prefix.Length - check.Length - (addWhitespace ? 0 : 1));

		if (addWhitespace)
		{
			prefix += " ";
		}

		if (isEnding)
		{
			return $"{prefix}{String.Join("\n" + padding + $"{check} ", checks)};";
		}

		return $"{prefix}{String.Join("\n" + padding + $"{check} ", checks)}";
	}

	protected class TreeNode<T>
	{
		public T Value { get; init; }
		public int Index { get; init; }
		public int ReturnValue { get; init; }
		public bool IsLeaf { get; init; }
		public TreeNode<T>? LessThan { get; set; }
		public TreeNode<T>? GreaterThan { get; set; }

		public NodeState State { get; init; }

		public TreeNode<T>? Parent { get; init; }

		public enum NodeState
		{
			None,
			LessThan,
			GreaterThan
		}
	}

	protected TreeNode<T> BuildBinarySearchTree<T>(int low, int high, int index, IList<KeyValuePair<T, int>> items, TreeNode<T>.NodeState state, TreeNode<T>? parentNode, IList<T> values)
	{
		if (low > high && parentNode is not null)
		{
			var value = parentNode.Value;
			var i = parentNode.Index;

			switch (state)
			{
				case TreeNode<T>.NodeState.LessThan:
				{
					for (; i >= 0; i--)
					{
						if (Comparer<T>.Default.Compare(values[i], value) < 0)
						{
							break;
						}
					}
					break;
				}
				case TreeNode<T>.NodeState.GreaterThan:
				{
					for (; i < values.Count; i++)
					{
						if (Comparer<T>.Default.Compare(values[i], value) > 0)
						{
							break;
						}
					}
					break;
				}
			}

			return new TreeNode<T> { IsLeaf = true, ReturnValue = ~Math.Max(0, Math.Min(i, values.Count - 1)), State = state, Parent = parentNode };
		}

		var item = new TreeNode<T>
		{
			Index = items[index].Value,
			Value = items[index].Key,
			IsLeaf = false,
			ReturnValue = items[index].Value,
			State = state,
			Parent = parentNode
		};

		item.LessThan = BuildBinarySearchTree(low, index - 1, (int) ((uint) (index - 1) + (uint) low >> 1), items, TreeNode<T>.NodeState.LessThan, item, values);
		item.GreaterThan = BuildBinarySearchTree(index + 1, high, (int) ((uint) high + (uint) (index + 1) >> 1), items, TreeNode<T>.NodeState.GreaterThan, item, values);

		if (elementType.IsInterger() && item.LessThan.IsLeaf && CompareLessThan(item, item.Value.Subtract((T) Convert.ChangeType(1, typeof(T)))))
		{
			item.LessThan = null;
		}

		if (elementType.IsInterger() && item.GreaterThan.IsLeaf && CompareGreaterThan(item, item.Value.Add((T) Convert.ChangeType(1, typeof(T)))))
		{
			item.GreaterThan = null;
		}

		return item;

		bool CompareLessThan(TreeNode<T> node, T value)
		{
			return node is { State: TreeNode<T>.NodeState.GreaterThan, Parent: not null } && EqualityComparer<T>.Default.Equals(node.Parent.Value, value) || node.Parent is not null && CompareLessThan(node.Parent, value);
		}

		bool CompareGreaterThan(TreeNode<T> node, T value)
		{
			if (EqualityComparer<T>.Default.Equals(node.Value, value))
			{
				return true;
			}

			return node.Parent is not null && CompareGreaterThan(node.Parent, value);
		}
	}

	protected void GenerateCodeFromTree<T>(IndentedCodeWriter builder, TreeNode<T> node, string compareFormat, bool isFirst, IMethodSymbol method, IList<KeyValuePair<T, int>> items)
	{
		if (node.IsLeaf)
		{
			builder.WriteLine($"return {node.ReturnValue};");
			return;
		}

		if (node.LessThan is null && node.GreaterThan is null)
		{
			if (node.State == TreeNode<T>.NodeState.LessThan && !EqualityComparer<T>.Default.Equals(node.Value, items[0].Key)
			    || node.State == TreeNode<T>.NodeState.GreaterThan && !EqualityComparer<T>.Default.Equals(node.Value, items[^1].Key))
			{
				builder.WriteLine($"return {node.ReturnValue};");
				return;
			}
		}

		var checkVarName = "check";

		if (!elementType.HasComparison())
		{
			// Generate comparison code only once
			builder.WriteLine($"{(isFirst ? "var " : String.Empty)}{checkVarName} = " +
			                   $"{String.Format(compareFormat, method.Parameters.Select<IParameterSymbol, object>(s => s.Name).Prepend(node.Value).ToArray())};");
			builder.WriteLine();
		}


		if (node.LessThan is not null)
		{
			if (elementType.HasComparison())
			{
				// Generate branch logic
				using (builder.WriteBlock($"if ({method.Parameters[0]} < {node.Value})"))
				{
					GenerateCodeFromTree(builder, node.LessThan, compareFormat, false, method, items);
				}
			}
			else
			{
				// Generate branch logic
				using (builder.WriteBlock($"if ({checkVarName} < 0)"))
				{
					GenerateCodeFromTree(builder, node.LessThan, compareFormat, false, method, items);
				}
			}
		}

		if (node.GreaterThan is not null)
		{
			if (node.LessThan is not null)
			{
				if (elementType.HasComparison())
				{
					using (builder.WriteBlock($"else if ({method.Parameters[0]} > {node.Value})"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
				else
				{
					using (builder.WriteBlock($"else if ({checkVarName} > 0)"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
			}
			else
			{
				if (elementType.HasComparison())
				{
					using (builder.WriteBlock($"if ({method.Parameters[0]} > {node.Value})"))
					{
						GenerateCodeFromTree(builder, node.GreaterThan, compareFormat, false, method, items);
					}
				}
				else
				{
					using (builder.WriteBlock("else"))
					{
						builder.WriteLine($"if ({checkVarName} > 0)");
					}

					builder.WriteLine();
				}
			}
		}

		builder.WriteLine();
		builder.WriteLine($"return {node.ReturnValue};");
	}
}