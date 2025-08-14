using ConstExpr.SourceGenerator;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.Tests;

public abstract class BaseTest<TResult>
{
	public abstract IEnumerable<TResult> Result { get; }

	public abstract string SourceCode { get; }

	[Fact]
	public virtual void RunTest()
	{
		var generated = RunGenerator(out var compilation);
		var result = GetResult(generated, compilation);

		Assert.Equal(Result, result);
	}

	protected GeneratorDriverRunResult RunGenerator(out Compilation outputCompilation)
	{
		var compilation = CreateCompilation(SourceCode);

		// Create an instance of your generator
		var generator = new ConstExprSourceGeneratorHoist(new());

		// Generate the output
		var driver = CSharpGeneratorDriver
			.Create(generator)
			.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		// Second generator run using the output from the first run
		driver = CSharpGeneratorDriver
			.Create(generator)
			.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);

		return driver.GetRunResult();
	}

	protected IEnumerable<TResult> GetResult(GeneratorDriverRunResult result, Compilation compilation)
	{
		var loader = MetadataLoader.GetLoader(compilation);

		var last = result.GeneratedTrees.Last();

		var methods = last.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>();

		foreach (var method in methods)
		{
			var items = method
				.DescendantNodes()
				.Where(w => w is ReturnStatementSyntax or YieldStatementSyntax)
				.Select(s => SyntaxHelpers.GetConstantValue(compilation, loader, s, new Dictionary<string, object?>()));

			return items.Cast<TResult>();
		}

		return [ ];
	}

	private static Compilation CreateCompilation(string source)
	{
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !String.IsNullOrEmpty(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location))
			.Cast<MetadataReference>();

		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[ CSharpSyntaxTree.ParseText(source) ],
			references,
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));

		return compilation;
	}
}