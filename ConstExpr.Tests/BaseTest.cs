extern alias sourcegen;
using System.Collections;
using System.Runtime.CompilerServices;
using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.Extensions;
using ConstExpr.Core.Enumerators;
using sourcegen::ConstExpr.SourceGenerator.Comparers;
using sourcegen::ConstExpr.SourceGenerator.Helpers;
using sourcegen::ConstExpr.SourceGenerator.Models;
using sourcegen::ConstExpr.SourceGenerator.Rewriters;

namespace ConstExpr.Tests;


public abstract class BaseTest<TDelegate>(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.Strict, LinqOptimisationMode linqOptimisationMode = LinqOptimisationMode.Unroll)
	where TDelegate : Delegate
{
	
	/// <summary>
	/// A collection of test cases, where each test case consists of an expected method body (as a string) and an array of parameter values. The expected method body can be null to indicate that the body should not change. The parameter values can be set to <see cref="Unknown"/> to indicate that the value is not known at compile time. The source generator will optimize <see cref="TestMethod"/> based on the provided parameter values, and the resulting body will be compared against the expected body for each test case.
	/// </summary>
	public abstract IEnumerable<KeyValuePair<string?, object?[]>> TestCases { get; }

	/// <summary>
	/// The method to be tested, represented as a string. Use the <see cref="GetString"/> helper method to generate this string from a lambda expression. The method should be defined as a local function within the generated source code, and should match the signature of <typeparamref name="TDelegate"/>. The body of the method will be optimized by the source generator, and the resulting body will be compared against the expected bodies defined in <see cref="TestCases"/>.
	/// </summary>
	public abstract string TestMethod { get; }

	/// <summary>
	/// A marker object to represent unknown parameter values in test cases. This indicates that a parameter's value is not known at compile time, and the optimizer should treat it as such.
	/// </summary>
	protected static readonly object Unknown = new();

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
		var attribute = new ConstExprAttribute { FloatingPointMode = evaluationMode, LinqOptimisationMode = linqOptimisationMode };
		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalMethods = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer<SyntaxNode>.Instance);
		
		var exceptionsDuringRewriting = new List<Exception>();

		var rewriter = new ConstExprPartialRewriter(semanticModel, loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalMethods, new HashSet<string>(), attribute, CancellationToken.None, visitedMethods);
		
		foreach (var result in TestCases)
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

			additionalMethods.Clear();

			var newBody = rewriter.VisitBlock(method.Body!) as BlockSyntax;

			foreach (var parameter in parameters)
			{
				if (!newBody.HasIdentifier(parameter.Key))
				{
					parameter.Value.HasValue = true;
					parameter.Value.IsAccessed = false;
					parameter.Value.IsAltered = false;
					parameter.Value.IsInitialized = true;
				}
			}

			newBody = DeadCodePruner.Prune(newBody, parameters, semanticModel) as BlockSyntax;
			newBody = FormattingHelper.Format(newBody!) as BlockSyntax;

			if (result.Key is null)
			{
				var expectedBody = FormattingHelper.Format(method.Body!) as BlockSyntax;

				if (!expectedBody.EqualsTo(newBody))
				{
					throw new InvalidOperationException($"""
						Generated method body does not match expected body.
						Parameters: {string.Join(", ", parameterNames.Select(p => $"{p} = {(parameters[p].HasValue ? ParseValue(parameters[p].Value) : "Unknown")}"))}

						Expected body:
						{FormattingHelper.Render(expectedBody) ?? "(null)"}

						Generated body:
						{FormattingHelper.Render(newBody) ?? "(null)"}
						
						Additional Items:
						{string.Join("\n\n", additionalMethods.OrderBy(o => o.Value).Select(s => FormattingHelper.Render(s.Key) ?? "(null)"))}
						""");
				}
			}
			else
			{
				var expectedBody = ParseBlock(result.Key);

				expectedBody = FormattingHelper.Format(expectedBody) as BlockSyntax;

				// Use Roslyn structural equivalence which ignores trivia differences
				if (newBody == null || expectedBody == null || !expectedBody.EqualsTo(newBody))
				{
					throw new InvalidOperationException($"""
						Generated method body does not match expected body.
						Parameters: {string.Join(", ", parameterNames.Select(p => $"{p} = {(parameters[p].HasValue ? ParseValue(parameters[p].Value) : "Unknown")}"))}

						Expected body:
						{FormattingHelper.Render(expectedBody) ?? "(null)"}

						Generated body:
						{FormattingHelper.Render(newBody) ?? "(null)"}
						
						Additional Items:
						{string.Join("\n\n", additionalMethods.OrderBy(o => o.Value).Select(s => FormattingHelper.Render(s.Key) ?? "(null)"))}
						""");
				}
			}

			SymbolAnnotation.Clear();
		}
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location))
			.ToList();

		return CSharpCompilation.Create(
			"TestAssembly",
			[ CSharpSyntaxTree.ParseText(source) ],
			references,
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));
	}

	// Builds the final source passed to Roslyn
	protected string BuildSource()
	{
		return $""""
			using System;
			using System.Collections.Generic;
			using System.Linq;

			{TestMethod}
			"""";
	}

	/// <summary>
	/// Helper method to create test cases with a specific expected body and parameter values.
	/// </summary>
	/// <param name="expectedBody">The expected body of the test case. Use null for no changed body</param>
	/// <param name="parameters">The values for the parameters of the test case. Use <see cref="Unknown"/> for unknown parameter</param>
	/// <returns>A key-value pair representing the test case.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the number of <see cref="parameters"/> does not match the number of parameters of <see cref="TDelegate"/>.</exception>
	protected static KeyValuePair<string?, object?[]> Create(string? expectedBody, params object?[] parameters)
	{
		// test if length of values matches delegate parameters
		var delegateParams = typeof(TDelegate).GetMethod("Invoke")!.GetParameters();

		if (parameters.Length != delegateParams.Length)
		{
			throw new InvalidOperationException($"""
				Parameter count mismatch.
				{expectedBody}
				""");
		}

		return KeyValuePair.Create(expectedBody, parameters);
	}

	/// <summary>
	/// Helper method to create test cases where the expected body is expressed as a lambda delegate instead of a raw string.
	/// The lambda source is captured via <see cref="CallerArgumentExpressionAttribute"/> and its body is extracted automatically.
	/// </summary>
	/// <param name="expectedBody">A delegate whose lambda body represents the expected optimized method body.</param>
	/// <param name="parameters">The values for the parameters of the test case. Use <see cref="Unknown"/> for unknown parameters.</param>
	/// <param name="lambdaSource">Auto-captured source of <paramref name="expectedBody"/> — do not pass explicitly.</param>
	/// <returns>A key-value pair representing the test case.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the number of <see cref="parameters"/> does not match the number of parameters of <see cref="TDelegate"/>.</exception>
	protected static KeyValuePair<string?, object?[]> Create(TDelegate expectedBody, object?[] parameters, [CallerArgumentExpression(nameof(expectedBody))] string? lambdaSource = null)
	{
		var delegateParams = typeof(TDelegate).GetMethod("Invoke")!.GetParameters();

		if (parameters.Length != delegateParams.Length)
		{
			throw new InvalidOperationException($"""
				Parameter count mismatch.
				{lambdaSource}
				""");
		}

		var body = TestMethodHelper.ExtractLambdaBody(lambdaSource);

		return KeyValuePair.Create<string?, object?[]>(body, parameters);
	}

	protected string GetString(TDelegate method, [CallerArgumentExpression(nameof(method))] string? lambdaSource = null)
	{
		var returnType = TestMethodHelper.GetTypeName(method.Method.ReturnType);
		var parameters = method.Method.GetParameters();
		var paramList = string.Join(", ", parameters.Select(p => $"{TestMethodHelper.GetTypeName(p.ParameterType)} {p.Name}"));

		// Try to extract body from CallerArgumentExpression
		var body = TestMethodHelper.ExtractLambdaBody(lambdaSource);

		return $"""
			{returnType} TestMethod({paramList})
			{body}
			""";
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

	private string? ParseValue(object? value)
	{
		return value switch
		{
			null => "null",
			string s => $"\"{s}\"",
			IEnumerable items => $"[{System.String.Join(", ", items.Cast<object?>().Select(ParseValue))}]",
			_ => value.ToString()
		};
	}
}