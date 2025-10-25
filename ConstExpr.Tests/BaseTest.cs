// using ConstExpr.Core.Attributes;
// using ConstExpr.SourceGenerator;
// using ConstExpr.SourceGenerator.Helpers;
// using ConstExpr.SourceGenerator.Visitors;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
// using Microsoft.CodeAnalysis.Operations;
// using System.Collections;
// using System.Collections.Immutable;
//
// namespace ConstExpr.Tests;
//
// public abstract class BaseTest<TResult>
// {
// 	public abstract IEnumerable<TResult> Result { get; }
// 	public abstract string SourceCode { get; }
//
// 	[Fact]
// 	public virtual void RunTest()
// 	{
// 		var generated = RunGenerator(out var compilation);
// 		var result = GetResult(generated, compilation);
//
// 		var diagnostics = generated.Diagnostics;
// 		var exceptions = diagnostics.Select(s => new InvalidOperationException(s.ToString()));
//
// 		switch (diagnostics.Length)
// 		{
// 			case 1:
// 				throw exceptions.First();
// 			case > 1:
// 				throw new AggregateException(exceptions);
// 			default:
// 				Assert.Equal(Result, result);
// 				break;
// 		}
//
// 	}
//
// 	protected GeneratorDriverRunResult RunGenerator(out Compilation outputCompilation)
// 	{
// 		var compilation = CreateCompilation(SourceCode);
// 		var generator = new ConstExprSourceGeneratorHoist(new());
// 		var optionsProvider = new TestAnalyzerConfigOptionsProvider();
//
// 		var driver = CSharpGeneratorDriver
// 			.Create(generator)
// 			.WithUpdatedAnalyzerConfigOptions(optionsProvider)
// 			.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);
//
// 		return driver.GetRunResult();
// 	}
//
// 	protected IEnumerable<TResult> GetResult(GeneratorDriverRunResult result, Compilation compilation)
// 	{
// 		var generatedTrees = result.GeneratedTrees;
//
// 		if (generatedTrees.Length == 0)
// 		{
// 			return [];
// 		}
//
// 		var loader = MetadataLoader.GetLoader(compilation);
// 		var last = generatedTrees.Last();
//
// 		var methods = last.GetRoot()
// 			.DescendantNodes()
// 			.OfType<MethodDeclarationSyntax>();
//
// 		var variables = new Dictionary<string, object?>();
// 		var visitor = new ConstExprOperationVisitor(compilation, loader, (_, _) => { }, CancellationToken.None);
//
// 		foreach (var method in methods)
// 		{
// 			var model = compilation.GetSemanticModel(method.SyntaxTree);
// 			var operation = model.GetOperation(method) as IMethodBodyOperation;
//
// 			visitor.VisitBlock(operation.BlockBody, variables);
// 		}
//
// 		return (variables[ConstExprOperationVisitor.RETURNVARIABLENAME] as IEnumerable)?.Cast<TResult>() ?? [];
// 	}
//
// 	private static CSharpCompilation CreateCompilation(string source)
// 	{
// 		var references = AppDomain.CurrentDomain.GetAssemblies()
// 			.Where(a => !String.IsNullOrEmpty(a.Location))
// 			.Select(a => MetadataReference.CreateFromFile(a.Location))
// 			.Cast<MetadataReference>()
// 			.ToList();
//
// 		references.Add(MetadataReference.CreateFromFile(typeof(ConstExprAttribute).Assembly.Location));
//
// 		return CSharpCompilation.Create(
// 			"TestAssembly",
// 			[CSharpSyntaxTree.ParseText(source)],
// 			references,
// 			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
// 	}
// }
//
// internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
// {
// 	private readonly AnalyzerConfigOptions _global = new TestGlobalAnalyzerConfigOptions();
// 	public override AnalyzerConfigOptions GlobalOptions => _global;
// 	public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => DictionaryAnalyzerConfigOptions.Empty;
// 	public override AnalyzerConfigOptions GetOptions(AdditionalText text) => DictionaryAnalyzerConfigOptions.Empty;
// }
//
// internal sealed class TestGlobalAnalyzerConfigOptions : AnalyzerConfigOptions
// {
// 	private readonly Dictionary<string, string> _data = new(StringComparer.OrdinalIgnoreCase)
// 	{
// 		{ "build_property.UseConstExpr", "true" }
// 	};
// 	public override bool TryGetValue(string key, out string value) => _data.TryGetValue(key, out value!);
// }
//
// internal sealed class DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> options) : AnalyzerConfigOptions
// {
// 	internal static readonly ImmutableDictionary<string, string> EmptyDictionary = ImmutableDictionary.Create<string, string>(KeyComparer);
//
// 	public static DictionaryAnalyzerConfigOptions Empty { get; } = new DictionaryAnalyzerConfigOptions(EmptyDictionary);
//
// 	// Note: Do not rename. Older versions of analyzers access this field via reflection.
// 	// https://github.com/dotnet/roslyn/blob/8e3d62a30b833631baaa4e84c5892298f16a8c9e/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Options/EditorConfig/EditorConfigStorageLocationExtensions.cs#L21
// 	internal readonly ImmutableDictionary<string, string> Options = options;
//
// 	public override bool TryGetValue(string key, out string? value)
// 		=> Options.TryGetValue(key, out value);
//
// 	public override IEnumerable<string> Keys
// 		=> Options.Keys;
// }