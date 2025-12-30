extern alias sourcegen;
using System.Runtime.CompilerServices;
using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.Extensions;
using ConstExpr.Core.Enumerators;
using sourcegen::ConstExpr.SourceGenerator.Helpers;
using sourcegen::ConstExpr.SourceGenerator.Models;
using sourcegen::ConstExpr.SourceGenerator.Rewriters;

namespace ConstExpr.Tests;

public abstract class BaseTest<TDelegate>(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.Strict)
	where TDelegate : Delegate
{
	// the generated method bodies to be expected
	public abstract IEnumerable<KeyValuePair<string?, object?[]>> Result { get; }

	public abstract string TestMethod { get; }

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
		var attribute = new ConstExprAttribute { FloatingPointMode = evaluationMode };
		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalMethods = new Dictionary<SyntaxNode, bool>();

		var rewriter = new ConstExprPartialRewriter(semanticModel, loader, (_, exception) => { }, parameters, additionalMethods, new HashSet<string>(), attribute, CancellationToken.None, visitedMethods);

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
						""");
				}
			}
		}
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		return CSharpCompilation.Create(
			"TestAssembly",
			[ CSharpSyntaxTree.ParseText(source) ],
			[ MetadataReference.CreateFromFile(typeof(object).Assembly.Location) ],
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));
	}

	// Builds the final source passed to Roslyn
	protected string BuildSource()
	{
		return $""""
			using System;

			{TestMethod}
			"""";
	}

	protected static KeyValuePair<string?, object?[]> Create(string? key, params object?[] values)
	{
		// test if length of values matches delegate parameters
		var delegateParams = typeof(TDelegate).GetMethod("Invoke")!.GetParameters();

		if (values.Length != delegateParams.Length)
		{
			throw new InvalidOperationException("Parameter count mismatch.");
		}

		return KeyValuePair.Create(key, values);
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
			_ => value.ToString()
		};

	}
}