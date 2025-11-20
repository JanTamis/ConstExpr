using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Text;

namespace ConstExpr.Tests;

public abstract class BaseTest
{
	// the generated method bodies to be expected
	public abstract IEnumerable<string> Result { get; }
	
	// the source code to be tested
	public abstract string Invocations { get; }

	public abstract string TestMethod { get; }

	[Fact]
	public virtual void RunTest()
	{
		var generated = RunGenerator(out var compilation);
		var result = GetResult(generated);

		var diagnostics = generated.Diagnostics;
		var exceptions = diagnostics.Select(s => new InvalidOperationException(s.ToString()));

		switch (diagnostics.Length)
		{
			case 1:
				throw exceptions.First();
			case > 1:
				throw new AggregateException(exceptions);
			default:
				// Parse expected bodies
				var expectedBodies = Result
					.Select(ParseMethodBody)
					.ToList();
				
				var actualMethods = result
					.Select(m => m.WithAttributeLists([]).NormalizeWhitespace())
					.ToList();
				
				// Compare count first
				if (expectedBodies.Count != actualMethods.Count)
				{
					var generatedMethodsList = String.Join("\n", actualMethods.Select((m, i) => $"  [{i}] {m.Identifier}"));
					
					throw new InvalidOperationException($"""
						Generated method count does not match expected count.
						Expected: {expectedBodies.Count} method(s)
						Generated: {actualMethods.Count} method(s)

						Generated methods:
						{generatedMethodsList}
						""");
				}
				
				// Match methods by body content since order may vary
				var unmatchedExpected = new List<BlockSyntax>();
				
				foreach (var expectedBody in expectedBodies)
				{
					var matching = actualMethods.FirstOrDefault(actual =>
						expectedBody.IsEquivalentTo(actual.Body));
					
					if (matching == null)
					{
						unmatchedExpected.Add(expectedBody);
					}
					else
					{
						// Remove from list to handle duplicates correctly
						actualMethods.Remove(matching);
					}
				}
				
				// Report any mismatches
				if (unmatchedExpected.Count > 0 || actualMethods.Count > 0)
				{
					var errorMessage = new StringBuilder();
					errorMessage.AppendLine("Generated method bodies do not match expected bodies.");
					errorMessage.AppendLine();
					
					if (unmatchedExpected.Count > 0)
					{
						errorMessage.AppendLine($"Expected bodies not found ({unmatchedExpected.Count}):");

						foreach (var body in unmatchedExpected)
						{
							errorMessage.AppendLine($"""
							  Body:
							{body.ToFullString()}
							
							""");
						}

						// errorMessage.AppendLine();
					}
					
					if (actualMethods.Count > 0)
					{
						errorMessage.AppendLine($"Unexpected generated methods ({actualMethods.Count}):");

						foreach (var method in actualMethods)
						{
							errorMessage.AppendLine($"""
								Method signature: {method.ReturnType} {method.Identifier}{method.ParameterList}
								Body:
							{method.Body?.ToFullString() ?? "(no body)"}
							
							""");
						}
					}
					
					throw new InvalidOperationException(errorMessage.ToString());
				}
				
				break;
		}
	}

	private static BlockSyntax ParseMethodBody(string bodyText)
	{
		// Trim whitespace
		var trimmedBody = bodyText.Trim();
		
		// Add curly braces if not present
		if (!trimmedBody.StartsWith('{'))
		{
			trimmedBody = $"{{\n{trimmedBody}\n}}";
		}
		
		// Wrap in a method to parse the body
		var wrappedCode = $$"""
			class TempClass
			{
				void TempMethod()
				{{trimmedBody}}
			}
			""";
		
		var tree = CSharpSyntaxTree.ParseText(wrappedCode);
		var root = tree.GetRoot();

		// Extract the body from the temporary method
		var method = root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.First();

		return method.Body!.NormalizeWhitespace();
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
			.Where(a => !String.IsNullOrEmpty(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location))
			.Cast<MetadataReference>()
			.ToList();

		references.Add(MetadataReference.CreateFromFile(typeof(ConstExprAttribute).Assembly.Location));

		return CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText(source)],
			references,
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));
	}

  // Builds the final source passed to Roslyn
  protected virtual string BuildSource()
  {
		return $$"""
		using ConstExpr.Core.Attributes;
		using ConstExpr.Core.Enumerators;
		using System;

		{{Invocations}}

		public static class TestMethods
		{
		{{TestMethod}}
		}
		""";
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