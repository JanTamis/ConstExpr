extern alias sourcegen;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.BuildIn;
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

	[Test]
	public async Task RunRandomTests()
	{
		var state = GetState();
		var attribute = new ConstExprAttribute { MathOptimizations = mathOptimizations, LinqOptimization = linqOptimization, Optimizations = optimizations, MaxUnrollIterations = maxUnrollIterations };
		var visitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
		var additionalSyntax = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer.Get());
		var parameters = new Dictionary<string, VariableItem>(state.ParameterNames.Count, StringComparer.Ordinal);

		var symbolStore = new ConcurrentDictionary<ulong, ISymbol>();
		var exceptionsDuringRewriting = new List<Exception>();
		var usings = new HashSet<string>();

		var analyzer = new InlineVariableAnalyzer(state.SemanticModel, symbolStore);
		var candidates = analyzer.FindInlineCandidates(state.Method.Body!);

		var rewriter = new ConstExprPartialRewriter(state.SemanticModel, state.Loader, (_, exception) => exceptionsDuringRewriting.Add(exception), parameters, additionalSyntax, usings, attribute, symbolStore, CancellationToken.None, visitedMethods);

		foreach (var testCase in CreateFoldedRandom())
		{
			visitedMethods.Clear();
			additionalSyntax.Clear();
			parameters.Clear();
			symbolStore.Clear();
			exceptionsDuringRewriting.Clear();
			usings.Clear();

			for (var i = 0; i < state.ParameterNames.Count; i++)
			{
				parameters[state.ParameterNames[i]] = new VariableItem(
					state.ParameterTypes[i],
					false,
					null,
					state.ParameterTypes[i] is { NullableAnnotation: NullableAnnotation.Annotated, IsValueType: false });
			}

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
						candidate.Symbol.Type, // Type is not needed for inlining, as the value will be directly substituted
						false,
						null,
						candidate.Symbol is { NullableAnnotation: NullableAnnotation.Annotated, Type.IsValueType: false })
					{
						CanBeInlined = true
					});
				}
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
			newBody = OptimizationPipeline.Apply(newBody!, state.Method.ParameterList, state.Method.Identifier, attribute, parameters, state.SemanticModel, symbolStore, additionalSyntax, usings) as BlockSyntax ?? newBody;

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