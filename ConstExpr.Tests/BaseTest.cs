using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Rewriters;

namespace ConstExpr.Tests;

public abstract class BaseTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.Strict)
{
	// the generated method bodies to be expected
	public abstract IEnumerable<KeyValuePair<string?, object[]>> Result { get; }

	public abstract string TestMethod { get; }

	protected object Unknown = new object();

	[Test]
	public void RunTest()
	{
		var compilation = CreateCompilation(BuildSource());

		var exceptions = compilation
			.GetDiagnostics()
			.Where(w => w.Severity == DiagnosticSeverity.Error)
			.Select(s => new InvalidOperationException(s.ToString()))
			.ToList();

		if (exceptions.Count > 0)
		{
			switch (exceptions.Count)
			{
				case 1:
					throw exceptions.First();
				case > 1:
					throw new AggregateException(exceptions);
			}
		}

		var method = compilation.SyntaxTrees
			.SelectMany(s => s.GetRoot()
				.DescendantNodes()
				.OfType<LocalFunctionStatementSyntax>())
			.First();

		var parameterNames = method.ParameterList.Parameters
			.Select(s => s.Identifier.Text)
			.ToList();

		var parameters = new Dictionary<string, VariableItem>(parameterNames.Count);

		foreach (var param in parameterNames)
		{
			parameters[param] = new VariableItem(
				type: compilation.GetSemanticModel(method.SyntaxTree).GetTypeInfo(
					method.ParameterList.Parameters
						.First(p => p.Identifier.Text == param)
						.Type!).Type!,
				hasValue: false,
				value: null);
		}

		var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);
		var loader = MetadataLoader.GetLoader(compilation);
		var attribute = new ConstExprAttribute { FloatingPointMode = evaluationMode };

		var roslynCache = new RoslynApiCache();

		var rewriter = new ConstExprPartialRewriter(semanticModel, loader, (_, exception) => { }, parameters, new Dictionary<SyntaxNode, bool>(), new HashSet<string>(), attribute, CancellationToken.None, []);
		var pruneRewriter = new PruneVariableRewriter(semanticModel, loader, parameters, roslynCache);

		foreach (var result in Result)
		{
			var notParameters = parameters.Keys
				.Except(parameterNames)
				.ToList();

			foreach (var notParam in notParameters)
			{
				parameters.Remove(notParam);
			}

			if (result.Value.Length != parameterNames.Count)
			{
				throw new InvalidOperationException("Parameter count mismatch.");
			}

			for (var i = 0; i < result.Value.Length; i++)
			{
				var value = result.Value[i];

				if (value == Unknown)
				{
					parameters[parameterNames[i]].HasValue = false;
					parameters[parameterNames[i]].Value = null;
				}
				else
				{
					parameters[parameterNames[i]].HasValue = true;
					parameters[parameterNames[i]].Value = value;
				}

				parameters[parameterNames[i]].IsAccessed = false;
				parameters[parameterNames[i]].IsAltered = false;
				parameters[parameterNames[i]].IsInitialized = true;
			}

			var newBody = rewriter.VisitBlock(method.Body!) as BlockSyntax;
			newBody = pruneRewriter.Visit(newBody) as BlockSyntax;

			if (result.Key is null)
			{
				if (method.Body.GetDeterministicHash() != newBody.GetDeterministicHash())
				{
					throw new InvalidOperationException($"""
					Generated method body does not match expected body.

					Expected body:
					{FormattingHelper.Render(method.Body) ?? "(null)"}

					Generated body:
					{FormattingHelper.Render(newBody) ?? "(null)"}
					""");
				}
			}
			else
			{
				var expectedBody = ParseBlock(result.Key); // SyntaxFactory.Block(SyntaxFactory.ParseStatement(result.Key!));

        if (newBody == null || expectedBody == null || FormattingHelper.Render(newBody) != FormattingHelper.Render(expectedBody))
				{
					throw new InvalidOperationException($"""
					Generated method body does not match expected body.

					Expected body:
					{FormattingHelper.Render(expectedBody) ?? "(null)"}

					Generated body:
					{FormattingHelper.Render(newBody) ?? "(null)"}
					""");
				}
			}
		}
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		return CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText(source)],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));
	}

	// Builds the final source passed to Roslyn
	protected string BuildSource()
	{
		return $"""
			using System;

			{TestMethod}
			""";
	}

	protected static KeyValuePair<string?, object[]> Create(string? key, params object[] values)
	{
		return KeyValuePair.Create(key, values);
	}

	protected static BlockSyntax ParseBlock(string code)
	{
		var tree = SyntaxFactory.ParseSyntaxTree($$"""
			void TestMethod()
			{
				{{code}}
			}
			""");

		return tree.GetRoot()
				.DescendantNodes()
				.OfType<LocalFunctionStatementSyntax>()
				.Select(s => s.Body!)
				.First();
	}
}