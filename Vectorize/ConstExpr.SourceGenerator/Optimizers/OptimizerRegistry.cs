using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers;

internal static class OptimizerRegistry
{
	private static readonly FrozenDictionary<string, Func<SyntaxNode?, BaseStringFunctionOptimizer>[]> _stringFactories = CreateStringFactories();

	public static FrozenDictionary<BinaryOperatorKind, BaseBinaryOptimizer> BinaryOptimizers { get; } = CreateBinaryOptimizers();

	public static BaseMathFunctionOptimizer[] MathOptimizers { get; } = CreateInstances<BaseMathFunctionOptimizer>();

	public static BaseLinqFunctionOptimizer[] LinqOptimizers { get; } = CreateInstances<BaseLinqFunctionOptimizer>();

	public static BaseSimdFunctionOptimizer[] SimdOptimizers { get; } = CreateInstances<BaseSimdFunctionOptimizer>();

	public static BaseRegexFunctionOptimizer[] RegexOptimizers { get; } = CreateInstances<BaseRegexFunctionOptimizer>();

	public static IEnumerable<BaseStringFunctionOptimizer> CreateStringOptimizers(string methodName, SyntaxNode? instance)
	{
		if (!_stringFactories.TryGetValue(methodName, out var factories))
		{
			yield break;
		}

		for (var i = 0; i < factories.Length; i++)
		{
			yield return factories[i](instance);
		}
	}

	private static FrozenDictionary<BinaryOperatorKind, BaseBinaryOptimizer> CreateBinaryOptimizers()
	{
		return CreateInstances<BaseBinaryOptimizer>()
			.ToFrozenDictionary(optimizer => optimizer.Kind);
	}

	private static T[] CreateInstances<T>() where T : class
	{
		return typeof(T).Assembly
			.GetTypes()
			.Where(static t => !t.IsAbstract && typeof(T).IsAssignableFrom(t))
			.Select(static t => Activator.CreateInstance(t) as T)
			.OfType<T>()
			.ToArray();
	}

	private static FrozenDictionary<string, Func<SyntaxNode?, BaseStringFunctionOptimizer>[]> CreateStringFactories()
	{
		var factories = new Dictionary<string, List<Func<SyntaxNode?, BaseStringFunctionOptimizer>>>(StringComparer.Ordinal);
		var optimizerTypes = typeof(BaseStringFunctionOptimizer).Assembly
			.GetTypes()
			.Where(static t => !t.IsAbstract && typeof(BaseStringFunctionOptimizer).IsAssignableFrom(t));

		foreach (var optimizerType in optimizerTypes)
		{
			var ctor = optimizerType.GetConstructor([ typeof(SyntaxNode) ]);

			if (ctor is null)
			{
				continue;
			}

			var tempOptimizer = ctor.Invoke([ null ]) as BaseStringFunctionOptimizer;

			if (tempOptimizer is null)
			{
				continue;
			}

			foreach (var optimizerName in tempOptimizer.Names)
			{
				if (!factories.TryGetValue(optimizerName, out var entries))
				{
					entries = [ ];
					factories[optimizerName] = entries;
				}

				entries.Add(instance => (BaseStringFunctionOptimizer)ctor.Invoke([ instance ])!);
			}
		}

		return factories.ToFrozenDictionary(
			static pair => pair.Key,
			static pair => pair.Value.ToArray(),
			StringComparer.Ordinal);
	}
}