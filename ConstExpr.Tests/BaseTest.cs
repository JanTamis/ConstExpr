extern alias sourcegen;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.BuildIn;
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
		return _delegateParameterCount.GetOrAdd(typeof(TDelegate), static delegateType => delegateType.GetMethod("Invoke")?.GetParameters().Length
		                                                                                  ?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{delegateType.FullName}'."));
	}

	private ClassState GetState() => _stateByType[GetType()];

	[Before(Class)]
	public static async Task SetupAsync(ClassHookContext context)
	{
		var testType = context.ClassType;
		var instance = Activator.CreateInstance(testType);
		var testMethodProperty = testType.GetProperty(nameof(TestMethod), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

		var symbolStore = new ConcurrentDictionary<ulong, ISymbol>();
		var exceptionsDuringRewriting = new List<Exception>();
		var rewriter = new ConstExprPartialRewriter(state.SemanticModel, state.Loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalSyntax, new HashSet<string>(), attribute, symbolStore, CancellationToken.None, visitedMethods);

		var accessVariables = new Dictionary<string, int>();

		for (var i = 0; i < state.ParameterNames.Count; i++)
		{
			accessVariables.Add(state.ParameterNames[i], 0);
		}

		var analyzer = new InlineVariableAnalyzer(state.SemanticModel, symbolStore);
		var candidates = analyzer.FindInlineCandidates(state.Method.Body!);

		foreach (var candidate in candidates)
		{
			var name = candidate.Symbol.Name;

			if (parameters.TryGetValue(name, out var variable))
			{
				variable.CanBeInlined = true;
			}
			else
			{
				parameters.Add(name, new VariableItem(
					type: candidate.Symbol.Type, // Type is not needed for inlining, as the value will be directly substituted
					hasValue: false,
					value: null)
				{
					CanBeInlined = true,
				});
			}
		}

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

		var newBody = rewriter.VisitBlock(state.Method.Body) as BlockSyntax;

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

		if (attribute.MathOptimizations.HasFlag(FastMathFlags.CommonSubexpressionElimination))
		{
			newBody = CommonSubexpressionEliminator.Eliminate(newBody) as BlockSyntax;
			newBody = DeadCodePruner.Prune(newBody, parameters, state.SemanticModel) as BlockSyntax;
		}

		if (attribute.MathOptimizations.HasFlag(FastMathFlags.TailRecursionElimination))
		{
			// Wrap the block in a pseudo MethodDeclarationSyntax so TailRecursionRewriter
			// can read the parameter list and the method name.
			var pseudoMethod = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.PredefinedType(
						SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
					state.Method.Identifier)
				.WithParameterList(state.Method.ParameterList)
				.WithBody(newBody);
			newBody = TailRecursionRewriter.Apply(pseudoMethod);
		}

		newBody = FormattingHelper.Format(newBody!) as BlockSyntax;

		if (testCase.Key is null)
		{
			var expectedBody = FormattingHelper.Format(state.FormattedOriginalBody) as BlockSyntax;

			// if (!SyntaxNodeComparer.Get<BlockSyntax>().Equals(expectedBody, newBody))
			if (FormattingHelper.Render(newBody) != FormattingHelper.Render(expectedBody))
			{
				throw FormatMismatchException(state.ParameterNames, parameters, expectedBody, newBody, additionalSyntax, exceptionsDuringRewriting);
			}
		}
		else
		{
			var expectedBody = ParseBlock(testCase.Key);

			// Use Roslyn structural equivalence which ignores trivia differences
			if (FormattingHelper.Render(newBody) != FormattingHelper.Render(expectedBody))
			{
				// Debug: find which statement differs
				var debugInfo = new StringBuilder();

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
	/// <exception cref="InvalidOperationException">Thrown when the number of <see cref="parameters"/> does not match the number of parameters of <see cref="TDelegate"/>.</exception>
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
		return KeyValuePair.Create(expectedBody, Enumerable.Repeat<object?>(Unknown, GetDelegateParameterCount()).ToArray());
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
		var delegateParamCount = GetDelegateParameterCount();

		if (parameters.Length != delegateParamCount)
		{
			throw new InvalidOperationException($"""
				Parameter count mismatch.
				{lambdaSource}
				""");
		}

		var body = TestMethodHelper.ExtractLambda(lambdaSource);

		return KeyValuePair.Create<string?, object?[]>(body, parameters);
	}

	/// <summary>
	/// Helper method to create test cases where the expected body is expressed as a lambda delegate instead of a raw string.
	/// The lambda source is captured via <see cref="CallerArgumentExpressionAttribute"/> and its body is extracted automatically.
	/// </summary>
	/// <param name="expectedBody">A delegate whose lambda body represents the expected optimized method body.</param>
	/// <param name="lambdaSource">Auto-captured source of <paramref name="expectedBody"/> — do not pass explicitly.</param>
	/// <returns>A key-value pair representing the test case.</returns>
	protected static KeyValuePair<string?, object?[]> Create(TDelegate expectedBody, [CallerArgumentExpression(nameof(expectedBody))] string? lambdaSource = null)
	{
		var delegateParamCount = GetDelegateParameterCount();
		var body = TestMethodHelper.ExtractLambda(lambdaSource);

		return KeyValuePair.Create<string?, object?[]>(body, Enumerable.Repeat<object?>(Unknown, delegateParamCount).ToArray());
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
			.Select(s => FormattingHelper.Format(s.Body!) as BlockSyntax ?? s.Body!)
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

	private static void Inline(BlockSyntax node, IDictionary<string, VariableItem> parameters, SemanticModel model)
	{
		var statements = node.Statements;

		if (statements.Count < 2)
		{
			return;
		}

		// Only consider local declarations that are direct children of this block,
		// since AnalyzeDataFlow requires a contiguous range of statements in the same block.
		for (var i = 0; i < statements.Count - 1; i++)
		{
			if (statements[i] is not LocalDeclarationStatementSyntax localDecl)
			{
				continue;
			}

			// Analyze data flow in the region *after* this declaration so that the
			// initial assignment is not counted as a write inside the analysed range.
			var dataFlow = model.AnalyzeDataFlow(statements[i + 1], statements[statements.Count - 1]);

			foreach (var declarator in localDecl.Declaration.Variables.Where(v => v.Initializer?.Value is not null))
			{
				if (model.GetDeclaredSymbol(declarator) is not ILocalSymbol symbol)
				{
					continue;
				}

				if (dataFlow is not { Succeeded: true })
				{
					continue;
				}

				// A variable may be inlined when it is:
				//   - read exactly once in the region after the declaration
				//   - never written to after the initial declaration
				//   - never passed by ref or out
				// WrittenInside covers both reassignments and ref/out arguments (Roslyn treats ref/out as writes).
				if (dataFlow.WrittenInside.Contains(symbol, SymbolEqualityComparer.Default))
				{
					continue;
				}

				// Count syntax-level reads in the region after the declaration (AnalyzeDataFlow has no count API).
				var name = declarator.Identifier.Text;
				var readCount = statements
					.Skip(i + 1)
					.SelectMany(s => s.DescendantNodes().OfType<IdentifierNameSyntax>())
					.Count(id => id.Identifier.Text == name);

				if (readCount != 1)
				{
					continue;
				}

				// Do not inline if any variable referenced in the initializer expression
				// is written strictly between the declaration and the read site.
				// Example: var temp = a; a = b; b = temp; — inlining temp→a would use
				// the updated value of a instead of the original.
				// Uses symbol equality (not string names) to avoid false positives from
				// lambda parameters that happen to share a name with other identifiers.

				// Find which statement contains the single read.
				var readStatementIndex = -1;

				for (var j = i + 1; j < statements.Count; j++)
				{
					if (statements[j].DescendantNodes().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.Text == name))
					{
						readStatementIndex = j;
						break;
					}
				}

				if (readStatementIndex == -1)
				{
					continue;
				}

				// If there are statements between the declaration and the read site,
				// check whether any symbol used in the initializer is written there.
				if (readStatementIndex > i + 1)
				{
					var intermediateFlow = model.AnalyzeDataFlow(statements[i + 1], statements[readStatementIndex - 1]);

					if (intermediateFlow is { Succeeded: true, WrittenInside.Length: > 0 })
					{
						var writtenSymbols = intermediateFlow.WrittenInside.ToImmutableHashSet(SymbolEqualityComparer.Default);

						var initializerRefsWritten = declarator.Initializer!.Value
							.DescendantNodesAndSelf()
							.OfType<IdentifierNameSyntax>()
							.Select(id => model.GetSymbolInfo(id).Symbol)
							.Where(s => s is ILocalSymbol or IParameterSymbol)
							.Any(writtenSymbols.Contains);

						if (initializerRefsWritten)
						{
							continue;
						}
					}
				}

				parameters.Add(name, new VariableItem(
					type: null!, // Type is not needed for inlining, as the value will be directly substituted
					hasValue: true,
					value: declarator.Initializer!.Value)
				{
					CanBeInlined = true,
				});
			}
		}
	}
}