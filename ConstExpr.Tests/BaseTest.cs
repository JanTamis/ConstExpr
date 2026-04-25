extern alias sourcegen;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ConstExpr.Core.Enumerators;
using sourcegen::ConstExpr.SourceGenerator.Comparers;
using sourcegen::ConstExpr.SourceGenerator.Helpers;
using sourcegen::ConstExpr.SourceGenerator.Models;
using sourcegen::ConstExpr.SourceGenerator.Rewriters;
using sourcegen::ConstExpr.SourceGenerator.Visitors;

namespace ConstExpr.Tests;

public abstract class BaseTest<TDelegate>(FastMathFlags mathOptimizations = FastMathFlags.Strict, LinqOptimisationMode linqOptimisationMode = LinqOptimisationMode.Unroll)
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
	public static readonly object Unknown = new();

	private sealed class ClassState
	{
		public Compilation Compilation { get; init; } = null!;
		public List<string> ParameterNames { get; init; } = null!;
		public List<ITypeSymbol> ParameterTypes { get; init; } = null!;
		public BlockSyntax FormattedOriginalBody { get; init; } = null!;
		public SemanticModel SemanticModel { get; init; } = null!;
		public MetadataLoader Loader { get; init; } = null!;
		public LocalFunctionStatementSyntax Method { get; init; } = null!;
	}

	private static readonly ConcurrentDictionary<Type, ClassState> _stateByType = new();
	private static readonly ConcurrentDictionary<Type, int> _delegateParameterCount = new();
	private static readonly Lazy<IReadOnlyList<MetadataReference>> _metadataReferences = new(() =>
		AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => a.Location)
			.Distinct(StringComparer.Ordinal)
			.Select(a => MetadataReference.CreateFromFile(a))
			.ToArray(),
		isThreadSafe: true);

	private static int GetDelegateParameterCount()
	{
		return _delegateParameterCount.GetOrAdd(typeof(TDelegate), static delegateType =>
			delegateType.GetMethod("Invoke")?.GetParameters().Length
			?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{delegateType.FullName}'."));
	}

	private ClassState GetState() => _stateByType[GetType()];

	[Before(Class)]
	public static async Task SetupAsync(ClassHookContext context)
	{
		var testType = context.ClassType;
		var instance = Activator.CreateInstance(testType);
		var testMethodProperty = testType.GetProperty(nameof(TestMethod), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
		var testMethodValue = testMethodProperty?.GetValue(instance) as string ?? throw new InvalidOperationException("TestMethod not found");

		var compilation = CreateCompilation(BuildSourceWithMethod(testMethodValue));

		var compilationErrors = compilation
			.GetDiagnostics()
			.Where(w => w.Severity == DiagnosticSeverity.Error)
			.Select(s => new InvalidOperationException(s.ToString()))
			.ToList();

		if (compilationErrors.Count > 0)
		{
			switch (compilationErrors.Count)
			{
				case 1:
					throw compilationErrors.First();
				case > 1:
					throw new AggregateException(compilationErrors);
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

		var parameterTypes = method.ParameterList.Parameters
			.Select(p => compilation.GetSemanticModel(method.SyntaxTree).GetTypeInfo(p.Type!).Type ?? compilation.ObjectType)
			.ToList();

		var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);
		var state = new ClassState
		{
			Compilation = compilation,
			Method = method,
			ParameterNames = parameterNames,
			ParameterTypes = parameterTypes,
			FormattedOriginalBody = FormattingHelper.Format(method.Body!) as BlockSyntax ?? method.Body!,
			SemanticModel = semanticModel,
			Loader = MetadataLoader.GetLoader(compilation),
		};

		_stateByType[testType] = state;
	}

	[After(Class)]
	public static void TearDown(ClassHookContext context)
	{
		_stateByType.TryRemove(context.ClassType, out _);
	}

	[Test]
	[TestName]
	[MethodDataSource(nameof(TestCases))]
	// [ArgumentDisplayFormatter<SyntaxFormatter>]
	public void RunTest(KeyValuePair<string?, object?[]> testCase)
	{
		var state = GetState();

		if (testCase.Value.Length != state.ParameterNames.Count)
		{
			throw new InvalidOperationException("Parameter count mismatch.");
		}

		var attribute = new ConstExprAttribute { MathOptimizations = mathOptimizations, LinqOptimisationMode = linqOptimisationMode };

		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalSyntax = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer.Get());

		var parameters = new Dictionary<string, VariableItem>(state.ParameterNames.Count, StringComparer.Ordinal);

		for (var i = 0; i < state.ParameterNames.Count; i++)
		{
			parameters[state.ParameterNames[i]] = new VariableItem(
				type: state.ParameterTypes[i],
				hasValue: false,
				value: null);
		}

		var exceptionsDuringRewriting = new List<Exception>();

		var rewriter = new ConstExprPartialRewriter(state.SemanticModel, state.Loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalSyntax, new HashSet<string>(), attribute, new(), CancellationToken.None, visitedMethods);

		for (var i = 0; i < testCase.Value.Length; i++)
		{
			var name = state.ParameterNames[i];
			var parameter = parameters[name];
			var value = testCase.Value[i];

			if (ReferenceEquals(value, Unknown))
			{
				parameter.HasValue = false;
				parameter.Value = null;
			}
			else
			{
				parameter.HasValue = true;
				parameter.Value = value;
			}

			parameter.IsAccessed = false;
			parameter.IsAltered = false;
			parameter.IsInitialized = true;
		}

		var newBody = rewriter.VisitBlock(state.Method.Body!) as BlockSyntax;

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

		newBody = DeadCodePruner.Prune(newBody, parameters, state.SemanticModel) as BlockSyntax;
		newBody = FormattingHelper.Format(newBody!) as BlockSyntax;

		if (testCase.Key is null)
		{
			var expectedBody = state.FormattedOriginalBody;

			if (!SyntaxNodeComparer.Get<BlockSyntax>().Equals(expectedBody, newBody))
			{
				throw FormatMismatchException(state.ParameterNames, parameters, expectedBody, newBody, additionalSyntax, exceptionsDuringRewriting);
			}
		}
		else
		{
			var expectedBody = ParseBlock(testCase.Key);

			expectedBody = FormattingHelper.Format(expectedBody) as BlockSyntax;

			// Use Roslyn structural equivalence which ignores trivia differences
			if (newBody == null || expectedBody == null || !SyntaxNodeComparer.Get<BlockSyntax>().Equals(expectedBody, newBody))
			{
				// Debug: find which statement differs
				var debugInfo = new System.Text.StringBuilder();

				if (expectedBody != null && newBody != null)
				{
					var visitor = DeteministicHashVisitor.Instance;

					for (var si = 0; si < System.Math.Min(expectedBody.Statements.Count, newBody.Statements.Count); si++)
					{
						var expHash = visitor.Visit(expectedBody.Statements[si]);
						var genHash = visitor.Visit(newBody.Statements[si]);

						if (expHash != genHash)
						{
							debugInfo.AppendLine($"Statement {si} differs:");
							debugInfo.AppendLine($"  Expected ({expHash}): {expectedBody.Statements[si]}");
							debugInfo.AppendLine($"  Generated ({genHash}): {newBody.Statements[si]}");
						}
					}
				}

				throw FormatMismatchException(state.ParameterNames, parameters, expectedBody, newBody, additionalSyntax, exceptionsDuringRewriting, debugInfo.ToString());
			}
		}
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		return CSharpCompilation.Create(
			"TestAssembly",
			[ CSharpSyntaxTree.ParseText(source) ],
			_metadataReferences.Value,
			new CSharpCompilationOptions(OutputKind.ConsoleApplication));
	}

	private static string BuildSourceWithMethod(string testMethod)
	{
		return $"""
			using System;
			using System.Collections.Generic;
			using System.Linq;
			using System.Text.RegularExpressions;

			{testMethod}
			""";
	}

	/// <summary>
	/// Helper method to create test cases with a specific expected body and parameter values.
	/// </summary>
	/// <param name="expectedBody">The expected body of the test case. Use null for no changed body</param>
	/// <param name="parameters">The values for the parameters of the test case. Use <see cref="Unknown"/> for unknown parameter</param>
	/// <returns>A key-value pair representing the test case.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the number of <see cref="_parameters"/> does not match the number of parameters of <see cref="TDelegate"/>.</exception>
	protected static KeyValuePair<string?, object?[]> Create(string? expectedBody, params object?[] parameters)
	{
		var delegateParamCount = GetDelegateParameterCount();

		if (parameters.Length != delegateParamCount)
		{
			throw new InvalidOperationException($"""
				Parameter count mismatch.
				{expectedBody}
				""");
		}

		return KeyValuePair.Create(expectedBody, parameters);
	}

	/// <summary>
	/// Helper method to create test cases with a specific expected body and parameter values.
	/// </summary>
	/// <param name="expectedBody">The expected body of the test case. Use null for no changed body</param>
	/// <returns>A key-value pair representing the test case.</returns>
	protected static KeyValuePair<string?, object?[]> Create(string? expectedBody)
	{
		var parameters = new object?[GetDelegateParameterCount()];
		
		System.Array.Fill(parameters, Unknown);

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
	/// <exception cref="InvalidOperationException">Thrown when the number of <see cref="_parameters"/> does not match the number of parameters of <see cref="TDelegate"/>.</exception>
	protected static KeyValuePair<string?, object?[]> Create(TDelegate expectedBody, object?[] parameters, [CallerArgumentExpression(nameof(expectedBody))] string? lambdaSource = null)
	{
		var delegateParamCount = GetDelegateParameterCount();

		if (parameters.Length != delegateParamCount)
		{
			throw new InvalidOperationException($"""
				Parameter count mismatch.
				{lambdaSource}
				""");
		}

		var body = TestMethodHelper.ExtractLambdaBody(lambdaSource);

		return KeyValuePair.Create<string?, object?[]>(body, parameters);
	}

	protected static string GetString(TDelegate method, [CallerArgumentExpression(nameof(method))] string? lambdaSource = null)
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

	private InvalidOperationException FormatMismatchException(
		List<string> parameterNames,
		Dictionary<string, VariableItem> parameters,
		BlockSyntax? expectedBody,
		BlockSyntax? newBody,
		Dictionary<SyntaxNode, bool> additionalMethods,
		List<Exception> exceptionsDuringRewriting,
		string debugInfo = "")
	{
		var parametersStr = string.Join(", ", parameterNames.Select(p =>
			$"{p} = {(parameters[p].HasValue ? ParseValue(parameters[p].Value) : "Unknown")}"));

		var expectedStr = FormattingHelper.Render(expectedBody) ?? "(null)";
		var generatedStr = FormattingHelper.Render(newBody) ?? "(null)";
		
		var additionalStr = additionalMethods.Count > 0
			? string.Join("\n\n", additionalMethods
				.OrderBy(o => o.Value)
				.Select(s => FormattingHelper.Render(s.Key) ?? "(null)"))
			: "(none)";

		var errorText = $"""
			Generated method body does not match expected body.
			Parameters: {parametersStr}

			Expected body:
			{expectedStr}

			Generated body:
			{generatedStr}
			
			Debug:
			{debugInfo}
			""";

		if (additionalMethods.Count > 0)
		{
			errorText += $"""

				
				Additional Items:
				{additionalStr}
				""";
		}
		
		if (exceptionsDuringRewriting.Count > 0)
		{
			var exceptionsStr = string.Join("\n\n", exceptionsDuringRewriting.Select(e => e.ToString()));
			
			errorText += $"""

				
				Exceptions during rewriting:
				{exceptionsStr}
				""";
		}

		return new InvalidOperationException(errorText);
	}
}

public class TestNameAttribute : DisplayNameFormatterAttribute
{
	protected override string FormatDisplayName(DiscoveredTestContext context)
	{
		var className = context.TestDetails.ClassType.Name;
		var args = context.TestDetails.TestMethodArguments;

		if (args is { Length: > 0 } && args[0] is KeyValuePair<string?, object?[]> pair)
		{
			var values = new string[pair.Value.Length];

			for (var i = 0; i < pair.Value.Length; i++)
			{
				if (SyntaxHelpers.TryCreateLiteral(pair.Value[i], out var literal))
				{
					values[i] = literal.ToString()!;
				}
				else
				{
					values[i] = "Unknown";
				}
			}

			return $"{className}({string.Join(", ", values)})";
		}

		return className;
	}
}