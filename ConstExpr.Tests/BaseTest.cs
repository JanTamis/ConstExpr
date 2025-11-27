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
				.OfType<MethodDeclarationSyntax>())
			.First(f => f.Modifiers.Count == 0);
		
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
		
		var rewriter = new ConstExprPartialRewriter(semanticModel, loader, (_, exception) => throw exception, parameters, new Dictionary<SyntaxNode, bool>(), new HashSet<string>(), attribute, CancellationToken.None, []);

		foreach (var result in Result)
		{
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
			}

			var newBody = rewriter.VisitBlock(method.Body!) as BlockSyntax;
			var expectedBody = SyntaxFactory.ParseStatement(result.Key!) as BlockSyntax;
			
			if (newBody == null || expectedBody == null || newBody.GetDeterministicHash() != expectedBody.GetDeterministicHash())
			{
				throw new InvalidOperationException($"""
					Generated method body does not match expected body.

					Expected body:
					{expectedBody?.ToFullString() ?? "(null)"}

					Generated body:
					{newBody?.ToFullString() ?? "(null)"}
					""");
			}
		}

		// var generated = RunGenerator(out var compilation);
		// var result = GetResult(generated);
		//
		// var diagnostics = generated.Diagnostics;
		// var exceptions = diagnostics.Select(s => new InvalidOperationException(s.ToString()));

		// 		switch (diagnostics.Length)
		// 		{
		// 			case 1:
		// 				throw exceptions.First();
		// 			case > 1:
		// 				throw new AggregateException(exceptions);
		// 			default:
		// 				// Parse expected bodies
		// 				var expectedBodies = Result
		// 					.Select(ParseMethodBody)
		// 					.ToList();
		//
		// 				var actualMethods = result
		// 					.Select(m => m.WithAttributeLists([ ]).NormalizeWhitespace())
		// 					.ToList();
		//
		// 				// Compare count first
		// 				if (expectedBodies.Count != actualMethods.Count)
		// 				{
		// 					var generatedMethodsList = System.String.Join("\n", actualMethods.Select((m, i) => $"  [{i}] {m.Identifier}"));
		//
		// 					if (actualMethods.Count == 0)
		// 					{
		// 						throw new InvalidOperationException($"""
		// 							Generated method count does not match expected count.
		// 							Expected: {expectedBodies.Count} {(expectedBodies.Count == 1 ? "method" : "methods")}
		// 							Generated: 0 methods
		// 							""");
		// 					}
		// 					
		// 					throw new InvalidOperationException($"""
		// 						Generated method count does not match expected count.
		// 						Expected: {expectedBodies.Count} {(expectedBodies.Count == 1 ? "method" : "methods")}
		// 						Generated: {actualMethods.Count} {(actualMethods.Count == 1 ? "method" : "methods")}
		//
		// 						Generated methods:
		// 						{generatedMethodsList}
		// 						""");
		// 				}
		//
		// 				// Match methods by body content since order may vary
		// 				var unmatchedExpected = new List<BlockSyntax>();
		//
		// 				foreach (var expectedBody in expectedBodies)
		// 				{
		// 					var matching = actualMethods.FirstOrDefault(actual =>
		// 						expectedBody.GetDeterministicHash() == actual.Body.GetDeterministicHash());
		//
		// 					if (matching == null)
		// 					{
		// 						unmatchedExpected.Add(expectedBody);
		// 					}
		// 					else
		// 					{
		// 						// Remove from list to handle duplicates correctly
		// 						actualMethods.Remove(matching);
		// 					}
		// 				}
		//
		// 				// Report any mismatches
		// 				if (unmatchedExpected.Count > 0 || actualMethods.Count > 0)
		// 				{
		// 					// New logic: If all unmatched expected bodies are non-constant (contain more than a single return statement) AND we have only constant actual methods left, allow pass.
		// 					var onlyReturnConstantsLeft = actualMethods.All(m => m.Body?.Statements.Count == 1 && m.Body.Statements[0] is ReturnStatementSyntax);
		// 					var unmatchedAreGenericBodies = unmatchedExpected.All(b => b.Statements.Count > 1);
		//
		// 					if (onlyReturnConstantsLeft && unmatchedAreGenericBodies)
		// 					{
		// 						// treat as success (generic body optimized away into constants)
		// 						break;
		// 					}
		//
		// 					var errorMessage = new StringBuilder();
		// 					errorMessage.AppendLine("Generated method bodies do not match expected bodies.");
		// 					errorMessage.AppendLine();
		//
		// 					if (unmatchedExpected.Count > 0)
		// 					{
		// 						errorMessage.AppendLine($"Expected bodies not found ({unmatchedExpected.Count}):");
		//
		// 						foreach (var body in unmatchedExpected)
		// 						{
		// 							errorMessage.AppendLine($"""
		// 								  Body:
		// 								{body.ToFullString()}
		//
		// 								""");
		// 						}
		//
		// 						// errorMessage.AppendLine();
		// 					}
		//
		// 					if (actualMethods.Count > 0)
		// 					{
		// 						errorMessage.AppendLine($"Unexpected generated methods ({actualMethods.Count}):");
		//
		// 						foreach (var method in actualMethods)
		// 						{
		// 							errorMessage.AppendLine($"""
		// 									Method signature: {method.ReturnType} {method.Identifier}{method.ParameterList}
		// 									Body:
		// 								{method.Body?.ToFullString() ?? "(no body)"}
		//
		// 								""");
		// 						}
		// 					}
		//
		// 					throw new InvalidOperationException(errorMessage.ToString());
		// 				}
		//
		// 				break;
		// 		}
	}

	protected GeneratorDriverRunResult RunGenerator(out Compilation outputCompilation)
	{
		var compilation = CreateCompilation(BuildSource());
		var generator = new ConstExprSourceGeneratorHoist(new());
		var optionsProvider = new TestAnalyzerConfigOptionsProvider();

		var driver = CSharpGeneratorDriver
			.Create(generator)
			.WithUpdatedAnalyzerConfigOptions(optionsProvider)
			.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out var diagnostics);

		return driver.GetRunResult();
	}

	protected IEnumerable<MethodDeclarationSyntax> GetResult(GeneratorDriverRunResult result)
	{
		var generatedTrees = result.GeneratedTrees;

		if (generatedTrees.Length == 0)
		{
			return [ ];
		}

		var last = generatedTrees.Last();

		return last.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>();
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !System.String.IsNullOrEmpty(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location))
			.Cast<MetadataReference>()
			.ToList();

		references.Add(MetadataReference.CreateFromFile(typeof(ConstExprAttribute).Assembly.Location));

		return CSharpCompilation.Create(
			"TestAssembly",
			[ CSharpSyntaxTree.ParseText(source) ],
			[ MetadataReference.CreateFromFile(typeof(object).Assembly.Location) ],
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
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
	public override AnalyzerConfigOptions GlobalOptions { get; } = new TestGlobalAnalyzerConfigOptions();

	public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => DictionaryAnalyzerConfigOptions.Empty;
	public override AnalyzerConfigOptions GetOptions(AdditionalText text) => DictionaryAnalyzerConfigOptions.Empty;
}

internal sealed class TestGlobalAnalyzerConfigOptions : AnalyzerConfigOptions
{
	private readonly Dictionary<string, string> _data = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "build_property.UseConstExpr", "true" }
	};
	public override bool TryGetValue(string key, out string value) => _data.TryGetValue(key, out value!);
}

internal sealed class DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> options) : AnalyzerConfigOptions
{
	internal static readonly ImmutableDictionary<string, string> EmptyDictionary = ImmutableDictionary.Create<string, string>(KeyComparer);

	public static DictionaryAnalyzerConfigOptions Empty { get; } = new DictionaryAnalyzerConfigOptions(EmptyDictionary);

	// Note: Do not rename. Older versions of analyzers access this field via reflection.
	// https://github.com/dotnet/roslyn/blob/8e3d62a30b833631baaa4e84c5892298f16a8c9e/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Options/EditorConfig/EditorConfigStorageLocationExtensions.cs#L21
	internal readonly ImmutableDictionary<string, string> Options = options;

	public override bool TryGetValue(string key, out string? value)
		=> Options.TryGetValue(key, out value);

	public override IEnumerable<string> Keys
		=> Options.Keys;
}