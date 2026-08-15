extern alias sourcegen;
using System.Collections;
using System.Collections.Concurrent;
using System.Numerics.Tensors;
using System.Reflection;
using System.Runtime.CompilerServices;
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

namespace ConstExpr.Tests;

public abstract class BaseTest<TDelegate>(FastMathFlags mathOptimizations = FastMathFlags.All, LinqOptimizationMode linqOptimization = LinqOptimizationMode.Unroll, OptimizationFlags optimizations = OptimizationFlags.All, uint maxUnrollIterations = 32)
	where TDelegate : Delegate
{
	/// <summary>
	///   A marker object to represent unknown parameter values in test cases. This indicates that a parameter's value is not
	///   known at compile time, and the optimizer should treat it as such.
	/// </summary>
	public static readonly object Unknown = new();

	/// <summary>
	///   A collection of test cases, where each test case consists of an expected method body (as a string) and an array of
	///   parameter values. The expected method body can be null to indicate that the body should not change. The parameter
	///   values can be set to <see cref="Unknown" /> to indicate that the value is not known at compile time. The source
	///   generator will optimize <see cref="TestMethod" /> based on the provided parameter values, and the resulting body will
	///   be compared against the expected body for each test case.
	/// </summary>
	public abstract IEnumerable<KeyValuePair<string?, object?[]>> TestCases { get; }

	/// <summary>
	///   The method to be tested, represented as a string. Use the <see cref="GetString" /> helper method to generate this
	///   string from a lambda expression. The method should be defined as a local function within the generated source code,
	///   and should match the signature of <typeparamref name="TDelegate" />. The body of the method will be optimized by the
	///   source generator, and the resulting body will be compared against the expected bodies defined in
	///   <see cref="TestCases" />.
	/// </summary>
	public abstract string TestMethod { get; }

	private TDelegate? _capturedMethod;

	private static int GetDelegateParameterCount()
	{
		return BaseTestShared.DelegateParameterCount.GetOrAdd(typeof(TDelegate), static delegateType => delegateType.GetMethod("Invoke")?.GetParameters().Length
		                                                                                                ?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{delegateType.FullName}'."));
	}

	private static bool IsVoidDelegate()
	{
		return (typeof(TDelegate).GetMethod("Invoke")?.ReturnType ?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{typeof(TDelegate).FullName}'.")) == typeof(void);
	}

	internal BaseTestClassState GetState()
	{
		return BaseTestShared.StateByType[GetType()];
	}

	[Before(Class)]
	public async static Task SetupAsync(ClassHookContext context)
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
				{
					throw compilationErrors.First();
				}
				case > 1:
				{
					throw new AggregateException(compilationErrors);
				}
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

		var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);

		// GetTypeInfo on a parameter's TypeSyntax does not resolve the nullable annotation even when
		// the compilation's nullable context is enabled; GetDeclaredSymbol's IParameterSymbol.Type does.
		var parameterTypes = method.ParameterList.Parameters
			.Select(p => semanticModel.GetDeclaredSymbol(p)?.Type ?? compilation.ObjectType)
			.ToList();

		var formattedOriginalBody = FormattingHelper.Format(method.Body!) as BlockSyntax ?? method.Body!;
		var formattedOriginalBodyTwice = FormattingHelper.Format(formattedOriginalBody) as BlockSyntax ?? formattedOriginalBody;

		// Computed once per class instead of once per RunTest/RunRandomTests invocation: candidates only
		// depend on method.Body/semanticModel, both fixed for the rest of the class's lifetime. The
		// symbolStore here is single-use and write-never-read by the analyzer (it's only ever consulted
		// as a fallback for synthetic/annotated nodes, none of which exist on the freshly parsed body).
		var candidates = new InlineVariableAnalyzer(semanticModel, new ConcurrentDictionary<ulong, ISymbol>()).FindInlineCandidates(method.Body!);

		var state = new BaseTestClassState
		{
			Compilation = compilation,
			Method = method,
			ParameterNames = parameterNames,
			ParameterTypes = parameterTypes,
			FormattedOriginalBody = formattedOriginalBody,
			FormattedOriginalBodyRendered = FormattingHelper.Render(formattedOriginalBodyTwice)!,
			SemanticModel = semanticModel,
			Loader = MetadataLoader.GetLoader(compilation),
			Candidates = candidates
		};

		BaseTestShared.StateByType[testType] = state;
	}

	[After(Class)]
	public static void TearDown(ClassHookContext context)
	{
		BaseTestShared.StateByType.TryRemove(context.ClassType, out _);
	}

	[Test, TestName, MethodDataSource(nameof(TestCases))]
	// [ArgumentDisplayFormatter<SyntaxFormatter>]
	public void RunTest(KeyValuePair<string?, object?[]> testCase)
	{
		var state = GetState();

		if (testCase.Value.Length != state.ParameterNames.Count)
		{
			throw new InvalidOperationException("Parameter count mismatch.");
		}

		var attribute = new ConstExprAttribute { MathOptimizations = mathOptimizations, LinqOptimization = linqOptimization, Optimizations = optimizations, MaxUnrollIterations = maxUnrollIterations };

		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalSyntax = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer.Get());

		var parameters = new Dictionary<string, VariableItem>(state.ParameterNames.Count, StringComparer.Ordinal);

		for (var i = 0; i < state.ParameterNames.Count; i++)
		{
			parameters[state.ParameterNames[i]] = new VariableItem(
				state.ParameterTypes[i],
				false,
				null,
				state.ParameterTypes[i] is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false });
		}

		var symbolStore = new ConcurrentDictionary<ulong, ISymbol>();
		var exceptionsDuringRewriting = new List<Exception>();
		var usings = new HashSet<string>();
		var rewriter = new ConstExprPartialRewriter(state.SemanticModel, state.Loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalSyntax, usings, attribute, symbolStore, CancellationToken.None, visitedMethods);

		var accessVariables = new Dictionary<string, int>();

		for (var i = 0; i < state.ParameterNames.Count; i++)
		{
			accessVariables.Add(state.ParameterNames[i], 0);
		}

		foreach (var candidate in state.Candidates)
		{
			var name = candidate.Symbol.Name;

			if (parameters.TryGetValue(name, out var variable))
			{
				variable.CanBeInlined = true;
			}
			else
			{
				parameters.Add(name, new VariableItem(
					candidate.Symbol.Type, // Type is not needed for inlining, as the value will be directly substituted
					false,
					null,
					candidate.Symbol is { NullableAnnotation: NullableAnnotation.Annotated, Type.IsValueType: false })
				{
					CanBeInlined = true
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
		newBody = ExceptionGuardSimplifier.Simplify(newBody!) as BlockSyntax;

		// Same shared pipeline the generator runs, so the harness cannot drift from it.
		newBody = OptimizationPipeline.Apply(newBody!, state.Method.ParameterList, state.Method.Identifier, attribute, parameters, state.SemanticModel, symbolStore, additionalSyntax, usings) as BlockSyntax ?? newBody;

		// NOTE: FormattingHelper.Format is not idempotent (BlockFormattingRewriter's grouping/spacing
		// heuristics can shift output on a second pass), and every other rendered comparison string in
		// this file (GetOrParseBlock, BaseTestClassState.FormattedOriginalBodyRendered) is produced by
		// formatting twice before rendering. This explicit Format call has to stay paired with the one
		// FormattingHelper.Render performs internally, or newBodyRendered stops matching a
		// twice-formatted expected string that happens to differ from its once-formatted form.
		newBody = FormattingHelper.Format(newBody!) as BlockSyntax;
		var newBodyRendered = FormattingHelper.Render(newBody);

		if (testCase.Key is null)
		{
			// if (!SyntaxNodeComparer.Get<BlockSyntax>().Equals(expectedBody, newBody))
			if (newBodyRendered != state.FormattedOriginalBodyRendered)
			{
				throw FormatMismatchException(state.ParameterNames, parameters, state.FormattedOriginalBody, newBody, additionalSyntax, exceptionsDuringRewriting);
			}
		}
		else
		{
			var (expectedBody, expectedBodyRendered) = GetOrParseBlock(testCase.Key);

			// Use Roslyn structural equivalence which ignores trivia differences
			if (newBodyRendered != expectedBodyRendered)
			{
				throw FormatMismatchException(state.ParameterNames, parameters, expectedBody, newBody, additionalSyntax, exceptionsDuringRewriting);
			}
		}
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		// Deriving from a shared seed (rather than CSharpCompilation.Create from scratch every time)
		// reuses Roslyn's reference-binding graph across all ~539 test classes instead of re-resolving
		// the same large MetadataReferences list once per class.
		return BaseTestShared.SeedCompilation.Value.AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
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
	///   Helper method to create test cases with a specific expected body and parameter values.
	/// </summary>
	/// <param name="expectedBody">The expected body of the test case. Use null for no changed body</param>
	/// <param name="parameters">
	///   The values for the parameters of the test case. Use <see cref="Unknown" /> for unknown
	///   parameter
	/// </param>
	/// <returns>A key-value pair representing the test case.</returns>
	/// <exception cref="InvalidOperationException">
	///   Thrown when the number of <see cref="parameters" /> does not match the
	///   number of parameters of <see cref="TDelegate" />.
	/// </exception>
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

	protected static KeyValuePair<string?, object?[]> CreateDefault()
	{
		return KeyValuePair.Create<string?, object?[]>(null, Enumerable.Repeat<object?>(Unknown, GetDelegateParameterCount()).ToArray());
	}

	/// <summary>
	///   Helper method to create test cases with a specific expected body and parameter values.
	/// </summary>
	/// <param name="expectedBody">The expected body of the test case. Use null for no changed body</param>
	/// <returns>A key-value pair representing the test case.</returns>
	protected static KeyValuePair<string?, object?[]> Create(string? expectedBody)
	{
		return KeyValuePair.Create(expectedBody, Enumerable.Repeat<object?>(Unknown, GetDelegateParameterCount()).ToArray());
	}

	/// <summary>
	///   Helper method to create test cases where the expected body is expressed as a lambda delegate instead of a raw string.
	///   The lambda source is captured via <see cref="CallerArgumentExpressionAttribute" /> and its body is extracted
	///   automatically.
	/// </summary>
	/// <param name="expectedBody">A delegate whose lambda body represents the expected optimized method body.</param>
	/// <param name="parameters">
	///   The values for the parameters of the test case. Use <see cref="Unknown" /> for unknown
	///   parameters.
	/// </param>
	/// <param name="lambdaSource">Auto-captured source of <paramref name="expectedBody" /> — do not pass explicitly.</param>
	/// <returns>A key-value pair representing the test case.</returns>
	/// <exception cref="InvalidOperationException">
	///   Thrown when the number of <see cref="parameters" /> does not match the
	///   number of parameters of <see cref="TDelegate" />.
	/// </exception>
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
	///   Helper method to create test cases where the expected body is expressed as a lambda delegate instead of a raw string.
	///   The lambda source is captured via <see cref="CallerArgumentExpressionAttribute" /> and its body is extracted
	///   automatically.
	/// </summary>
	/// <param name="expectedBody">A delegate whose lambda body represents the expected optimized method body.</param>
	/// <param name="lambdaSource">Auto-captured source of <paramref name="expectedBody" /> — do not pass explicitly.</param>
	/// <returns>A key-value pair representing the test case.</returns>
	protected static KeyValuePair<string?, object?[]> Create(TDelegate expectedBody, [CallerArgumentExpression(nameof(expectedBody))] string? lambdaSource = null)
	{
		var delegateParamCount = GetDelegateParameterCount();
		var body = TestMethodHelper.ExtractLambda(lambdaSource);

		return KeyValuePair.Create<string?, object?[]>(body, Enumerable.Repeat<object?>(Unknown, delegateParamCount).ToArray());
	}

	protected string GetString(TDelegate method, [CallerArgumentExpression(nameof(method))] string? lambdaSource = null)
	{
		_capturedMethod = method;

		var nullability = new NullabilityInfoContext();
		var returnType = TestMethodHelper.GetTypeName(method.Method.ReturnType, nullability.Create(method.Method.ReturnParameter));
		var parameters = method.Method.GetParameters();
		var paramList = System.String.Join(", ", parameters.Select(p => $"{TestMethodHelper.GetTypeName(p.ParameterType, nullability.Create(p))} {p.Name}"));

		// Try to extract body from CallerArgumentExpression
		var body = TestMethodHelper.ExtractLambdaBody(lambdaSource);

		return $"""
			{returnType} TestMethod({paramList})
			{body}
			""";
	}

	/// <summary>
	///   Helper method to create a test case whose expected result is computed by invoking the real
	///   <see cref="TestMethod" /> delegate with the given (fully known) parameter values, instead of a
	///   hand-typed literal. Guarantees the expected value can never drift from the logic under test.
	/// </summary>
	/// <param name="parameters">The known parameter values to invoke <see cref="TestMethod" /> with.</param>
	/// <returns>A key-value pair representing the test case.</returns>
	protected KeyValuePair<string?, object?[]> CreateFolded(params object?[] parameters)
	{
		var result = InvokeForFolding(parameters);

		// A void delegate's DynamicInvoke result is always null - that's "no return value", not "the
		// method returned the literal null". Wrapping it in `return null;` regardless produces invalid
		// C# for a void method (CS0127) and can never match a fully-folded void body, which - once
		// every statement has folded away - renders as an empty block, not one with a return.
		var expectedBody = IsVoidDelegate() ? "" : $"return {FormattingHelper.Render(SyntaxHelpers.CreateLiteral(result))};";

		return KeyValuePair.Create<string?, object?[]>(expectedBody, parameters);
	}

	/// <summary>
	///   Random-case variant of <see cref="CreateFolded" />, used only by
	///   <see cref="BaseTestWithRandomValues{TDelegate}.CreateFoldedRandom" />. Builds the expected body
	///   directly as a <see cref="BlockSyntax" /> instead of rendering it to source text and handing that
	///   text to <see cref="GetOrParseBlock" /> for a full Roslyn reparse - a randomly drawn expected body
	///   is essentially never reused across iterations, so that reparse (and the extra format pass it
	///   implies) is pure waste on this path.
	/// </summary>
	private protected (object?[] Parameters, BlockSyntax ExpectedBody, string ExpectedBodyRendered) CreateFoldedSyntax(object?[] parameters)
	{
		var result = InvokeForFolding(parameters);

		var block = IsVoidDelegate()
			? SyntaxFactory.Block()
			: SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxHelpers.CreateLiteral(result)));

		var formatted = FormattingHelper.Format(block) as BlockSyntax ?? block;
		var rendered = FormattingHelper.Render(formatted)!;

		return (parameters, formatted, rendered);
	}

	/// <summary>
	///   Shared validation + real-delegate invocation behind <see cref="CreateFolded" /> and
	///   <see cref="CreateFoldedSyntax" />.
	/// </summary>
	private object? InvokeForFolding(object?[] parameters)
	{
		var delegateParamCount = GetDelegateParameterCount();

		if (parameters.Length != delegateParamCount)
		{
			throw new InvalidOperationException("Parameter count mismatch.");
		}

		if (parameters.Any(p => ReferenceEquals(p, Unknown)))
		{
			throw new InvalidOperationException("CreateFolded requires all parameter values to be known; use Create(...) for cases with Unknown parameters.");
		}

		// Force the TestMethod getter to run on this instance, populating _capturedMethod. Guarded because the
		// getter rebuilds a NullabilityInfoContext and reflects over the whole signature every call, and the
		// random-value harness calls this once per draw (discarded duplicates included), not once per class.
		if (_capturedMethod is null)
		{
			_ = TestMethod;
		}

		// Invoke on a clone of each parameter: a method under test (e.g. an in-place array reverse)
		// may mutate its argument, and `parameters` is also handed to the rewriter afterwards as the
		// "known" input to fold from - if the real invocation mutated the same array/list object in
		// place, the rewriter would start folding from the post-mutation state instead of the original.
		return _capturedMethod!.DynamicInvoke(parameters.Select(CloneParameterForInvocation).ToArray());
	}

	/// <summary>
	///   Clones a parameter value that <see cref="CreateFolded" /> is about to hand to a real delegate
	///   invocation, so an in-place mutation performed by the method under test can't corrupt the
	///   original value also stored in the returned test case's parameters.
	/// </summary>
	private static object? CloneParameterForInvocation(object? value)
	{
		switch (value)
		{
			case null:
			{
				return null;
			}
			case System.Array array:
			{
				return array.Clone();
			}
			case not null when value.GetType() is { IsGenericType: true } listType && listType.GetGenericTypeDefinition() == typeof(List<>):
			{
				return Activator.CreateInstance(listType, value);
			}
			default:
			{
				return value;
			}
		}
	}

	protected static BlockSyntax ParseBlock(string code)
	{
		return GetOrParseBlock(code).Block;
	}

	protected static (BlockSyntax Block, string Rendered) GetOrParseBlock(string code)
	{
		return BaseTestShared.ParsedBlockCache.GetOrAdd(code, static key =>
		{
			var tree = SyntaxFactory.ParseSyntaxTree($$"""
				void TestMethod()
				{
					{{key}}
				}
				""");

			var block = tree.GetRoot()
				.DescendantNodes()
				.OfType<LocalFunctionStatementSyntax>()
				.Select(s => FormattingHelper.Format(s.Body!) as BlockSyntax ?? s.Body!)
				.First();

			return (block, FormattingHelper.Render(block)!);
		});
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

	protected InvalidOperationException FormatMismatchException(
		List<string> parameterNames,
		Dictionary<string, VariableItem> parameters,
		BlockSyntax? expectedBody,
		BlockSyntax? newBody,
		Dictionary<SyntaxNode, bool> additionalMethods,
		List<Exception> exceptionsDuringRewriting)
	{
		var parametersStr = System.String.Join(", ", parameterNames.Select(p =>
			$"{p} = {(parameters[p].HasValue ? ParseValue(parameters[p].Value) : "Unknown")}"));

		var expectedStr = FormattingHelper.Render(expectedBody) ?? "(null)";
		var generatedStr = FormattingHelper.Render(newBody) ?? "(null)";

		var additionalStr = additionalMethods.Count > 0
			? System.String.Join("\n\n", additionalMethods
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
			var exceptionsStr = System.String.Join("\n\n", exceptionsDuringRewriting.Select(e => e.ToString()));

			errorText += $"""


				Exceptions during rewriting:
				{exceptionsStr}
				""";
		}

		return new InvalidOperationException(errorText);
	}
}

/// <summary>
///   Shared infrastructure for all <see cref="BaseTest{TDelegate}" /> instantiations, regardless of
///   <c>TDelegate</c>. Deliberately declared as a top-level type rather than nested inside
///   <see cref="BaseTest{TDelegate}" /> - a type nested inside a generic class implicitly carries the
///   enclosing type's generic parameters at the CLR level, so a nested version of this class would get a
///   separate copy of these static fields per distinct closed <c>TDelegate</c> used across the test suite,
///   defeating the "initialized exactly once" intent.
/// </summary>
internal static class BaseTestShared
{
	private static readonly Type[] ForceLoadedTypes = [ typeof(TensorPrimitives) ];

	public static readonly ConcurrentDictionary<Type, BaseTestClassState> StateByType = new();
	public static readonly ConcurrentDictionary<Type, int> DelegateParameterCount = new();
	public static readonly ConcurrentDictionary<string, (BlockSyntax Block, string Rendered)> ParsedBlockCache = new(StringComparer.Ordinal);

	internal static readonly Lazy<IReadOnlyList<MetadataReference>> MetadataReferences = new(() =>
		{
			// Force-load assemblies needed in test compilations before scanning the AppDomain.
			foreach (var t in ForceLoadedTypes)
			{
				_ = t;
			}

			var appDomainRefs = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => !a.IsDynamic && !System.String.IsNullOrWhiteSpace(a.Location))
				.Select(a => a.Location)
				.ToHashSet(StringComparer.Ordinal);

			var result = appDomainRefs
				.Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
				.ToList();

			// Explicitly add force-loaded assemblies by location in case they were filtered out.
			foreach (var t in ForceLoadedTypes)
			{
				var location = t.Assembly.Location;

				if (!System.String.IsNullOrWhiteSpace(location) && appDomainRefs.Add(location))
				{
					result.Add(MetadataReference.CreateFromFile(location));
				}
			}

			return result;
		},
		true);

	/// <summary>
	///   A seed compilation carrying the shared references/options with zero syntax trees. Every test
	///   class's compilation is derived from this via <see cref="CSharpCompilation.AddSyntaxTrees(SyntaxTree[])" />
	///   instead of
	///   <see
	///     cref="CSharpCompilation.Create(string, IEnumerable{SyntaxTree}, IEnumerable{MetadataReference}, CSharpCompilationOptions)" />
	///   ,
	///   so Roslyn reuses the reference-binding graph built for this seed instead of rebuilding it from
	///   scratch for every one of the ~539 test classes.
	/// </summary>
	internal static readonly Lazy<CSharpCompilation> SeedCompilation = new(() =>
			CSharpCompilation.Create(
				"TestAssembly",
				[ ],
				MetadataReferences.Value,
				new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable)),
		true);
}

internal sealed class BaseTestClassState
{
	public Compilation Compilation { get; init; } = null!;
	public List<string> ParameterNames { get; init; } = null!;
	public List<ITypeSymbol> ParameterTypes { get; init; } = null!;
	public BlockSyntax FormattedOriginalBody { get; init; } = null!;
	public string FormattedOriginalBodyRendered { get; init; } = null!;
	public SemanticModel SemanticModel { get; init; } = null!;
	public MetadataLoader Loader { get; init; } = null!;
	public LocalFunctionStatementSyntax Method { get; init; } = null!;
	public IReadOnlyList<InlineCandidate> Candidates { get; init; } = null!;
}