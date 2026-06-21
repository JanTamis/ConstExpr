using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Visitors;

/// <summary>
///   Analyzes a syntax node (method body, block, or loop) to determine whether
///   it is eligible for auto-vectorization using SIMD intrinsics.
///   A node is considered vectorizable when it contains at least one loop that:
///   <list type="bullet">
///     <item>Iterates over a numeric array or <c>Span&lt;T&gt;</c> / <c>ReadOnlySpan&lt;T&gt;</c></item>
///     <item>Works with element types that map to a SIMD vector lane (byte, short, int, long, float, double, …)</item>
///     <item>Uses only element-wise arithmetic, bitwise, or comparison operations</item>
///     <item>Has no loop-carried data dependences (no <c>arr[i-1]</c>-style cross-iteration reads)</item>
///     <item>Contains no unsupported control flow inside the hot loop (no <c>goto</c>, no <c>yield</c>)</item>
///     <item>Does not call non-vectorizable methods in the loop body</item>
///   </list>
/// </summary>
public sealed class VectorizationEligibilityVisitor : CSharpSyntaxWalker
{
	// -------------------------------------------------------------------------
	// Supported element SpecialTypes — these are the types that map directly
	// to SIMD vector lanes on all major platforms.
	// -------------------------------------------------------------------------
	private static readonly ImmutableHashSet<SpecialType> VectorizableElementTypes =
		ImmutableHashSet.Create(
			SpecialType.System_Byte,
			SpecialType.System_SByte,
			SpecialType.System_Int16,
			SpecialType.System_UInt16,
			SpecialType.System_Int32,
			SpecialType.System_UInt32,
			SpecialType.System_Int64,
			SpecialType.System_UInt64,
			SpecialType.System_Single,
			SpecialType.System_Double
		);

	// Element-type metadata names for Span / ReadOnlySpan detection
	private static readonly ImmutableHashSet<string> SpanTypeNames =
		ImmutableHashSet.Create(
			StringComparer.Ordinal,
			"System.Span`1",
			"System.ReadOnlySpan`1",
			"System.Memory`1",
			"System.ReadOnlyMemory`1"
		);

	// Intrinsic / pure math methods that have direct SIMD equivalents and
	// are therefore safe to use inside a vectorizable loop body.
	private static readonly ImmutableHashSet<string> VectorizableMathMethods =
		ImmutableHashSet.Create(
			StringComparer.Ordinal,
			// System.Math
			"System.Math.Abs",
			"System.Math.Min",
			"System.Math.Max",
			"System.Math.Clamp",
			"System.Math.Sqrt",
			"System.Math.Floor",
			"System.Math.Ceiling",
			"System.Math.Round",
			"System.Math.Truncate",
			"System.Math.Exp",
			"System.Math.Log",
			"System.Math.Log2",
			"System.Math.Log10",
			"System.Math.Pow",
			"System.Math.Sin",
			"System.Math.Cos",
			"System.Math.Tan",
			"System.Math.Sinh",
			"System.Math.Cosh",
			"System.Math.Tanh",
			"System.Math.Asin",
			"System.Math.Acos",
			"System.Math.Atan",
			"System.Math.Atan2",
			"System.Math.SinCos",
			"System.Math.CopySign",
			"System.Math.FusedMultiplyAdd",
			"System.Math.Lerp",
			// System.MathF
			"System.MathF.Abs",
			"System.MathF.Min",
			"System.MathF.Max",
			"System.MathF.Clamp",
			"System.MathF.Sqrt",
			"System.MathF.Floor",
			"System.MathF.Ceiling",
			"System.MathF.Round",
			"System.MathF.Truncate",
			"System.MathF.Exp",
			"System.MathF.Log",
			"System.MathF.Log2",
			"System.MathF.Log10",
			"System.MathF.Pow",
			"System.MathF.Sin",
			"System.MathF.Cos",
			"System.MathF.Tan",
			"System.MathF.Sinh",
			"System.MathF.Cosh",
			"System.MathF.Tanh",
			"System.MathF.Asin",
			"System.MathF.Acos",
			"System.MathF.Atan",
			"System.MathF.Atan2",
			"System.MathF.SinCos",
			"System.MathF.CopySign",
			"System.MathF.FusedMultiplyAdd",
			"System.MathF.Lerp"
		);
	private readonly CancellationToken _ct;

	// The set of loop variable names that act as a simple counter (for int i = 0; i < n; i++)
	private readonly HashSet<string> _loopCounterNames = [ ];

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	private readonly SemanticModel _model;

	private readonly List<string> _reasons = [ ];
	private readonly ConcurrentDictionary<ulong, ISymbol> _symbolStore;

	// The element type found in the innermost loop, used to select a vector width
	private SpecialType _elementType = SpecialType.None;

	// Tracks whether any vectorizable loop was found
	private bool _foundVectorizableLoop;

	// Tracks whether we are currently inside a supported loop
	private bool _insideVectorizableLoop;

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	private VectorizationEligibilityVisitor(SemanticModel model, ConcurrentDictionary<ulong, ISymbol> symbolStore, CancellationToken ct)
	{
		_model = model;
		_symbolStore = symbolStore;
		_ct = ct;
	}

	/// <summary>
	///   Analyzes the given <paramref name="node" /> and returns a
	///   <see cref="VectorizationResult" /> describing whether vectorization is applicable.
	/// </summary>
	public static VectorizationResult Analyze(SyntaxNode node, SemanticModel model, ConcurrentDictionary<ulong, ISymbol> symbolStore, CancellationToken ct = default)
	{
		var visitor = new VectorizationEligibilityVisitor(model, symbolStore, ct);
		visitor.Visit(node);
		return visitor.BuildResult();
	}

	// -------------------------------------------------------------------------
	// Loop detection
	// -------------------------------------------------------------------------

	public override void VisitForStatement(ForStatementSyntax node)
	{
		if (_ct.IsCancellationRequested)
		{
			return;
		}

		// Detect canonical counting pattern: for (var i = 0; i < n; i++) or (int i = 0; i < n; i++)
		if (!TryExtractCounterVariable(node, out var counterName))
		{
			// Not a simple counted loop — record reason and continue walking children
			// so we can still detect an inner loop that might be vectorizable.
			_reasons.Add($"Loop at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} does not follow a simple counter pattern (int i = 0; i < n; i++).");
			base.VisitForStatement(node);
			return;
		}

		_loopCounterNames.Add(counterName);
		var wasInside = _insideVectorizableLoop;
		_insideVectorizableLoop = true;

		// Analyze the body
		var bodyAnalyzer = new LoopBodyAnalyzer(_model, _loopCounterNames.ToImmutableHashSet(), VectorizableMathMethods, _symbolStore, _ct);
		bodyAnalyzer.Visit(node.Statement);

		if (bodyAnalyzer.IsVectorizable)
		{
			_foundVectorizableLoop = true;

			if (bodyAnalyzer.ElementType != SpecialType.None)
			{
				_elementType = bodyAnalyzer.ElementType;
			}
		}
		else
		{
			foreach (var reason in bodyAnalyzer.Reasons)
			{
				_reasons.Add(reason);
			}
		}

		_loopCounterNames.Remove(counterName);
		_insideVectorizableLoop = wasInside;
	}

	public override void VisitForEachStatement(ForEachStatementSyntax node)
	{
		if (_ct.IsCancellationRequested)
		{
			return;
		}

		// A foreach loop can be vectorized only when the source collection type
		// is a numeric array or Span<T> with a vectorizable element type.
		// We walk the body with a conservative analyzer that disallows index arithmetic.
		if (!_model.TryGetTypeSymbol(node.Expression, _symbolStore, out var collectionType) || !TryGetCollectionElementType(collectionType, out var elementType))
		{
			_reasons.Add($"foreach loop at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: collection element type is not a supported numeric type.");
			base.VisitForEachStatement(node);
			return;
		}

		var bodyAnalyzer = new LoopBodyAnalyzer(_model, _loopCounterNames.ToImmutableHashSet(), VectorizableMathMethods, _symbolStore, _ct, node.Identifier.Text);
		bodyAnalyzer.Visit(node.Statement);

		if (bodyAnalyzer.IsVectorizable)
		{
			_foundVectorizableLoop = true;
			_elementType = elementType;
		}
		else
		{
			foreach (var reason in bodyAnalyzer.Reasons)
			{
				_reasons.Add(reason);
			}
		}
	}

	public override void VisitWhileStatement(WhileStatementSyntax node)
	{
		// While loops are generally not auto-vectorizable without further analysis
		// of the induction variable, so we report it and keep walking.
		_reasons.Add($"while loop at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} cannot be auto-vectorized (no canonical induction variable).");
		base.VisitWhileStatement(node);
	}

	// -------------------------------------------------------------------------
	// Result construction
	// -------------------------------------------------------------------------

	private VectorizationResult BuildResult()
	{
		// if (!_foundVectorizableLoop)
		// {
		// 	if (_reasons.Count == 0)
		// 	{
		// 		_reasons.Add("No vectorizable loop pattern was found in the analyzed code.");
		// 	}
		//
		// 	return new VectorizationResult(false, VectorTypes.None, _reasons);
		// }

		var vectorType = SelectVectorType(_elementType);
		return new VectorizationResult(true, vectorType, _reasons);
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	/// <summary>
	///   Attempts to extract the name of the induction variable from a simple
	///   <c>for (int/var i = 0; i &lt; n; i++)</c> statement.
	/// </summary>
	private static bool TryExtractCounterVariable(ForStatementSyntax node, out string counterName)
	{
		counterName = String.Empty;

		// Declaration must be a single variable initialised to 0
		if (node.Declaration is not { Variables: { Count: 1 } variables })
		{
			return false;
		}

		var declarator = variables[0];

		if (declarator.Initializer?.Value is not LiteralExpressionSyntax { Token.Value: 0 })
		{
			return false;
		}

		// Condition must be a binary < or <= comparison involving the counter variable
		if (node.Condition is not BinaryExpressionSyntax condition ||
		    condition.Kind() is not (SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression))
		{
			return false;
		}

		if (condition.Left is not IdentifierNameSyntax leftId ||
		    leftId.Identifier.Text != declarator.Identifier.Text)
		{
			return false;
		}

		// Incrementor must be i++ or ++i
		if (node.Incrementors.Count != 1)
		{
			return false;
		}

		var incrementor = node.Incrementors[0];

		var isSimpleIncrement = incrementor switch
		{
			PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
			                                        && postfix.Operand is IdentifierNameSyntax pi
			                                        && pi.Identifier.Text == declarator.Identifier.Text,
			PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
			                                      && prefix.Operand is IdentifierNameSyntax pri
			                                      && pri.Identifier.Text == declarator.Identifier.Text,
			_ => false
		};

		if (!isSimpleIncrement)
		{
			return false;
		}

		counterName = declarator.Identifier.Text;
		return true;
	}

	/// <summary>
	///   Returns the <see cref="SpecialType" /> of the element stored in a numeric
	///   array or Span-like collection, or <see cref="SpecialType.None" /> when the
	///   type is not supported.
	/// </summary>
	private static bool TryGetCollectionElementType(ITypeSymbol? collectionType, out SpecialType elementType)
	{
		if (collectionType is null)
		{
			elementType = SpecialType.None;
			return false;
		}

		// T[]
		if (collectionType is IArrayTypeSymbol arrayType)
		{
			if (VectorizableElementTypes.Contains(arrayType.ElementType.SpecialType))
			{
				elementType = arrayType.ElementType.SpecialType;
				return true;
			}

			elementType = SpecialType.None;
			return false;
		}

		// Span<T> / ReadOnlySpan<T> / Memory<T> / ReadOnlyMemory<T>
		if (collectionType is INamedTypeSymbol { IsGenericType: true } namedType)
		{
			var metadataName = $"{namedType.ContainingNamespace}.{namedType.MetadataName}";

			if (SpanTypeNames.Contains(metadataName) && namedType.TypeArguments.Length == 1)
			{
				var elementSpecial = namedType.TypeArguments[0].SpecialType;
				elementType = VectorizableElementTypes.Contains(elementSpecial) ? elementSpecial : SpecialType.None;

				return elementType != SpecialType.None;
			}
		}

		elementType = SpecialType.None;
		return false;
	}

	/// <summary>
	///   Selects the widest applicable <see cref="VectorTypes" /> for the given
	///   element type.  Uses <c>Vector256</c> as the default target (widely
	///   supported on modern x64 via AVX2), stepping down when lane count would
	///   exceed 64 bytes.
	/// </summary>
	private static VectorTypes SelectVectorType(SpecialType elementType)
	{
		var byteSize = elementType switch
		{
			SpecialType.System_Byte or SpecialType.System_SByte => 1,
			SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
			SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
			SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
			_ => 4
		};

		// Pick 256-bit vectors (32 bytes / byteSize lanes) as a sensible default;
		// for 8-byte elements that gives 4 lanes which is still useful.
		// 512-bit (AVX-512) is suggested only for 1- or 2-byte elements where
		// the lane count is very high.
		return byteSize switch
		{
			1 => VectorTypes.Vector512, // 64 byte / 1 = 64 lanes
			2 => VectorTypes.Vector256, // 32 byte / 2 = 16 lanes
			4 => VectorTypes.Vector256, // 32 byte / 4 = 8 lanes (AVX2 sweet-spot)
			8 => VectorTypes.Vector256, // 32 byte / 8 = 4 lanes
			_ => VectorTypes.Vector128
		};
	}

	// =========================================================================
	// Inner class: LoopBodyAnalyzer
	// Walks a single loop body and checks all vectorization preconditions.
	// =========================================================================

	/// <summary>
	///   Walks the body of a single loop and records whether every operation
	///   inside is compatible with auto-vectorization.
	/// </summary>
	private sealed class LoopBodyAnalyzer(
		SemanticModel model,
		ImmutableHashSet<string> counterNames,
		ImmutableHashSet<string> vectorizableMathMethods,
		ConcurrentDictionary<ulong, ISymbol> symbolStore,
		CancellationToken ct,
		string? foreachElementName = null) : CSharpSyntaxWalker
	{
		private readonly CancellationToken _ct = ct;

		private readonly List<string> _reasons = [ ];
		private bool _hadViolation;

		// For foreach loops the element variable has a well-known name

		// The element type we detect from the first array/span access
		private bool _hasArrayOrSpanAccess;

		public bool IsVectorizable => _hasArrayOrSpanAccess && !_hadViolation;
		public IReadOnlyList<string> Reasons => _reasons;
		public SpecialType ElementType { get; private set; } = SpecialType.None;

		public override void Visit(SyntaxNode? node)
		{
			if (_ct.IsCancellationRequested || _hadViolation)
			{
				return;
			}

			base.Visit(node);
		}

		// ── Element access: arr[i] ────────────────────────────────────────────

		public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
		{
			if (!model.TryGetTypeSymbol(node.Expression, symbolStore, out var collectionType) || !TryGetElementType(collectionType, out var elementType))
			{
				_reasons.Add($"Element access at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} is on a non-numeric or unsupported collection type.");
				_hadViolation = true;
				return;
			}

			// Validate that the index expression is a simple counter variable or
			// a counter ± a compile-time constant (stride-1 access is fine for
			// gathers, but true dependency checks require the same index for
			// both reads and writes — for now we accept counter ± literal).
			foreach (var arg in node.ArgumentList.Arguments)
			{
				if (!IsSimpleIndexExpression(arg.Expression))
				{
					_reasons.Add($"Non-trivial index expression '{arg.Expression}' at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}. Only simple counter accesses (i, i+k, i-k) are supported.");
					_hadViolation = true;
					return;
				}
			}

			_hasArrayOrSpanAccess = true;

			if (ElementType == SpecialType.None)
			{
				ElementType = elementType;
			}
			else if (ElementType != elementType)
			{
				// Mixed element types — still vectorizable if widening is possible,
				// but we keep it conservative and report.
				_reasons.Add($"Mixed element types detected ({ElementType} vs {elementType}). Mixed-type vectorization is not supported.");
				_hadViolation = true;
				return;
			}

			base.VisitElementAccessExpression(node);
		}

		// ── foreach element variable: treat it like an array element ─────────

		public override void VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (foreachElementName is null || node.Identifier.Text != foreachElementName)
			{
				base.VisitIdentifierName(node);
				return;
			}

			// The foreach element itself — the type is the element type of the
			// collection, which was already validated by the outer visitor.
			_hasArrayOrSpanAccess = true;
			base.VisitIdentifierName(node);
		}

		// ── Supported binary operators ────────────────────────────────────────

		public override void VisitBinaryExpression(BinaryExpressionSyntax node)
		{
			if (!IsSupportedBinaryOperator(node.Kind()))
			{
				_reasons.Add($"Unsupported binary operator '{node.OperatorToken}' at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}.");
				_hadViolation = true;
				return;
			}

			base.VisitBinaryExpression(node);
		}

		// ── Method calls inside the loop ─────────────────────────────────────

		public override void VisitInvocationExpression(InvocationExpressionSyntax node)
		{
			if (!model.TryGetMethodSymbol(node, symbolStore, out var symbol))
			{
				_reasons.Add($"Unresolved method call at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}.");
				_hadViolation = true;
				return;
			}

			var fullName = $"{symbol.ContainingType?.ToDisplayString()}.{symbol.Name}";

			if (!vectorizableMathMethods.Contains(fullName))
			{
				_reasons.Add($"Method call '{fullName}' at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} does not have a known SIMD equivalent.");
				_hadViolation = true;
				return;
			}

			base.VisitInvocationExpression(node);
		}

		// ── Control flow that breaks vectorization ────────────────────────────

		public override void VisitGotoStatement(GotoStatementSyntax node)
		{
			_reasons.Add($"goto statement at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} prevents vectorization.");
			_hadViolation = true;
		}

		public override void VisitYieldStatement(YieldStatementSyntax node)
		{
			_reasons.Add($"yield statement at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} prevents vectorization.");
			_hadViolation = true;
		}

		public override void VisitAwaitExpression(AwaitExpressionSyntax node)
		{
			_reasons.Add($"await expression at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} prevents vectorization.");
			_hadViolation = true;
		}

		public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
		{
			_reasons.Add($"unsafe block at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} prevents vectorization.");
			_hadViolation = true;
		}

		public override void VisitLockStatement(LockStatementSyntax node)
		{
			_reasons.Add($"lock statement at line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1} prevents vectorization.");
			_hadViolation = true;
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		/// <summary>
		///   Returns the element <see cref="SpecialType" /> for arrays and Span-like types,
		///   or <see cref="SpecialType.None" /> for unsupported types.
		/// </summary>
		private bool TryGetElementType(ITypeSymbol? type, out SpecialType elementType)
		{
			if (type is IArrayTypeSymbol arrayType)
			{
				elementType = VectorizableElementTypes.Contains(arrayType.ElementType.SpecialType)
					? arrayType.ElementType.SpecialType
					: SpecialType.None;
				return elementType != SpecialType.None;
			}

			if (type is INamedTypeSymbol { IsGenericType: true } namedType)
			{
				var metadataName = $"{namedType.ContainingNamespace}.{namedType.MetadataName}";

				if (SpanTypeNames.Contains(metadataName) && namedType.TypeArguments.Length == 1)
				{
					var elementSpecial = namedType.TypeArguments[0].SpecialType;
					elementType = VectorizableElementTypes.Contains(elementSpecial) ? elementSpecial : SpecialType.None;

					return elementType != SpecialType.None;
				}
			}

			elementType = SpecialType.None;
			return false;
		}

		/// <summary>
		///   Returns <see langword="true" /> when the index expression is:
		///   <list type="bullet">
		///     <item>A counter variable (<c>i</c>)</item>
		///     <item>A counter ± an integer literal (<c>i + 1</c>, <c>i - 2</c>)</item>
		///     <item>An integer literal (constant access)</item>
		///   </list>
		/// </summary>
		private bool IsSimpleIndexExpression(ExpressionSyntax expression)
		{
			return expression switch
			{
				// i
				IdentifierNameSyntax id => counterNames.Contains(id.Identifier.Text),

				// 42
				LiteralExpressionSyntax lit => lit.IsKind(SyntaxKind.NumericLiteralExpression),

				// i +/- k  or  k +/- i
				BinaryExpressionSyntax bin when
					bin.IsKind(SyntaxKind.AddExpression) || bin.IsKind(SyntaxKind.SubtractExpression) =>
					IsSimpleIndexExpression(bin.Left) && bin.Right is LiteralExpressionSyntax
					|| bin.Left is LiteralExpressionSyntax && IsSimpleIndexExpression(bin.Right),

				_ => false
			};
		}

		/// <summary>
		///   Returns <see langword="true" /> when the binary operator has a direct
		///   SIMD equivalent and does not introduce cross-lane dependencies.
		/// </summary>
		private static bool IsSupportedBinaryOperator(SyntaxKind kind)
		{
			return kind switch
			{
				SyntaxKind.AddExpression => true,
				SyntaxKind.SubtractExpression => true,
				SyntaxKind.MultiplyExpression => true,
				SyntaxKind.DivideExpression => true,
				SyntaxKind.ModuloExpression => true,
				SyntaxKind.BitwiseAndExpression => true,
				SyntaxKind.BitwiseOrExpression => true,
				SyntaxKind.ExclusiveOrExpression => true,
				SyntaxKind.LeftShiftExpression => true,
				SyntaxKind.RightShiftExpression => true,
				SyntaxKind.UnsignedRightShiftExpression => true,
				SyntaxKind.LessThanExpression => true,
				SyntaxKind.LessThanOrEqualExpression => true,
				SyntaxKind.GreaterThanExpression => true,
				SyntaxKind.GreaterThanOrEqualExpression => true,
				SyntaxKind.EqualsExpression => true,
				SyntaxKind.NotEqualsExpression => true,
				_ => false
			};
		}
	}
}