using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Helpers;

public sealed class MethodPurityAnalyzer
{
	// -------------------------------------------------------------------------
	// Whitelisted types — every public instance/static method is considered pure
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<string> PureTypes = ImmutableHashSet.Create(
		StringComparer.Ordinal,

		// Core math
		"System.Math",
		"System.MathF",

		// Primitive types (all operators / Parse round-trips are pure)
		"System.Boolean",
		"System.Byte",
		"System.SByte",
		"System.Int16",
		"System.UInt16",
		"System.Int32",
		"System.UInt32",
		"System.Int64",
		"System.UInt64",
		"System.Int128",
		"System.UInt128",
		"System.Single",
		"System.Double",
		"System.Decimal",
		"System.Char",
		"System.Half",
		"System.IntPtr",
		"System.UIntPtr",

		// String (immutable — all methods return new values)
		"System.String",

		// Conversion helpers
		"System.Convert",
		"System.BitConverter",

		// Numerics
		"System.Numerics.BigInteger",
		"System.Numerics.Complex",
		"System.Numerics.BitOperations",
		"System.Numerics.Vector",
		"System.Numerics.Vector2",
		"System.Numerics.Vector3",
		"System.Numerics.Vector4",
		"System.Numerics.Quaternion",
		"System.Numerics.Matrix3x2",
		"System.Numerics.Matrix4x4",
		"System.Numerics.Plane",
		"System.Numerics.Vector64",
		"System.Numerics.Vector128",
		"System.Numerics.Vector256",
		"System.Numerics.Vector512",

		// SIMD — x86
		"System.Runtime.Intrinsics.X86.Sse",
		"System.Runtime.Intrinsics.X86.Sse2",
		"System.Runtime.Intrinsics.X86.Sse3",
		"System.Runtime.Intrinsics.X86.Ssse3",
		"System.Runtime.Intrinsics.X86.Sse41",
		"System.Runtime.Intrinsics.X86.Sse42",
		"System.Runtime.Intrinsics.X86.Avx",
		"System.Runtime.Intrinsics.X86.Avx2",
		"System.Runtime.Intrinsics.X86.Avx512F",
		"System.Runtime.Intrinsics.X86.Avx512BW",
		"System.Runtime.Intrinsics.X86.Avx512CD",
		"System.Runtime.Intrinsics.X86.Avx512DQ",
		"System.Runtime.Intrinsics.X86.Avx512Vbmi",
		"System.Runtime.Intrinsics.X86.Avx10v1",
		"System.Runtime.Intrinsics.X86.Fma",
		"System.Runtime.Intrinsics.X86.Bmi1",
		"System.Runtime.Intrinsics.X86.Bmi2",
		"System.Runtime.Intrinsics.X86.Lzcnt",
		"System.Runtime.Intrinsics.X86.Popcnt",
		"System.Runtime.Intrinsics.X86.Pclmulqdq",
		"System.Runtime.Intrinsics.X86.Aes",

		// SIMD — ARM
		"System.Runtime.Intrinsics.Arm.AdvSimd",
		"System.Runtime.Intrinsics.Arm.AdvSimd.Arm64",
		"System.Runtime.Intrinsics.Arm.ArmBase",
		"System.Runtime.Intrinsics.Arm.ArmBase.Arm64",
		"System.Runtime.Intrinsics.Arm.Crc32",
		"System.Runtime.Intrinsics.Arm.Crc32.Arm64",
		"System.Runtime.Intrinsics.Arm.Dp",
		"System.Runtime.Intrinsics.Arm.Rdm",
		"System.Runtime.Intrinsics.Arm.Sha1",
		"System.Runtime.Intrinsics.Arm.Sha256",
		"System.Runtime.Intrinsics.Arm.Aes",
		"System.Runtime.Intrinsics.Arm.Sve",

		// Vector128/256/512 helpers
		"System.Runtime.Intrinsics.Vector64",
		"System.Runtime.Intrinsics.Vector128",
		"System.Runtime.Intrinsics.Vector256",
		"System.Runtime.Intrinsics.Vector512",

		// Bit operations
		"System.Runtime.InteropServices.MemoryMarshal", // read-only overloads only — see blacklist

		// Tuple helpers
		"System.ValueTuple",
		"System.Tuple",

		// Span-friendly helpers (pure overloads)
		"System.MemoryExtensions"
	);

	// -------------------------------------------------------------------------
	// Whitelisted methods — individual pure methods on otherwise impure types
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<string> PureMethods = ImmutableHashSet.Create(
		StringComparer.Ordinal,

		// Object
		"System.Object.Equals",
		"System.Object.GetHashCode",
		"System.Object.ReferenceEquals",

		// Nullable<T>
		"System.Nullable`1.GetValueOrDefault",
		"System.Nullable`1.HasValue.get",
		"System.Nullable`1.Value.get",

		// GC — pure queries
		"System.GC.GetTotalMemory",

		// Enum
		"System.Enum.HasFlag",
		"System.Enum.CompareTo",
		"System.Enum.Equals",

		// Guid
		"System.Guid.Parse",
		"System.Guid.TryParse",
		"System.Guid.NewGuid", // deterministic within tests; acceptable

		// Uri
		"System.Uri.IsWellFormedUriString",
		"System.Uri.EscapeDataString",
		"System.Uri.UnescapeDataString",

		// Regex (compiled, no side effects on static methods)
		"System.Text.RegularExpressions.Regex.IsMatch",
		"System.Text.RegularExpressions.Regex.Match",
		"System.Text.RegularExpressions.Regex.Matches",
		"System.Text.RegularExpressions.Regex.Replace",
		"System.Text.RegularExpressions.Regex.Split",

		// Encoding
		"System.Text.Encoding.GetByteCount",
		"System.Text.Encoding.GetBytes",
		"System.Text.Encoding.GetCharCount",
		"System.Text.Encoding.GetChars",
		"System.Text.Encoding.GetString",

		// LINQ-style pure aggregates (on IEnumerable only — no deferred side effects assumed)
		"System.Linq.Enumerable.Count",
		"System.Linq.Enumerable.Any",
		"System.Linq.Enumerable.All",
		"System.Linq.Enumerable.First",
		"System.Linq.Enumerable.FirstOrDefault",
		"System.Linq.Enumerable.Last",
		"System.Linq.Enumerable.LastOrDefault",
		"System.Linq.Enumerable.Single",
		"System.Linq.Enumerable.SingleOrDefault",
		"System.Linq.Enumerable.Min",
		"System.Linq.Enumerable.Max",
		"System.Linq.Enumerable.Sum",
		"System.Linq.Enumerable.Average",
		"System.Linq.Enumerable.Contains",
		"System.Linq.Enumerable.ElementAt",
		"System.Linq.Enumerable.ElementAtOrDefault",
		"System.Linq.Enumerable.SequenceEqual"
	);

	// -------------------------------------------------------------------------
	// Pure interfaces — any method explicitly implementing one of these is pure
	// by contract (the interface semantics require deterministic, side-effect-free
	// behaviour).
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<string> PureInterfaces = ImmutableHashSet.Create(
		StringComparer.Ordinal,

		// Equality / ordering
		"System.IEquatable`1",
		"System.IComparable",
		"System.IComparable`1",
		"System.Collections.Generic.IEqualityComparer`1",
		"System.Collections.Generic.IComparer`1",
		"System.Collections.IEqualityComparer",
		"System.Collections.IComparer",
		"System.IStructuralEquatable",
		"System.IStructuralComparable",

		// Parsing / formatting (produce new value from input — no mutation)
		"System.IParsable`1",
		"System.ISpanParsable`1",
		"System.IUtf8SpanParsable`1",
		"System.IFormattable",
		"System.ISpanFormattable",
		"System.IUtf8SpanFormattable",

		// Numeric interfaces (.NET 7+ generic math — all methods are pure math ops)
		"System.Numerics.INumber`1",
		"System.Numerics.INumberBase`1",
		"System.Numerics.IBinaryInteger`1",
		"System.Numerics.IBinaryNumber`1",
		"System.Numerics.IFloatingPoint`1",
		"System.Numerics.IFloatingPointIeee754`1",
		"System.Numerics.IFloatingPointConstants`1",
		"System.Numerics.ISignedNumber`1",
		"System.Numerics.IUnsignedNumber`1",
		"System.Numerics.IMinMaxValue`1",
		"System.Numerics.IAdditiveIdentity`2",
		"System.Numerics.IMultiplicativeIdentity`2",
		"System.Numerics.IAdditionOperators`3",
		"System.Numerics.ISubtractionOperators`3",
		"System.Numerics.IMultiplyOperators`3",
		"System.Numerics.IDivisionOperators`3",
		"System.Numerics.IModulusOperators`3",
		"System.Numerics.IUnaryNegationOperators`2",
		"System.Numerics.IUnaryPlusOperators`2",
		"System.Numerics.IBitwiseOperators`3",
		"System.Numerics.IShiftOperators`3",
		"System.Numerics.IIncrementOperators`1",
		"System.Numerics.IDecrementOperators`1",
		"System.Numerics.IComparisonOperators`3",
		"System.Numerics.IEqualityOperators`3",
		"System.Numerics.IPowerFunctions`1",
		"System.Numerics.IRootFunctions`1",
		"System.Numerics.ILogarithmicFunctions`1",
		"System.Numerics.IExponentialFunctions`1",
		"System.Numerics.ITrigonometricFunctions`1",
		"System.Numerics.IHyperbolicFunctions`1",

		// Conversion
		"System.IConvertible"
	);

	// -------------------------------------------------------------------------
	// Sealed immutable reference types — instance methods without ref/out are
	// pure even though they are reference types (no mutable state to escape).
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<string> ImmutableSealedRefTypes =
		ImmutableHashSet.Create(
			StringComparer.Ordinal,
			"System.String",
			"System.Uri",
			"System.Version",
			"System.Guid", // struct, but just in case
			"System.Text.RegularExpressions.Regex"
		);

	// -------------------------------------------------------------------------
	// Explicitly impure — overrides everything else
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<string> ImpureMethods = ImmutableHashSet.Create(
		StringComparer.Ordinal,
		"System.Environment.get_TickCount",
		"System.Environment.get_TickCount64",
		"System.DateTime.get_Now",
		"System.DateTime.get_UtcNow",
		"System.DateTimeOffset.get_Now",
		"System.DateTimeOffset.get_UtcNow",
		"System.DateOnly.get_Today",
		"System.TimeOnly.get_Now",
		"System.Diagnostics.Stopwatch.GetTimestamp",
		"System.Random.Next",
		"System.Random.NextInt64",
		"System.Random.NextDouble",
		"System.Random.NextSingle",
		"System.Random.NextBytes",
		"System.Security.Cryptography.RandomNumberGenerator.GetBytes",
		"System.Security.Cryptography.RandomNumberGenerator.GetInt32",
		"System.Threading.Interlocked.Increment",
		"System.Threading.Interlocked.Decrement",
		"System.Threading.Interlocked.Add",
		"System.Threading.Interlocked.Exchange",
		"System.Threading.Interlocked.CompareExchange",
		"System.Console.WriteLine",
		"System.Console.Write",
		"System.Console.ReadLine",
		"System.Console.ReadKey",
		"System.GC.Collect",
		"System.GC.WaitForPendingFinalizers"
	);

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	private readonly Compilation _compilation;
	private readonly INamedTypeSymbol? _pureAttributeType;
	private readonly INamedTypeSymbol? _pureFunctionAttributeType;

	// Cycle guard for recursive body analysis
	private readonly HashSet<IMethodSymbol> _analysisStack =
		new(SymbolEqualityComparer.Default);

	// Cache results to avoid re-analysis of the same method
	private readonly Dictionary<IMethodSymbol, bool> _cache =
		new(SymbolEqualityComparer.Default);

	public MethodPurityAnalyzer(Compilation compilation)
	{
		_compilation = compilation;
		_pureAttributeType =
			compilation.GetTypeByMetadataName("System.Diagnostics.Contracts.PureAttribute");
		_pureFunctionAttributeType =
			compilation.GetTypeByMetadataName("JetBrains.Annotations.PureAttribute");
	}

	// =========================================================================
	// Public API
	// =========================================================================

	/// <summary>
	/// Returns <see langword="true"/> if <paramref name="method"/> is considered
	/// observationally pure.  Pass a <paramref name="semanticModel"/> to enable
	/// deep body analysis for user-defined methods.
	/// </summary>
	public bool IsPureMethod(
		IMethodSymbol method,
		SemanticModel? semanticModel = null,
		CancellationToken cancellationToken = default)
	{
		method = method.OriginalDefinition;

		if (_cache.TryGetValue(method, out var cached))
		{
			return cached;
		}

		var result = Analyze(method, semanticModel, cancellationToken);
		_cache[method] = result;
		return result;
	}

	// =========================================================================
	// Core pipeline
	// =========================================================================

	private bool Analyze(
		IMethodSymbol method,
		SemanticModel? semanticModel,
		CancellationToken ct)
	{
		// 0. Explicit impurity blacklist — checked first, always wins
		if (IsExplicitlyImpure(method))
		{
			return false;
		}

		// 1. [Pure] / [PureFunction] attribute
		if (HasPureAttribute(method))
		{
			return true;
		}

		// 2. Known-pure type whitelist
		if (IsInPureType(method))
		{
			return true;
		}

		// 3. Known-pure individual method whitelist
		if (IsKnownPureMethod(method))
		{
			return true;
		}

		// 4. Structural / signature heuristics — eliminate obviously impure methods
		//    (async, void, extern, ref/out params, …)
		if (!PassesStructuralChecks(method))
		{
			return false;
		}

		// ── From here on the method has passed basic structural checks. ──
		// The remaining steps infer purity from the symbol without a body.

		// 5. `readonly` modifier on the method itself (C# 8+).
		//    A `readonly` method on a struct guarantees `this` is not mutated.
		//    Combined with the earlier ref/out check this is a strong pure signal
		//    when the method is also static or on a value type.
		if (method.IsReadOnly)
		{
			return true;
		}

		// 6. Operator overloads
		if (method.MethodKind is MethodKind.UserDefinedOperator
		    or MethodKind.BuiltinOperator
		    or MethodKind.Conversion)
		{
			// Conversion and arithmetic operators on value types are universally pure.
			if (method.ContainingType.IsValueType)
			{
				return true;
			}

			// Conversion operators on immutable sealed ref types are also pure.
			if (IsImmutableSealedRefType(method.ContainingType))
			{
				return true;
			}
		}

		// 7. Property getters
		if (method.MethodKind == MethodKind.PropertyGet)
		{
			// readonly struct — all getters are pure (no mutation possible)
			if (method.ContainingType is { IsValueType: true, IsReadOnly: true })
			{
				return true;
			}

			// init-only backing store — the getter half is pure
			if (method.AssociatedSymbol is IPropertySymbol { SetMethod.IsInitOnly: true })
			{
				return true;
			}

			// Getter on an immutable sealed reference type (String, Uri, …)
			if (IsImmutableSealedRefType(method.ContainingType))
			{
				return true;
			}
		}

		// 8. Implements a known-pure interface method.
		//    e.g. IEquatable<T>.Equals, IComparable<T>.CompareTo,
		//    INumber<T>.op_Addition, ITrigonometricFunctions<T>.Sin, …
		if (ImplementsPureInterfaceMethod(method))
		{
			return true;
		}

		// 9. Fully value-type signature on a static method.
		//    If every parameter and the return type are unmanaged value types
		//    (no heap allocation possible), and the method is static, there is no
		//    ambient state that can be observed or mutated.
		if (method.IsStatic && HasFullyUnmanagedSignature(method))
		{
			return true;
		}

		// 10. Static method on a sealed type with only in/value parameters
		//     and an immutable return type.  Weaker than step 9 but catches
		//     things like static helpers returning strings on sealed utility classes.
		if (method.IsStatic &&
		    method.ContainingType.IsSealed &&
		    AllParametersReadOnly(method) &&
		    IsImmutableReturnType(method))
		{
			return true;
		}

		// 11. Deep body analysis (optional, user-defined source)
		if (semanticModel is not null)
		{
			return AnalyzeBody(method, semanticModel, ct);
		}

		// Not enough information — conservative: assume impure
		return false;
	}

	// =========================================================================
	// Step helpers
	// =========================================================================

	private bool IsExplicitlyImpure(IMethodSymbol method)
	{
		var key = GetMethodKey(method);
		return ImpureMethods.Contains(key);
	}

	private bool HasPureAttribute(IMethodSymbol method)
	{
		foreach (var attr in method.GetAttributes())
		{
			var cls = attr.AttributeClass;

			if (cls is null)
			{
				continue;
			}

			if (SymbolEqualityComparer.Default.Equals(cls, _pureAttributeType))
			{
				return true;
			}

			if (SymbolEqualityComparer.Default.Equals(cls, _pureFunctionAttributeType))
			{
				return true;
			}

			// Duck-typing: any attribute named "Pure" or "PureFunction"
			if (cls.Name is "PureAttribute" or "PureFunctionAttribute")
			{
				return true;
			}
		}

		// Also check the containing type (type-level [Pure] implies all methods)
		foreach (var attr in method.ContainingType.GetAttributes())
		{
			var cls = attr.AttributeClass;

			if (cls is null)
			{
				continue;
			}

			if (SymbolEqualityComparer.Default.Equals(cls, _pureAttributeType))
			{
				return true;
			}

			if (SymbolEqualityComparer.Default.Equals(cls, _pureFunctionAttributeType))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsInPureType(IMethodSymbol method)
	{
		// Walk up — covers nested types like AdvSimd.Arm64
		var type = method.ContainingType;

		while (type is not null)
		{
			var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
				.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

			if (PureTypes.Contains(name))
			{
				return true;
			}
			type = type.ContainingType;
		}

		return false;
	}

	private static bool IsKnownPureMethod(IMethodSymbol method)
	{
		var key = GetMethodKey(method);
		return PureMethods.Contains(key);
	}

	private static bool PassesStructuralChecks(IMethodSymbol method)
	{
		// Async methods have scheduler side effects
		if (method.IsAsync)
		{
			return false;
		}

		// void return — only useful for side effects
		if (method.ReturnsVoid)
		{
			return false;
		}

		// extern / DllImport — unknown native code
		if (method.IsExtern)
		{
			return false;
		}

		// Abstract without implementation — behaviour is unknown
		if (method.IsAbstract)
		{
			return false;
		}

		// ref / out parameters indicate mutation of caller's state
		foreach (var p in method.Parameters)
		{
			if (p.RefKind is RefKind.Out or RefKind.Ref)
			{
				return false;
			}
		}

		return true;
	}

	// ── Helpers used by the no-body heuristic steps ───────────────────────────

	/// <summary>
	/// Returns true if the method explicitly implements a method declared on a
	/// known-pure interface (e.g. IEquatable&lt;T&gt;.Equals, INumber&lt;T&gt;.op_Addition).
	/// </summary>
	private static bool ImplementsPureInterfaceMethod(IMethodSymbol method)
	{
		// ExplicitInterfaceImplementations is populated for explicit impls.
		foreach (var iface in method.ExplicitInterfaceImplementations)
		{
			if (IsPureInterfaceMethod(iface))
			{
				return true;
			}
		}

		// For implicit implementations we walk the interface map of the containing type.
		if (method.ContainingType is not { } type)
		{
			return false;
		}

		foreach (var iface in type.AllInterfaces)
		{
			var ifaceName = GetGenericTypeName(iface);

			if (!PureInterfaces.Contains(ifaceName))
			{
				continue;
			}

			foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
			{
				var impl = type.FindImplementationForInterfaceMember(member);

				if (SymbolEqualityComparer.Default.Equals(impl, method))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool IsPureInterfaceMethod(IMethodSymbol ifaceMethod)
	{
		var typeName = GetGenericTypeName(ifaceMethod.ContainingType);
		return PureInterfaces.Contains(typeName);
	}

	/// <summary>
	/// Returns true when all parameters and the return type are unmanaged value
	/// types.  An unmanaged static method cannot reach any heap object, so it
	/// has nowhere to store state and nowhere to observe shared mutable state.
	/// </summary>
	private static bool HasFullyUnmanagedSignature(IMethodSymbol method)
	{
		if (!IsUnmanagedOrVoid(method.ReturnType))
		{
			return false;
		}

		foreach (var p in method.Parameters)
		{
			// Allow `in T` (readonly reference to value type) — no mutation
			if (p.RefKind is RefKind.Ref or RefKind.Out)
			{
				return false;
			}

			if (!IsUnmanagedOrVoid(p.Type))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsUnmanagedOrVoid(ITypeSymbol type)
	{
		// Void is only valid as a return type; treat as ok
		if (type.SpecialType == SpecialType.System_Void)
		{
			return true;
		}

		if (!type.IsValueType)
		{
			return false;
		}

		// Generic type parameters without `unmanaged` constraint are not guaranteed
		if (type is ITypeParameterSymbol tp)
		{
			return tp.HasUnmanagedTypeConstraint;
		}

		// Recurse into generic value types (e.g. Vector128<float>)
		if (type is INamedTypeSymbol { IsGenericType: true } named)
		{
			return named.TypeArguments.All(IsUnmanagedOrVoid);
		}

		// Plain non-generic value type (int, float, custom struct, enum, …)
		return true;
	}

	/// <summary>
	/// Returns true when every parameter is passed by value, `in`, or `ref readonly`
	/// — i.e. the method cannot mutate any caller-owned state through its parameters.
	/// </summary>
	private static bool AllParametersReadOnly(IMethodSymbol method)
	{
		foreach (var p in method.Parameters)
		{
			if (p.RefKind is RefKind.Out or RefKind.Ref)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Returns true when the return type is a well-known immutable type
	/// (string, primitive, value type, …) — used as an additional signal for
	/// static sealed helpers.
	/// </summary>
	private static bool IsImmutableReturnType(IMethodSymbol method)
	{
		var ret = method.ReturnType;

		if (ret.IsValueType)
		{
			return true;
		}

		if (ret.SpecialType == SpecialType.System_String)
		{
			return true;
		}

		if (IsImmutableSealedRefType(ret))
		{
			return true;
		}

		return false;
	}

	private static bool IsImmutableSealedRefType(ITypeSymbol type)
	{
		var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
			.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
		return ImmutableSealedRefTypes.Contains(name);
	}

	/// <summary>
	/// Returns the fully qualified type name with generic arity suffix preserved
	/// (e.g. <c>System.IEquatable`1</c>) for interface matching.
	/// </summary>
	private static string GetGenericTypeName(INamedTypeSymbol type)
	{
		// Use the original definition so T is still a type parameter
		var original = type.OriginalDefinition;
		return original.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
			.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
	}

	// =========================================================================
	// Body analysis
	// =========================================================================

	private bool AnalyzeBody(
		IMethodSymbol method,
		SemanticModel semanticModel,
		CancellationToken ct)
	{
		// Cycle guard — if we're already analysing this method, treat it as
		// pure optimistically (avoids infinite recursion on self-recursive calls).
		if (!_analysisStack.Add(method))
		{
			return true;
		}

		try
		{
			var syntax = method.DeclaringSyntaxReferences
				.FirstOrDefault()
				?.GetSyntax(ct);

			if (syntax is null)
			{
				return false;
			}

			// Resolve the semantic model for the correct syntax tree
			var model = semanticModel.Compilation == _compilation
				? semanticModel.SyntaxTree == syntax.SyntaxTree
					? semanticModel
					: _compilation.GetSemanticModel(syntax.SyntaxTree)
				: semanticModel;

			var walker = new PurityWalker(this, model, ct);
			walker.Visit(syntax);
			return !walker.HasSideEffects;
		}
		finally
		{
			_analysisStack.Remove(method);
		}
	}

	// =========================================================================
	// Syntax walker
	// =========================================================================

	private sealed class PurityWalker(
		MethodPurityAnalyzer owner,
		SemanticModel model,
		CancellationToken ct)
		: CSharpSyntaxWalker
	{
		public bool HasSideEffects { get; private set; }

		// Short-circuit: stop walking as soon as a side-effect is found
		public override void Visit(SyntaxNode? node)
		{
			if (HasSideEffects || ct.IsCancellationRequested)
			{
				return;
			}
			base.Visit(node);
		}

		// ── Assignments ──────────────────────────────────────────────────────

		public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
		{
			if (WritesToNonLocal(node.Left))
			{
				HasSideEffects = true;
				return;
			}
			base.VisitAssignmentExpression(node);
		}

		public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
		{
			if (node.IsKind(SyntaxKind.PreIncrementExpression) ||
			    node.IsKind(SyntaxKind.PreDecrementExpression))
			{
				if (WritesToNonLocal(node.Operand))
				{
					HasSideEffects = true;
					return;
				}
			}
			base.VisitPrefixUnaryExpression(node);
		}

		public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
		{
			if (node.IsKind(SyntaxKind.PostIncrementExpression) ||
			    node.IsKind(SyntaxKind.PostDecrementExpression))
			{
				if (WritesToNonLocal(node.Operand))
				{
					HasSideEffects = true;
					return;
				}
			}
			base.VisitPostfixUnaryExpression(node);
		}

		// ── Invocations ──────────────────────────────────────────────────────

		public override void VisitInvocationExpression(InvocationExpressionSyntax node)
		{
			var sym = model.GetSymbolInfo(node, ct).Symbol as IMethodSymbol;

			if (sym is not null && !owner.IsPureMethod(sym, model, ct))
			{
				HasSideEffects = true;
				return;
			}
			base.VisitInvocationExpression(node);
		}

		// ── Object creation ──────────────────────────────────────────────────
		// Creating a heap object is a side effect only if the constructor itself
		// has side effects; for value types (struct) it is always local.

		public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
		{
			var type = model.GetTypeInfo(node, ct).Type;

			if (type is { IsValueType: false })
			{
				// Check constructor purity
				var sym = model.GetSymbolInfo(node, ct).Symbol as IMethodSymbol;

				if (sym is null || !owner.IsPureMethod(sym, model, ct))
				{
					HasSideEffects = true;
					return;
				}
			}
			base.VisitObjectCreationExpression(node);
		}

		public override void VisitImplicitObjectCreationExpression(
			ImplicitObjectCreationExpressionSyntax node)
		{
			var type = model.GetTypeInfo(node, ct).Type;

			if (type is { IsValueType: false })
			{
				var sym = model.GetSymbolInfo(node, ct).Symbol as IMethodSymbol;

				if (sym is null || !owner.IsPureMethod(sym, model, ct))
				{
					HasSideEffects = true;
					return;
				}
			}
			base.VisitImplicitObjectCreationExpression(node);
		}

		// ── throw ────────────────────────────────────────────────────────────
		// A throw is technically observable, but for constant-folding purposes
		// it is acceptable: if inputs are compile-time constants the throw will
		// never be reached at runtime, and if it is it would abort execution anyway.
		// So we intentionally do NOT mark throw as a side effect here.

		// ── yield / await ────────────────────────────────────────────────────

		public override void VisitYieldStatement(YieldStatementSyntax node)
		{
			// Iterator methods produce lazy sequences — acceptable for our purposes
			// as long as the yielded values are pure.
			base.VisitYieldStatement(node);
		}

		public override void VisitAwaitExpression(AwaitExpressionSyntax node)
		{
			// Async/await implies scheduler interaction
			HasSideEffects = true;
		}

		// ── Lock / unsafe ────────────────────────────────────────────────────

		public override void VisitLockStatement(LockStatementSyntax node)
		{
			HasSideEffects = true;
		}

		public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
		{
			HasSideEffects = true;
		}

		public override void VisitFixedStatement(FixedStatementSyntax node)
		{
			HasSideEffects = true;
		}

		// ── Helpers ──────────────────────────────────────────────────────────

		private bool WritesToNonLocal(ExpressionSyntax target)
		{
			var sym = model.GetSymbolInfo(target, ct).Symbol;
			return sym switch
			{
				IFieldSymbol => true, // static or instance field write
				IPropertySymbol => true, // property setter
				IEventSymbol => true, // event add/remove
				// Local variable or parameter — fine
				ILocalSymbol => false,
				IParameterSymbol => false,
				_ => false
			};
		}
	}

	// =========================================================================
	// Utility
	// =========================================================================

	/// <summary>
	/// Produces a stable string key for whitelist/blacklist lookup:
	/// <c>Namespace.TypeName.MethodName</c> (no generic arity suffix).
	/// </summary>
	private static string GetMethodKey(IMethodSymbol method)
	{
		var typeName = method.ContainingType.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat
				.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

		// Strip generic arity for lookup, e.g. List`1 → List
		var backtick = typeName.IndexOf('`');

		if (backtick >= 0)
		{
			typeName = typeName[..backtick];
		}

		return $"{typeName}.{method.Name}";
	}
}