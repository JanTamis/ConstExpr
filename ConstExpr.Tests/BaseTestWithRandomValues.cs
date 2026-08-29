extern alias sourcegen;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.Comparers;
using sourcegen::ConstExpr.SourceGenerator.Helpers;
using sourcegen::ConstExpr.SourceGenerator.Models;
using sourcegen::ConstExpr.SourceGenerator.Rewriters;

namespace ConstExpr.Tests;

[InheritsTests]
public abstract class BaseTestWithRandomValues<TDelegate>(FastMathFlags mathOptimizations = FastMathFlags.All, LinqOptimizationMode linqOptimization = LinqOptimizationMode.Unroll, OptimizationFlags optimizations = OptimizationFlags.All, uint maxUnrollIterations = 32) : BaseTest<TDelegate>(mathOptimizations, linqOptimization, optimizations, maxUnrollIterations)
	where TDelegate : Delegate
{
	/// <summary>
	///   The number of randomly generated test cases <see cref="RunRandomTests" /> checks. Every case is guaranteed to
	///   have a distinct expected body (see <see cref="CreateFoldedRandom" />), so this can't exceed however many
	///   distinct results the tested method can actually produce - override per test class accordingly.
	/// </summary>
	protected virtual int RandomTestCaseCount => 10;

	/// <summary>
	///   Caps the bit-length (and thus magnitude) of randomly generated integral values. Defaults to each type's own
	///   full range. Override to a smaller value for methods whose runtime cost (e.g. loop iteration count) scales
	///   with the parameter's magnitude, so <see cref="BaseTest{TDelegate}" />'s constructor-level
	///   <c>maxUnrollIterations</c> can still fully unroll and fold every generated case.
	/// </summary>
	protected virtual int MaxRandomMagnitudeBits => Int32.MaxValue;

	/// <summary>
	///   Caps the power-of-two exponent (and thus magnitude) of randomly generated double/float/decimal values.
	///   Defaults to 20 (values up to ~2^20). Override to a smaller value for methods that overflow to
	///   Infinity/NaN well before that (e.g. <see cref="System.Math.Exp(double)" />, whose result overflows once
	///   the input exceeds ~709) so every generated case stays foldable to a finite literal.
	/// </summary>
	protected virtual int MaxRandomFloatExponent => 20;

	/// <summary>
	///   The minimum number of distinct random cases <see cref="RunRandomTests" /> must actually check before it
	///   accepts the run as meaningful. Defaults to 1, which catches the case this was added for: a method whose
	///   preconditions a random input essentially never satisfies (an unconstrained index, <c>Single()</c> needing
	///   an exact match) throws on every single draw, and since a throwing draw is discarded and retried, the test
	///   used to report green having checked nothing at all. Raise it above 1 on classes that tighten
	///   <see cref="MaxRandomMagnitudeBits" /> or <see cref="MaxRandomFloatExponent" />, where a narrower range
	///   makes more draws collide on an already-yielded expected body. Can never exceed however many distinct
	///   results the tested method can produce (a bool-returning method tops out at 2).
	/// </summary>
	protected virtual int MinRandomTestCaseCount => 1;

	[Test]
	public void RunRandomTests()
	{
		var state = GetState();
		var attribute = new ConstExprAttribute { MathOptimizations = mathOptimizations, LinqOptimization = linqOptimization, Optimizations = optimizations, MaxUnrollIterations = maxUnrollIterations };

		var seeds = BuildVariableSeeds(state);

		// Materialised so the case count can be checked before any rewriting happens. The loop below is kept
		// sequential deliberately: running the cases through a Parallel.ForEach (each with its own rewriter and
		// collections, sharing only the SemanticModel and MetadataLoader) was measured and rejected - on the
		// heaviest test class it tripled CPU through contention, left suite wall-clock unchanged at ~24 s, and
		// produced failures, so something below the rewriter still holds shared mutable state.
		var testCases = CreateFoldedRandom().ToList();

		// CreateFoldedRandom draws RandomTestCaseCount times but only yields cases with a distinct expected body,
		// silently discarding the rest, so without this a class whose every draw throws (or keeps producing the
		// same result) passes having checked nothing.
		if (testCases.Count < MinRandomTestCaseCount)
		{
			throw new InvalidOperationException($"""
				{GetType().Name} only checked {testCases.Count} distinct random case(s), below its MinRandomTestCaseCount of {MinRandomTestCaseCount}.
				{RandomTestCaseCount} draws were taken; the rest threw or collided on an already-yielded expected body.
				Either the method's preconditions reject nearly every random input (an unconstrained index, an exact-match
				requirement) - in which case this class should derive from BaseTest and not fuzz at all - or its generator
				knobs are too tight: MaxRandomMagnitudeBits = {MaxRandomMagnitudeBits}, MaxRandomFloatExponent = {MaxRandomFloatExponent}.
				""");
		}

		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalSyntax = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer.Get());
		var parameters = new Dictionary<string, VariableItem>(seeds.Count, StringComparer.Ordinal);
		var symbolStore = new ConcurrentDictionary<ulong, ISymbol>();
		var exceptionsDuringRewriting = new List<Exception>();
		var usings = new HashSet<string>();

		var rewriter = new ConstExprPartialRewriter(state.SemanticModel, state.Loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalSyntax, usings, attribute, symbolStore, CancellationToken.None, visitedMethods);

		foreach (var testCase in testCases)
		{
			visitedMethods.Clear();
			additionalSyntax.Clear();
			parameters.Clear();
			symbolStore.Clear();
			exceptionsDuringRewriting.Clear();
			usings.Clear();

			// Fresh VariableItem instances every case, deliberately: the rewriter mutates them (and adds
			// entries of its own for locals it encounters), and they carry more state than the five fields
			// reset below - UnknownIndices' per-element array tracking and the never-cleared IsAltered flag
			// would both leak from one random case into the next.
			foreach (var seed in seeds)
			{
				parameters[seed.Name] = new VariableItem(seed.Type, false, null, seed.CanBeNull)
				{
					CanBeInlined = seed.CanBeInlined
				};
			}

			for (var i = 0; i < testCase.Parameters.Length; i++)
			{
				var name = state.ParameterNames[i];
				var parameter = parameters[name];
				var value = testCase.Parameters[i];

				parameter.HasValue = true;
				parameter.Value = value;
				parameter.IsAccessed = false;
				parameter.IsAltered = false;
				parameter.IsInitialized = true;
			}

			var newBody = rewriter.VisitBlock(state.Method.Body!) as BlockSyntax;

			foreach (var parameter in parameters)
			{
				if (!newBody!.HasIdentifier(parameter.Key))
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
			newBody = OptimizationPipeline.Apply(newBody!, state.Method.ParameterList, state.Method.Identifier, attribute, parameters, state.SemanticModel, symbolStore, additionalSyntax, usings, state.Method.ReturnType) as BlockSyntax ?? newBody;

			// NOTE: FormattingHelper.Format is not idempotent (BlockFormattingRewriter's grouping/spacing
			// heuristics can shift output on a second pass). testCase.ExpectedBodyRendered was produced by
			// CreateFoldedSyntax formatting twice before rendering (matching every other rendered
			// comparison string in this codebase), so newBody must go through the same two passes - one
			// here, one inside FormattingHelper.Render - or the two sides can disagree despite being
			// semantically identical.
			newBody = FormattingHelper.Format(newBody!) as BlockSyntax;
			var newBodyRendered = FormattingHelper.Render(newBody);

			if (newBodyRendered != testCase.ExpectedBodyRendered)
			{
				throw FormatMismatchException(state.ParameterNames, parameters, testCase.ExpectedBody, newBody, additionalSyntax, exceptionsDuringRewriting);
			}
		}
	}

	/// <summary>
	///   Derives the per-case parameter/inline-candidate seed list once per run. Only the symbol inspection is
	///   hoisted out of the loop - name, type and nullability are fixed for the class's lifetime - not the
	///   <see cref="SourceGenerator.Models.VariableItem" /> allocation itself, which has to stay per-case (see the loop's
	///   comment).
	/// </summary>
	private static List<(string Name, ITypeSymbol Type, bool CanBeNull, bool CanBeInlined)> BuildVariableSeeds(BaseTestClassState state)
	{
		var seeds = new List<(string Name, ITypeSymbol Type, bool CanBeNull, bool CanBeInlined)>(state.ParameterNames.Count + state.Candidates.Count);
		var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

		for (var i = 0; i < state.ParameterNames.Count; i++)
		{
			indexByName[state.ParameterNames[i]] = seeds.Count;

			seeds.Add((
				state.ParameterNames[i],
				state.ParameterTypes[i],
				state.ParameterTypes[i] is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false },
				false));
		}

		foreach (var candidate in state.Candidates)
		{
			var name = candidate.Symbol.Name;

			if (indexByName.TryGetValue(name, out var index))
			{
				var seed = seeds[index];

				seeds[index] = (seed.Name, seed.Type, seed.CanBeNull, true);
			}
			else
			{
				indexByName[name] = seeds.Count;

				seeds.Add((
					name,
					candidate.Symbol.Type, // Type is not needed for inlining, as the value will be directly substituted
					candidate.Symbol is { NullableAnnotation: NullableAnnotation.Annotated, Type.IsValueType: false },
					true));
			}
		}

		return seeds;
	}

	/// <summary>
	///   Lazily generates test cases with randomly generated (fully known) parameter values, using
	///   <see cref="BaseTest{TDelegate}.CreateFoldedSyntax" /> to compute each expected result by invoking the real
	///   <see cref="BaseTest{TDelegate}.TestMethod" /> delegate. The seed defaults to a stable hash of the test class's type
	///   name, so results are reproducible across runs unless explicitly overridden. Every yielded case has a distinct
	///   expected body - a randomly generated input that throws (violates a precondition the method under test enforces
	///   at runtime, e.g. <c>Single()</c> matching zero or several elements), or that folds to a body already yielded,
	///   is discarded and a new one is drawn in its place. Stops after <see cref="RandomTestCaseCount" /> total draws, however many distinct cases that produced - callers that need
	///   a guaranteed count should assert on the sequence length, not assume it always reaches
	///   <see cref="RandomTestCaseCount" />.
	/// </summary>
	protected IEnumerable<(object?[] Parameters, BlockSyntax ExpectedBody, string ExpectedBodyRendered)> CreateFoldedRandom(int? seed = null)
	{
		var parameterTypes = typeof(TDelegate).GetMethod("Invoke")?.GetParameters().Select(p => p.ParameterType).ToArray()
		                     ?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{typeof(TDelegate).FullName}'.");

		var random = new Random(seed ?? GetStableSeed(GetType()));
		var seenBodies = new HashSet<string>(StringComparer.Ordinal);

		for (var attempt = 0; attempt < RandomTestCaseCount; attempt++)
		{
			var parameters = parameterTypes.Select(t => GenerateRandomValue(t, random)).ToArray();
			(object?[] Parameters, BlockSyntax ExpectedBody, string ExpectedBodyRendered) testCase;

			try
			{
				testCase = CreateFoldedSyntax(parameters);
			}
			catch (TargetInvocationException)
			{
				// The real TestMethod threw for this input (e.g. Single() matched zero or several
				// elements, First()/Last()/Aggregate() ran on an empty array, ElementAt() got an
				// out-of-range index) - the input violated a precondition the method itself enforces
				// at runtime rather than something the optimizer needs to handle. Draw a new one.
				continue;
			}

			if (seenBodies.Add(testCase.ExpectedBodyRendered))
			{
				yield return testCase;
			}
		}
	}

	private object GenerateRandomValue(Type type, Random random)
	{
		return type switch
		{
			_ when type == typeof(int) => (int) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(31, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(long) => ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(63, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(short) => (short) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(15, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(sbyte) => (sbyte) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(7, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(byte) => (byte) GenerateRandomMagnitude(random, System.Math.Min(8, MaxRandomMagnitudeBits)),
			_ when type == typeof(uint) => (uint) GenerateRandomMagnitude(random, System.Math.Min(32, MaxRandomMagnitudeBits)),
			_ when type == typeof(ulong) => (ulong) GenerateRandomMagnitude(random, System.Math.Min(63, MaxRandomMagnitudeBits)),
			_ when type == typeof(ushort) => (ushort) GenerateRandomMagnitude(random, System.Math.Min(16, MaxRandomMagnitudeBits)),
			_ when type == typeof(double) => ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, System.Math.Min(20, MaxRandomFloatExponent)), random),
			_ when type == typeof(float) => (float) ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, System.Math.Min(20, MaxRandomFloatExponent)), random),
			_ when type == typeof(decimal) => (decimal) ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, System.Math.Min(20, MaxRandomFloatExponent)), random),
			_ when type == typeof(bool) => random.Next(2) == 0,
			_ when type == typeof(char) => GenerateRandomChar(random),
			_ when type == typeof(string) => GenerateRandomString(random),
			// `object` itself is too broad to generate meaningfully - every current consumer
			// (Cast<int>()/OfType<int>() over an object[]/List<object> source) expects boxed ints,
			// so that's what we hand back rather than picking an arbitrary runtime type.
			_ when type == typeof(object) => GenerateRandomValue(typeof(int), random),
			_ when type.IsArray => GenerateRandomArray(type.GetElementType()!, random),
			_ when type.IsGenericType && CollectionSupport.GenericTypeDefinitions.Contains(type.GetGenericTypeDefinition())
				=> GenerateRandomList(type.GetGenericArguments()[0], random),
			_ when type.IsEnum => Enum.GetValues(type).GetValue(random.Next(Enum.GetValues(type).Length))!,
			_ => throw new NotSupportedException($"CreateFoldedRandom does not support generating random values for parameter type '{type}'. Use Create(...)/CreateFolded(...) directly for this test instead.")
		};
	}

	/// <summary>
	///   Generates a random-length (0-8 elements) array of <paramref name="elementType" />, recursively generating
	///   each element via <see cref="GenerateRandomValue" />. Includes the empty array, an important edge case for
	///   most collection-processing functions.
	///   <para>
	///     The 0-8 range is deliberately not configurable: capping it was measured on the suite's heaviest random
	///     test (<c>LinqCountOptimizationTests</c>) and did nothing - per-case cost came out flat at ~0.45 s
	///     whether the arrays held 8 elements or 2. Per-case cost tracks the body being rewritten, not the input
	///     size, so <see cref="RandomTestCaseCount" /> is the knob that actually moves runtime.
	///   </para>
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test-only helper; random test-case generation never runs under AOT/trimming.")]
	private System.Array GenerateRandomArray(Type elementType, Random random)
	{
		var length = random.Next(0, 9);
#pragma warning disable IL3050
		var array = System.Array.CreateInstance(elementType, length);
#pragma warning restore IL3050

		for (var i = 0; i < length; i++)
		{
			array.SetValue(GenerateRandomValue(elementType, random), i);
		}

		return array;
	}

	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test-only helper; random test-case generation never runs under AOT/trimming.")]
	private object GenerateRandomList(Type elementType, Random random)
	{
		var array = GenerateRandomArray(elementType, random);

		return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), array)!;
	}

	private static long ApplyRandomSign(long magnitude, Random random)
	{
		return random.Next(2) == 0 ? magnitude : -magnitude;
	}

	private static double ApplyRandomSign(double magnitude, Random random)
	{
		return random.Next(2) == 0 ? magnitude : -magnitude;
	}

	/// <summary>
	///   Floating-point analogue of <see cref="GenerateRandomMagnitude" />: picks a random power-of-two exponent in
	///   [<paramref name="minExponent" />, <paramref name="maxExponent" />] (each order of magnitude equally likely,
	///   for the same reason integer magnitudes are bit-length scaled rather than drawn linearly), plus one extra
	///   bucket below <paramref name="minExponent" /> that yields exact zero. A mantissa in [1, 2) is then scaled by
	///   2^exponent.
	/// </summary>
	private static double GenerateRandomFloatMagnitude(Random random, int minExponent, int maxExponent)
	{
		var exponent = random.Next(minExponent - 1, maxExponent + 1);

		if (exponent < minExponent)
		{
			return 0d;
		}

		var mantissa = 1 + random.NextDouble();

		return mantissa * System.Math.Pow(2, exponent);
	}

	/// <summary>
	///   Picks a random magnitude on a log/bit-length scale (0..<paramref name="maxBits" /> significant bits, each
	///   equally likely) rather than linearly across the whole range. A linear-uniform draw over e.g. the full
	///   32-bit int range almost always lands in the top one or two decimal-digit buckets (they cover ~47% of the
	///   range), starving small values and producing lots of same-result (duplicate expected body) cases; scaling by
	///   bit-length instead spreads draws roughly evenly across orders of magnitude.
	/// </summary>
	private static long GenerateRandomMagnitude(Random random, int maxBits)
	{
		var bits = random.Next(0, maxBits + 1);

		if (bits == 0)
		{
			return 0;
		}

		var lower = 1L << bits - 1;
		// 1L << bits overflows once bits reaches 63 (there is no 64th magnitude bit in a signed long) - that only
		// happens for the long/ulong case (maxBits == 63); every smaller type's own top bucket is well within range.
		var exclusiveUpper = bits >= 63 ? Int64.MaxValue : 1L << bits;

		return random.NextInt64(lower, exclusiveUpper);
	}

	/// <summary>
	///   Generates a random printable ASCII character (' ' through '~', 0x20-0x7E) - covers lowercase, uppercase,
	///   digits, punctuation, and spaces, while staying free of control characters and surrogate pairs that would
	///   complicate string-processing tests.
	/// </summary>
	private static char GenerateRandomChar(Random random)
	{
		return (char) random.Next(' ', '~' + 1);
	}

	/// <summary>
	///   Generates a random printable-ASCII string, 0-16 characters long. Includes the empty string, an important
	///   edge case for most string-processing functions.
	/// </summary>
	private static string GenerateRandomString(Random random)
	{
		var length = random.Next(0, 17);
		var chars = new char[length];

		for (var i = 0; i < length; i++)
		{
			chars[i] = GenerateRandomChar(random);
		}

		return new string(chars);
	}

	private static int GetStableSeed(Type type)
	{
		unchecked
		{
			var hash = 17;

			foreach (var c in type.FullName ?? type.Name)
			{
				hash = hash * 31 + c;
			}

			return hash;
		}
	}
}

/// <summary>
///   Generic collection interfaces/types that <see cref="BaseTestWithRandomValues{TDelegate}" /> can satisfy with a
///   <see cref="List{T}" /> instance - <see cref="List{T}" /> itself implements all of them, so a single generated
///   list can be handed back regardless of which one the delegate parameter actually declares. Kept as a top-level,
///   non-generic file class so the set isn't duplicated per <c>TDelegate</c> instantiation.
/// </summary>
file static class CollectionSupport
{
	public static readonly HashSet<Type> GenericTypeDefinitions =
	[
		typeof(List<>),
		typeof(IEnumerable<>),
		typeof(IReadOnlyList<>),
		typeof(IReadOnlyCollection<>),
		typeof(ICollection<>)
	];
}