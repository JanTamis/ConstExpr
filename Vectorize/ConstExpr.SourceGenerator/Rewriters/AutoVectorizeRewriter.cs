using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Auto-vectorization pass. Rewrites simple counted (<c>for (int i = 0; i &lt; src.Length; i++)</c>)
///   and <c>foreach</c> loops over numeric arrays / <c>Span&lt;T&gt;</c> / <c>ReadOnlySpan&lt;T&gt;</c>
///   into SIMD <c>System.Numerics.Vector&lt;T&gt;</c> code by extracting the loop body into a
///   <c>private static</c> helper method (registered in <c>additionalMethods</c>) and replacing the loop
///   with a call to that helper.
///   <para>
///     Two loop shapes are handled:
///     <list type="bullet">
///       <item>
///         <b>Reduction</b> — <c>acc op= f(src[i])</c> (op ∈ <c>+ * | &amp; ^</c>) or
///         <c>acc = Math.Min/Max(acc, f(src[i]))</c>, over one or more source arrays.
///       </item>
///       <item>
///         <b>Element-wise map</b> — <c>dst[i] = f(src0[i], src1[i], …)</c>.
///       </item>
///     </list>
///   </para>
///   The emitted helper is guarded by <c>Vector.IsHardwareAccelerated</c> and falls back to a scalar tail
///   loop for the remaining elements, so the transform is a no-op on hardware without SIMD support.
///   Any loop the pass cannot prove safe to vectorize is left unchanged.
/// </summary>
public sealed class AutoVectorizeRewriter : CSharpSyntaxRewriter
{
	private readonly SemanticModel _model;
	private readonly ConcurrentDictionary<ulong, ISymbol> _symbolStore;
	private readonly ISet<string?> _usings;
	private readonly IDictionary<SyntaxNode, bool> _additionalMethods;
	private readonly FastMathFlags _fastMath;
	private readonly CancellationToken _ct;

	private AutoVectorizeRewriter(SemanticModel model, ConcurrentDictionary<ulong, ISymbol> symbolStore,
		ISet<string?> usings, IDictionary<SyntaxNode, bool> additionalMethods, FastMathFlags fastMath, CancellationToken ct)
	{
		_model = model;
		_symbolStore = symbolStore;
		_usings = usings;
		_additionalMethods = additionalMethods;
		_fastMath = fastMath;
		_ct = ct;
	}

	/// <summary>
	///   Applies the auto-vectorization pass to <paramref name="node" />. Newly generated helper methods
	///   are added to <paramref name="additionalMethods" /> and the required <c>using</c> directives to
	///   <paramref name="usings" />.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, SemanticModel model, ConcurrentDictionary<ulong, ISymbol> symbolStore,
		ISet<string?> usings, IDictionary<SyntaxNode, bool> additionalMethods, FastMathFlags fastMath, CancellationToken ct = default)
	{
		return new AutoVectorizeRewriter(model, symbolStore, usings, additionalMethods, fastMath, ct).Visit(node)!;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		if (!_ct.IsCancellationRequested && TryVectorizeForEach(node, out var replacement))
		{
			return replacement;
		}

		return base.VisitForEachStatement(node);
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		if (!_ct.IsCancellationRequested && TryVectorizeFor(node, out var replacement))
		{
			return replacement;
		}

		return base.VisitForStatement(node);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// foreach (var x in src) acc op= f(x);
	// ─────────────────────────────────────────────────────────────────────────

	private bool TryVectorizeForEach(ForEachStatementSyntax node, out StatementSyntax? replacement)
	{
		replacement = null;

		if (!IsEligible(node) || !TryGetSingleExpressionStatement(node.Statement, out var expression))
		{
			return false;
		}

		// The source collection must be an array or (ReadOnly)Span of a vectorizable element type.
		if (!TryGetElementType(node.Expression, out var elementType, out var typeName)
		    || !IsReadableSpanOrArray(node.Expression))
		{
			return false;
		}

		if (!TryGetReduction(expression, out var reduction))
		{
			return false;
		}

		var elementName = node.Identifier.Text;

		// The reduction operand may only reference the loop element (and constants / type names).
		if (!OnlyReferences(reduction.Operand, [ elementName ]))
		{
			return false;
		}

		if (!IsFloatAllowed(elementType, reduction.Kind) || !LiteralsCompatible(reduction.Operand, elementType.SpecialType))
		{
			return false;
		}

		// vectorBody: element identifier → the loaded Vector<T> ("v0"); scalarBody: element identifier → "src0[i]".
		var vectorBody = RenderVector(ReplaceIdentifier(reduction.Operand, elementName, IdentifierName("v0")), elementType);
		var scalarBody = FormattingHelper.Render(ReplaceIdentifier(reduction.Operand, elementName, ParseExpression("src0[i]")));

		var method = BuildReductionMethod(reduction.Kind, typeName, [ "src0" ], "src0",
			[ ("v0", "src0") ], vectorBody, scalarBody);

		replacement = RegisterAndCall(method, reduction, [ node.Expression ]);
		return true;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// for (int i = 0; i < src.Length; i++) { acc op= f(src[i]); | dst[i] = f(src[i]); }
	// ─────────────────────────────────────────────────────────────────────────

	private bool TryVectorizeFor(ForStatementSyntax node, out StatementSyntax? replacement)
	{
		replacement = null;

		if (!IsEligible(node)
		    || !TryGetCounter(node, out var counter, out var boundArray)
		    || !TryGetSingleExpressionStatement(node.Statement, out var expression))
		{
			return false;
		}

		return TryVectorizeForReduction(expression, counter, boundArray, out replacement)
		       || TryVectorizeForMap(expression, counter, boundArray, out replacement);
	}

	private bool TryVectorizeForReduction(ExpressionStatementSyntax expression, string counter, string boundArray, out StatementSyntax? replacement)
	{
		replacement = null;

		if (!TryGetReduction(expression, out var reduction)
		    || !TryCollectSources(reduction.Operand, counter, out var sources)
		    || sources.Count == 0)
		{
			return false;
		}

		if (!TryResolveSources(sources, out var elementType, out var typeName)
		    || !IsFloatAllowed(elementType, reduction.Kind)
		    || !LiteralsCompatible(reduction.Operand, elementType.SpecialType))
		{
			return false;
		}

		// The loop bound array must be one of the reduction's source arrays.
		if (!sources.Contains(boundArray))
		{
			return false;
		}

		if (!OnlyReferences(reduction.Operand, sources.Append(counter)))
		{
			return false;
		}

		var vectors = new List<(string Vector, string Param)>();
		var vectorOperand = reduction.Operand;
		var scalarOperand = reduction.Operand;

		for (var s = 0; s < sources.Count; s++)
		{
			var param = $"src{s}";
			var vector = $"v{s}";
			vectors.Add((vector, param));
			vectorOperand = ReplaceElementAccess(vectorOperand, sources[s], counter, IdentifierName(vector));
			scalarOperand = ReplaceElementAccess(scalarOperand, sources[s], counter, ParseExpression($"{param}[i]"));
		}

		var boundParam = $"src{sources.IndexOf(boundArray)}";
		var method = BuildReductionMethod(reduction.Kind, typeName, sources.Select((_, s) => $"src{s}"), boundParam,
			vectors, RenderVector(vectorOperand, elementType), FormattingHelper.Render(scalarOperand));

		replacement = RegisterAndCall(method, reduction, sources.Select(s => (ExpressionSyntax) IdentifierName(s)));
		return true;
	}

	private bool TryVectorizeForMap(ExpressionStatementSyntax expression, string counter, string boundArray, out StatementSyntax? replacement)
	{
		replacement = null;

		// dst[i] = operand;
		if (expression.Expression is not AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.SimpleAssignmentExpression } assignment
		    || assignment.Left is not ElementAccessExpressionSyntax { Expression: IdentifierNameSyntax dstId } dstAccess
		    || !IsCounterIndex(dstAccess, counter))
		{
			return false;
		}

		var dst = dstId.Identifier.Text;
		var operand = assignment.Right;

		if (!TryCollectSources(operand, counter, out var sources) || sources.Count == 0)
		{
			return false;
		}

		// In-place map (dst is also read) is only supported when dst is the single source.
		var inPlace = sources.Contains(dst);

		if (inPlace && sources.Count > 1)
		{
			return false;
		}

		if (!TryResolveSources(sources, out var elementType, out var typeName)
		    || !IsWritableSpanOrArray(IdentifierName(dst))
		    || !IsFloatAllowed(elementType, ReductionKind.Add)
		    || !LiteralsCompatible(operand, elementType.SpecialType))
		{
			return false;
		}

		// The loop bound array must be the destination or one of the source arrays.
		if (boundArray != dst && !sources.Contains(boundArray))
		{
			return false;
		}

		if (!OnlyReferences(operand, sources.Append(counter)))
		{
			return false;
		}

		var vectors = new List<(string Vector, string Param)>();
		var vectorOperand = operand;
		var scalarOperand = operand;

		for (var s = 0; s < sources.Count; s++)
		{
			var param = inPlace ? "dst" : $"src{s}";
			var vector = $"v{s}";
			vectors.Add((vector, param));
			vectorOperand = ReplaceElementAccess(vectorOperand, sources[s], counter, IdentifierName(vector));
			scalarOperand = ReplaceElementAccess(scalarOperand, sources[s], counter, ParseExpression($"{param}[i]"));
		}

		string boundParam;
		List<string> readParams;
		List<ExpressionSyntax> callArgs;

		if (inPlace)
		{
			boundParam = "dst";
			readParams = [ ];
			callArgs = [ IdentifierName(dst) ];
		}
		else
		{
			readParams = sources.Select((_, s) => $"src{s}").ToList();
			boundParam = boundArray == dst ? "dst" : $"src{sources.IndexOf(boundArray)}";
			callArgs = sources.Select(s => (ExpressionSyntax) IdentifierName(s)).Append(IdentifierName(dst)).ToList();
		}

		var method = BuildMapMethod(typeName, readParams, inPlace, boundParam, vectors,
			RenderVector(vectorOperand, elementType), FormattingHelper.Render(scalarOperand));

		var named = RegisterMethod(method);
		replacement = ExpressionStatement(CreateCall(named, callArgs));
		return true;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Helper-method builders (string templates → parsed MethodDeclarationSyntax)
	// ─────────────────────────────────────────────────────────────────────────

	private static MethodDeclarationSyntax BuildReductionMethod(ReductionKind kind, string t, IEnumerable<string> readParams,
		string boundParam, IReadOnlyList<(string Vector, string Param)> vectors, string vectorBody, string scalarBody)
	{
		var parameters = String.Join(", ", readParams.Select(p => $"ReadOnlySpan<{t}> {p}"));
		var refs = String.Join("\n\t\t\t", vectors.Select(v => $"ref var r_{v.Param} = ref MemoryMarshal.GetReference({v.Param});").Distinct());
		var loads = String.Join("\n\t\t\t\t", vectors.Select(v => $"var {v.Vector} = Vector.LoadUnsafe(ref r_{v.Param}, (nuint)i);"));

		var vectorCombine = kind switch
		{
			ReductionKind.Add => $"acc + ({vectorBody})",
			ReductionKind.Multiply => $"acc * ({vectorBody})",
			ReductionKind.BitwiseOr => $"acc | ({vectorBody})",
			ReductionKind.BitwiseAnd => $"acc & ({vectorBody})",
			ReductionKind.ExclusiveOr => $"acc ^ ({vectorBody})",
			ReductionKind.Min => $"Vector.Min(acc, {vectorBody})",
			ReductionKind.Max => $"Vector.Max(acc, {vectorBody})",
			_ => throw new InvalidOperationException()
		};

		// Compound assignments so narrow integer results (byte/short) don't need an explicit narrowing cast.
		var scalarCombine = kind switch
		{
			ReductionKind.Add => $"result += ({scalarBody});",
			ReductionKind.Multiply => $"result *= ({scalarBody});",
			ReductionKind.BitwiseOr => $"result |= ({scalarBody});",
			ReductionKind.BitwiseAnd => $"result &= ({scalarBody});",
			ReductionKind.ExclusiveOr => $"result ^= ({scalarBody});",
			ReductionKind.Min => $"result = ({t})System.Math.Min(result, {scalarBody});",
			ReductionKind.Max => $"result = ({t})System.Math.Max(result, {scalarBody});",
			_ => throw new InvalidOperationException()
		};

		var scalarSeed = kind switch
		{
			ReductionKind.Add or ReductionKind.BitwiseOr or ReductionKind.ExclusiveOr => $"default({t})",
			ReductionKind.Multiply => $"({t})1",
			ReductionKind.BitwiseAnd => $"unchecked(({t})~default({t}))",
			ReductionKind.Min => $"{t}.MaxValue",
			ReductionKind.Max => $"{t}.MinValue",
			_ => throw new InvalidOperationException()
		};

		var vectorSeed = kind switch
		{
			ReductionKind.Add or ReductionKind.BitwiseOr or ReductionKind.ExclusiveOr => $"Vector<{t}>.Zero",
			ReductionKind.Multiply => $"Vector<{t}>.One",
			ReductionKind.BitwiseAnd => $"Vector<{t}>.AllBitsSet",
			ReductionKind.Min => $"new Vector<{t}>({t}.MaxValue)",
			ReductionKind.Max => $"new Vector<{t}>({t}.MinValue)",
			_ => throw new InvalidOperationException()
		};

		// Full statements so narrow integer results don't require an explicit narrowing cast.
		var laneCombine = kind switch
		{
			ReductionKind.Multiply => "result *= acc[lane];",
			ReductionKind.BitwiseOr => "result |= acc[lane];",
			ReductionKind.BitwiseAnd => "result &= acc[lane];",
			ReductionKind.ExclusiveOr => "result ^= acc[lane];",
			ReductionKind.Min => $"result = ({t})System.Math.Min(result, acc[lane]);",
			ReductionKind.Max => $"result = ({t})System.Math.Max(result, acc[lane]);",
			_ => "result += acc[lane];"
		};

		// Horizontal reduction of the accumulator lanes into a scalar. Vector.Sum is only available for addition.
		var horizontal = kind == ReductionKind.Add
			? "result = Vector.Sum(acc);"
			: $"result = acc[0]; for (var lane = 1; lane < Vector<{t}>.Count; lane++) {{ {laneCombine} }}";

		var source = $$"""
			private static {{t}} Reduce({{parameters}})
			{
				{{t}} result = {{scalarSeed}};
				var i = 0;

				if (Vector.IsHardwareAccelerated && {{boundParam}}.Length >= Vector<{{t}}>.Count)
				{
					{{refs}}
					var acc = {{vectorSeed}};

					for (; i <= {{boundParam}}.Length - Vector<{{t}}>.Count; i += Vector<{{t}}>.Count)
					{
						{{loads}}
						acc = {{vectorCombine}};
					}

					{{horizontal}}
				}

				for (; i < {{boundParam}}.Length; i++)
				{
					{{scalarCombine}}
				}

				return result;
			}
			""";

		return ParseHelper(source, "Reduce");
	}

	private static MethodDeclarationSyntax BuildMapMethod(string t, IReadOnlyList<string> readParams, bool inPlace,
		string boundParam, IReadOnlyList<(string Vector, string Param)> vectors, string vectorBody, string scalarBody)
	{
		var paramList = inPlace
			? $"Span<{t}> dst"
			: String.Join(", ", readParams.Select(p => $"ReadOnlySpan<{t}> {p}").Append($"Span<{t}> dst"));

		var refNames = vectors.Select(v => v.Param).Append("dst").Distinct().ToList();
		var refs = String.Join("\n\t\t\t", refNames.Select(p => $"ref var r_{p} = ref MemoryMarshal.GetReference({p});"));
		var loads = String.Join("\n\t\t\t\t", vectors.Select(v => $"var {v.Vector} = Vector.LoadUnsafe(ref r_{v.Param}, (nuint)i);"));

		var source = $$"""
			private static void Map({{paramList}})
			{
				var i = 0;

				if (Vector.IsHardwareAccelerated && {{boundParam}}.Length >= Vector<{{t}}>.Count)
				{
					{{refs}}

					for (; i <= {{boundParam}}.Length - Vector<{{t}}>.Count; i += Vector<{{t}}>.Count)
					{
						{{loads}}
						Vector.StoreUnsafe({{vectorBody}}, ref r_dst, (nuint)i);
					}
				}

				for (; i < {{boundParam}}.Length; i++)
				{
					dst[i] = {{scalarBody}};
				}
			}
			""";

		return ParseHelper(source, "Map");
	}

	private static MethodDeclarationSyntax ParseHelper(string source, string prefix)
	{
		var method = ParseMemberDeclaration(source) as MethodDeclarationSyntax
			?? throw new InvalidOperationException("Failed to parse generated vectorized method.");

		return method.WithIdentifier(Identifier($"{prefix}_{method.GetDeterministicHashString()}"));
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Registration + call-site construction
	// ─────────────────────────────────────────────────────────────────────────

	private StatementSyntax RegisterAndCall(MethodDeclarationSyntax method, Reduction reduction, IEnumerable<ExpressionSyntax> args)
	{
		var name = RegisterMethod(method);
		var call = CreateCall(name, args);

		// Rebuild the accumulator update using the helper result. e.g. acc += Helper(src)  /  acc = Math.Min(acc, Helper(src)).
		var acc = IdentifierName(reduction.AccumulatorName);

		ExpressionSyntax updated = reduction.Kind switch
		{
			ReductionKind.Min => AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, acc, InvocationMinMax("Min", reduction.AccumulatorName, call)),
			ReductionKind.Max => AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, acc, InvocationMinMax("Max", reduction.AccumulatorName, call)),
			_ => AssignmentExpression(CompoundKind(reduction.Kind), acc, call)
		};

		return ExpressionStatement(updated);
	}

	private static InvocationExpressionSyntax InvocationMinMax(string name, string acc, ExpressionSyntax call)
	{
		return InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ParseExpression("System.Math"), IdentifierName(name)))
			.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(IdentifierName(acc)), Argument(call) })));
	}

	private string RegisterMethod(MethodDeclarationSyntax method)
	{
		_usings.Add("System.Numerics");
		_usings.Add("System.Runtime.InteropServices");
		_additionalMethods[method] = false;
		return method.Identifier.Text;
	}

	private static InvocationExpressionSyntax CreateCall(string name, IEnumerable<ExpressionSyntax> args)
	{
		return InvocationExpression(IdentifierName(name))
			.WithArgumentList(ArgumentList(SeparatedList(args.Select(Argument))));
	}

	private static SyntaxKind CompoundKind(ReductionKind kind)
	{
		return kind switch
		{
			ReductionKind.Add => SyntaxKind.AddAssignmentExpression,
			ReductionKind.Multiply => SyntaxKind.MultiplyAssignmentExpression,
			ReductionKind.BitwiseOr => SyntaxKind.OrAssignmentExpression,
			ReductionKind.BitwiseAnd => SyntaxKind.AndAssignmentExpression,
			ReductionKind.ExclusiveOr => SyntaxKind.ExclusiveOrAssignmentExpression,
			_ => throw new InvalidOperationException()
		};
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Pattern extraction
	// ─────────────────────────────────────────────────────────────────────────

	private bool IsEligible(SyntaxNode loop)
	{
		return VectorizationEligibilityVisitor.Analyze(loop, _model, _symbolStore, _ct, requireLoop: true).IsVectorizable;
	}

	private static bool TryGetReduction(ExpressionStatementSyntax statement, out Reduction reduction)
	{
		reduction = default;

		switch (statement.Expression)
		{
			// acc op= operand;
			case AssignmentExpressionSyntax { Left: IdentifierNameSyntax accId } compound when TryGetCompoundKind(compound.Kind(), out var kind):
				reduction = new Reduction(kind, accId.Identifier.Text, compound.Right);
				return true;

			// acc = Math.Min/Max(acc, operand);  (or Math.Min(operand, acc))
			case AssignmentExpressionSyntax
			{
				RawKind: (int) SyntaxKind.SimpleAssignmentExpression,
				Left: IdentifierNameSyntax accId2,
				Right: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Min" or "Max" } member, ArgumentList.Arguments: { Count: 2 } minMaxArgs }
			} when IsMathType(member.Expression):
			{
				var accName = accId2.Identifier.Text;
				var kind = member.Name.Identifier.Text == "Min" ? ReductionKind.Min : ReductionKind.Max;

				if (minMaxArgs[0].Expression is IdentifierNameSyntax a0 && a0.Identifier.Text == accName)
				{
					reduction = new Reduction(kind, accName, minMaxArgs[1].Expression);
					return true;
				}

				if (minMaxArgs[1].Expression is IdentifierNameSyntax a1 && a1.Identifier.Text == accName)
				{
					reduction = new Reduction(kind, accName, minMaxArgs[0].Expression);
					return true;
				}

				return false;
			}

			default:
				return false;
		}
	}

	private static bool TryGetCompoundKind(SyntaxKind kind, out ReductionKind reductionKind)
	{
		reductionKind = kind switch
		{
			SyntaxKind.AddAssignmentExpression => ReductionKind.Add,
			SyntaxKind.MultiplyAssignmentExpression => ReductionKind.Multiply,
			SyntaxKind.OrAssignmentExpression => ReductionKind.BitwiseOr,
			SyntaxKind.AndAssignmentExpression => ReductionKind.BitwiseAnd,
			SyntaxKind.ExclusiveOrAssignmentExpression => ReductionKind.ExclusiveOr,
			_ => (ReductionKind) (-1)
		};

		return (int) reductionKind >= 0;
	}

	private static bool IsMathType(ExpressionSyntax expression)
	{
		return expression switch
		{
			IdentifierNameSyntax { Identifier.Text: "Math" or "MathF" } => true,
			MemberAccessExpressionSyntax { Name.Identifier.Text: "Math" or "MathF" } => true,
			_ => false
		};
	}

	/// <summary>
	///   Extracts the induction variable name and the array whose <c>.Length</c> bounds the loop from a
	///   canonical <c>for (int i = 0; i &lt; arr.Length; i++)</c> statement.
	/// </summary>
	private static bool TryGetCounter(ForStatementSyntax node, out string counter, out string boundArray)
	{
		counter = String.Empty;
		boundArray = String.Empty;

		if (node.Declaration is not { Variables: [ { Initializer.Value: LiteralExpressionSyntax { Token.Value: 0 } } declarator ] })
		{
			return false;
		}

		counter = declarator.Identifier.Text;

		// Condition: i < arr.Length
		if (node.Condition is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } condition
		    || condition.Left is not IdentifierNameSyntax leftId || leftId.Identifier.Text != counter
		    || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Length", Expression: IdentifierNameSyntax boundId })
		{
			return false;
		}

		boundArray = boundId.Identifier.Text;

		// Incrementor: i++ or ++i
		if (node.Incrementors is not [ var incrementor ])
		{
			return false;
		}

		return incrementor switch
		{
			PostfixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PostIncrementExpression, Operand: IdentifierNameSyntax p } => p.Identifier.Text == counter,
			PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PreIncrementExpression, Operand: IdentifierNameSyntax p } => p.Identifier.Text == counter,
			_ => false
		};
	}

	private static bool TryGetSingleExpressionStatement(StatementSyntax body, out ExpressionStatementSyntax statement)
	{
		statement = body switch
		{
			ExpressionStatementSyntax e => e,
			BlockSyntax { Statements: [ ExpressionStatementSyntax e ] } => e,
			_ => null!
		};

		return statement is not null;
	}

	/// <summary>
	///   Collects the distinct source arrays read as <c>arr[i]</c> in <paramref name="operand" />, requiring
	///   every element access to index exactly the loop counter (no <c>i ± k</c>, no gathers). Returns
	///   <see langword="false" /> when any element access uses a different index shape.
	/// </summary>
	private static bool TryCollectSources(ExpressionSyntax operand, string counter, out List<string> sources)
	{
		sources = [ ];

		foreach (var access in operand.DescendantNodesAndSelf().OfType<ElementAccessExpressionSyntax>())
		{
			if (access.Expression is not IdentifierNameSyntax arrayId || !IsCounterIndex(access, counter))
			{
				sources = [ ];
				return false;
			}

			if (!sources.Contains(arrayId.Identifier.Text))
			{
				sources.Add(arrayId.Identifier.Text);
			}
		}

		return true;
	}

	private static bool IsCounterIndex(ElementAccessExpressionSyntax access, string counter)
	{
		return access.ArgumentList.Arguments is [ { Expression: IdentifierNameSyntax indexId } ] && indexId.Identifier.Text == counter;
	}

	/// <summary>
	///   Verifies the expression only references the allowed variable names (source arrays / loop element /
	///   counter). Any other lowercase-initial identifier is treated as a captured variable and rejected —
	///   the same heuristic the LINQ vectorization path uses.
	/// </summary>
	private static bool OnlyReferences(ExpressionSyntax expression, IEnumerable<string> allowed)
	{
		var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);

		foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
		{
			var text = identifier.Identifier.Text;

			// Skip the member name of a member access (e.g. the "Sqrt" in Math.Sqrt).
			if (identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier)
			{
				continue;
			}

			if (text.Length > 0 && Char.IsLower(text[0]) && !allowedSet.Contains(text))
			{
				return false;
			}
		}

		return true;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Type resolution
	// ─────────────────────────────────────────────────────────────────────────

	private bool TryResolveSources(IReadOnlyList<string> sources, out ITypeSymbol elementType, out string typeName)
	{
		elementType = null!;
		typeName = String.Empty;

		foreach (var source in sources)
		{
			if (!TryGetElementType(IdentifierName(source), out var sourceElement, out var sourceName) || !IsReadableSpanOrArray(IdentifierName(source)))
			{
				return false;
			}

			// All sources must share the same element type.
			if (elementType is null)
			{
				elementType = sourceElement;
				typeName = sourceName;
			}
			else if (!SymbolEqualityComparer.Default.Equals(elementType, sourceElement))
			{
				return false;
			}
		}

		return elementType is not null;
	}

	private bool TryGetElementType(ExpressionSyntax expression, out ITypeSymbol elementType, out string typeName)
	{
		elementType = null!;
		typeName = String.Empty;

		if (!_model.TryGetTypeSymbol(expression, _symbolStore, out var type))
		{
			return false;
		}

		var element = type switch
		{
			IArrayTypeSymbol array => array.ElementType,
			INamedTypeSymbol { IsGenericType: true, TypeArguments: [ var arg ] } named when IsSpanType(named) => arg,
			_ => null
		};

		if (element is null || !IsSupportedElement(element))
		{
			return false;
		}

		elementType = element;
		typeName = element.ToDisplayString();
		return true;
	}

	private bool IsReadableSpanOrArray(ExpressionSyntax expression)
	{
		return _model.TryGetTypeSymbol(expression, _symbolStore, out var type) && IsArrayOrSpan(type, allowReadOnly: true);
	}

	private bool IsWritableSpanOrArray(ExpressionSyntax expression)
	{
		return _model.TryGetTypeSymbol(expression, _symbolStore, out var type) && IsArrayOrSpan(type, allowReadOnly: false);
	}

	private static bool IsArrayOrSpan(ITypeSymbol type, bool allowReadOnly)
	{
		if (type is IArrayTypeSymbol)
		{
			return true;
		}

		if (type is not INamedTypeSymbol { IsGenericType: true } named)
		{
			return false;
		}

		var metadataName = $"{named.ContainingNamespace}.{named.MetadataName}";

		return metadataName == "System.Span`1" || (allowReadOnly && metadataName == "System.ReadOnlySpan`1");
	}

	private static bool IsSpanType(INamedTypeSymbol named)
	{
		var metadataName = $"{named.ContainingNamespace}.{named.MetadataName}";
		return metadataName is "System.Span`1" or "System.ReadOnlySpan`1";
	}

	/// <summary>
	///   Floating-point loops are only vectorized when <see cref="FastMathFlags.AssociativeMath" /> is set,
	///   because SIMD reductions reorder the operations (and bitwise reductions are meaningless on floats).
	/// </summary>
	private bool IsFloatAllowed(ITypeSymbol elementType, ReductionKind kind)
	{
		var isFloat = elementType.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

		if (!isFloat)
		{
			return true;
		}

		if (kind is ReductionKind.BitwiseOr or ReductionKind.BitwiseAnd or ReductionKind.ExclusiveOr)
		{
			return false;
		}

		return _fastMath.HasFlag(FastMathFlags.AssociativeMath);
	}

	/// <summary>
	///   Element types the pass vectorizes. Restricted to the numeric types whose C# arithmetic result
	///   stays in-type (so no implicit widening to <c>int</c> that would break the generated codegen).
	///   Narrow types (byte/sbyte/short/ushort) are intentionally excluded.
	/// </summary>
	private static bool IsSupportedElement(ITypeSymbol type)
	{
		return type.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
			or SpecialType.System_Int64 or SpecialType.System_UInt64
			or SpecialType.System_Single or SpecialType.System_Double;
	}

	/// <summary>
	///   Rejects operands containing a numeric literal whose type does not match the element type.
	///   <c>VectorizerRewriter</c> lowers a literal <c>k</c> to <c>Vector.Create(k)</c> (a
	///   <c>Vector&lt;typeof(k)&gt;</c>), which would not match <c>Vector&lt;T&gt;</c> when the literal
	///   is, say, an <c>int</c> in a <c>long</c> loop. The literals 0/1/-1 are always safe because they
	///   lower to <c>Vector&lt;T&gt;.Zero</c>/<c>One</c>/<c>AllBitsSet</c>.
	/// </summary>
	private static bool LiteralsCompatible(ExpressionSyntax operand, SpecialType elementType)
	{
		foreach (var literal in operand.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
		{
			if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
			{
				continue;
			}

			var value = literal.Token.Value;

			if (IsZeroOneOrMinusOne(value))
			{
				continue;
			}

			if (LiteralSpecialType(value) != elementType)
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsZeroOneOrMinusOne(object? value)
	{
		return value switch
		{
			int i => i is 0 or 1 or -1,
			uint u => u is 0u or 1u,
			long l => l is 0L or 1L or -1L,
			ulong ul => ul is 0UL or 1UL,
			float f => f is 0f or 1f or -1f,
			double d => d is 0d or 1d or -1d,
			_ => false
		};
	}

	private static SpecialType LiteralSpecialType(object? value)
	{
		return value switch
		{
			int => SpecialType.System_Int32,
			uint => SpecialType.System_UInt32,
			long => SpecialType.System_Int64,
			ulong => SpecialType.System_UInt64,
			float => SpecialType.System_Single,
			double => SpecialType.System_Double,
			_ => SpecialType.None
		};
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Expression rewriting helpers
	// ─────────────────────────────────────────────────────────────────────────

	private string RenderVector(ExpressionSyntax expression, ITypeSymbol elementType)
	{
		var vectorized = new VectorizerRewriter(_model, elementType, _symbolStore).Visit(expression)!;
		return FormattingHelper.Render(vectorized)!;
	}

	private static ExpressionSyntax ReplaceIdentifier(ExpressionSyntax expression, string name, ExpressionSyntax replacement)
	{
		// The whole operand may itself be the identifier (e.g. `sum += x`).
		if (expression is IdentifierNameSyntax root && root.Identifier.Text == name)
		{
			return replacement;
		}

		var targets = expression.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Where(id => id.Identifier.Text == name && !(id.Parent is MemberAccessExpressionSyntax member && member.Name == id))
			.ToArray();

		return targets.Length == 0
			? expression
			: expression.ReplaceNodes(targets, (_, _) => replacement);
	}

	private static ExpressionSyntax ReplaceElementAccess(ExpressionSyntax expression, string arrayName, string counter, ExpressionSyntax replacement)
	{
		// The whole operand may itself be the element access (e.g. `sum += a[i]`).
		if (expression is ElementAccessExpressionSyntax rootAccess
		    && rootAccess.Expression is IdentifierNameSyntax rootId && rootId.Identifier.Text == arrayName
		    && IsCounterIndex(rootAccess, counter))
		{
			return replacement;
		}

		var targets = expression.DescendantNodesAndSelf()
			.OfType<ElementAccessExpressionSyntax>()
			.Where(access => access.Expression is IdentifierNameSyntax id && id.Identifier.Text == arrayName && IsCounterIndex(access, counter))
			.ToArray();

		return targets.Length == 0
			? expression
			: expression.ReplaceNodes(targets, (_, _) => replacement);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Nested types
	// ─────────────────────────────────────────────────────────────────────────

	private enum ReductionKind
	{
		Add,
		Multiply,
		BitwiseOr,
		BitwiseAnd,
		ExclusiveOr,
		Min,
		Max
	}

	private readonly struct Reduction(ReductionKind kind, string accumulatorName, ExpressionSyntax operand)
	{
		public ReductionKind Kind { get; } = kind;
		public string AccumulatorName { get; } = accumulatorName;
		public ExpressionSyntax Operand { get; } = operand;
	}
}
