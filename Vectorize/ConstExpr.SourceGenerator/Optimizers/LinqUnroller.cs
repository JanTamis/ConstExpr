using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;
using ConstExpr.SourceGenerator.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers;

public static class LinqUnroller
{
	private static readonly Dictionary<PossibleLinqMethod, BaseLinqUnroller> Unrollers = new()
	{
		{ PossibleLinqMethod.Where, new WhereLinqUnroller() },
		{ PossibleLinqMethod.Select, new SelectLinqUnroller() },
		{ PossibleLinqMethod.Cast, new CastLinqUnroller() },
		{ PossibleLinqMethod.OfType, new OfTypeLinqUnroller() },
		{ PossibleLinqMethod.Contains, new ContainsLinqUnroller() },
		{ PossibleLinqMethod.Sum, new SumLinqUnroller() },
		{ PossibleLinqMethod.Count, new CountLinqUnrolled() },
		{ PossibleLinqMethod.Any, new AnyLinqUnroller() },
		{ PossibleLinqMethod.All, new AllLinqUnroller() },
		{ PossibleLinqMethod.Average, new AverageLinqUnroller() },
		{ PossibleLinqMethod.Aggregate, new AggregateLinqUnroller() },
		{ PossibleLinqMethod.Distinct, new DistinctLinqUnroller() },
		{ PossibleLinqMethod.DistinctBy, new DistinctByLinqUnroller() },
		{ PossibleLinqMethod.First, new FirstLinqUnroller() },
		{ PossibleLinqMethod.FirstOrDefault, new FirstOrDefaultLinqUnroller() },
		{ PossibleLinqMethod.Last, new LastLinqUnroller() },
		{ PossibleLinqMethod.LastOrDefault, new LastOrDefaultLinqUnroller() },
		{ PossibleLinqMethod.Single, new SingleLinqUnroller() },
		{ PossibleLinqMethod.SingleOrDefault, new SingleOrDefaultLinqUnroller() },
		{ PossibleLinqMethod.Min, new MinLinqUnroller() },
		{ PossibleLinqMethod.Max, new MaxLinqUnroller() },
		{ PossibleLinqMethod.MinBy, new MinByLinqUnroller() },
		{ PossibleLinqMethod.MaxBy, new MaxByLinqUnroller() },
		{ PossibleLinqMethod.Skip, new SkipLinqUnroller() },
		{ PossibleLinqMethod.SkipWhile, new SkipWhileLinqUnroller() },
		{ PossibleLinqMethod.Take, new TakeLinqUnroller() },
		{ PossibleLinqMethod.TakeWhile, new TakeWhileLinqUnroller() },
		{ PossibleLinqMethod.LongCount, new LongCountLinqUnroller() },
		{ PossibleLinqMethod.ElementAt, new ElementAtLinqUnroller() },
		{ PossibleLinqMethod.ElementAtOrDefault, new ElementAtOrDefaultLinqUnroller() },
		{ PossibleLinqMethod.Except, new ExceptLinqUnroller() },
		{ PossibleLinqMethod.ExceptBy, new ExceptByLinqUnroller() },
		{ PossibleLinqMethod.Intersect, new IntersectLinqUnroller() },
		{ PossibleLinqMethod.IntersectBy, new IntersectByLinqUnroller() },
		{ PossibleLinqMethod.SequenceEqual, new SequenceEqualLinqUnroller() },
		{ PossibleLinqMethod.Append, new AppendLinqUnroller() },
		{ PossibleLinqMethod.Prepend, new PrependLinqUnroller() },
		{ PossibleLinqMethod.Concat, new ConcatLinqUnroller() },
		{ PossibleLinqMethod.Union, new UnionLinqUnroller() },
		{ PossibleLinqMethod.UnionBy, new UnionByLinqUnroller() },
		{ PossibleLinqMethod.DefaultIfEmpty, new DefaultIfEmptyLinqUnroller() },
		{ PossibleLinqMethod.SelectMany, new SelectManyLinqUnroller() },
		{ PossibleLinqMethod.SkipLast, new SkipLastLinqUnroller() },
		{ PossibleLinqMethod.TakeLast, new TakeLastLinqUnroller() },
		{ PossibleLinqMethod.Reverse, new ReverseLinqUnroller() },
		{ PossibleLinqMethod.Zip, new ZipLinqUnroller() },
		{ PossibleLinqMethod.Order, new OrderLinqUnroller() },
		{ PossibleLinqMethod.OrderDescending, new OrderDescendingLinqUnroller() },
		{ PossibleLinqMethod.OrderBy, new OrderByLinqUnroller() },
		{ PossibleLinqMethod.OrderByDescending, new OrderByDescendingLinqUnroller() },
		{ PossibleLinqMethod.Index, new IndexLinqUnroller() },
		{ PossibleLinqMethod.Chunk, new ChunkLinqUnroller() },
		{ PossibleLinqMethod.GroupBy, new GroupByLinqUnroller() },
		{ PossibleLinqMethod.ToDictionary, new ToDictionaryLinqUnroller() },
		{ PossibleLinqMethod.ToHashSet, new ToHashSetLinqUnroller() },
		{ PossibleLinqMethod.ToLookup, new ToLookupLinqUnroller() },
		{ PossibleLinqMethod.AggregateBy, new AggregateByLinqUnroller() },
		{ PossibleLinqMethod.CountBy, new CountByLinqUnroller() },
		{ PossibleLinqMethod.Join, new JoinLinqUnroller() },
		{ PossibleLinqMethod.GroupJoin, new GroupJoinLinqUnroller() },
		{ PossibleLinqMethod.LeftJoin, new LeftJoinLinqUnroller() },
		{ PossibleLinqMethod.RightJoin, new RightJoinLinqUnroller() },
		{ PossibleLinqMethod.ThenBy, new ThenByLinqUnroller() },
		{ PossibleLinqMethod.ThenByDescending, new ThenByDescendingLinqUnroller() }
	};

	/// <summary>
	///   Intermediate steps that emit exactly one output element per input element. When a chain
	///   contains only these between source and terminal, the iteration count equals the source size.
	///   Conservative: a missing entry only costs an optimization; a wrong entry produces wrong results.
	/// </summary>
	private static readonly HashSet<PossibleLinqMethod> CountPreservingMethods =
	[
		PossibleLinqMethod.Select,
		PossibleLinqMethod.Cast,
		PossibleLinqMethod.Index,
		PossibleLinqMethod.Reverse,
		PossibleLinqMethod.Order,
		PossibleLinqMethod.OrderBy,
		PossibleLinqMethod.OrderByDescending,
		PossibleLinqMethod.OrderDescending,
		PossibleLinqMethod.ThenBy,
		PossibleLinqMethod.ThenByDescending,
		PossibleLinqMethod.ToList,
		PossibleLinqMethod.ToArray,
		PossibleLinqMethod.AsEnumerable,
	];

	/// <summary>
	///   Walks a LINQ method-chain rooted at <paramref name="node" /> and returns every
	///   recognised <see cref="PossibleLinqMethod" /> step together with its argument
	///   expressions, ordered from the first (innermost) call to the last (outermost).
	///   Stops as soon as a call with an unrecognised method name is encountered.
	/// </summary>
	public static UnrolledLinqMethod[] ParseLinqChain(SemanticModel model, Func<SyntaxNode?, SyntaxNode?> visit, SyntaxNode node, ConcurrentDictionary<ulong, ISymbol> symbolStore)
	{
		var methods = new List<UnrolledLinqMethod>();
		var current = node as ExpressionSyntax;

		// Each iteration peels off one layer: outerCall.Method(args)
		while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
		{
			if (!Enum.TryParse<PossibleLinqMethod>(memberAccess.Name.Identifier.Text, out var linqMethod))
			{
				return [ ];
			}

			var parameters = invocation.ArgumentList.Arguments
				.Select(arg => arg.Expression)
				.ToArray();

			var typeArguments = memberAccess.Name is GenericNameSyntax genericName
				? genericName.TypeArgumentList.Arguments.ToArray()
				: [ ];

			// check if memberAccess.Expression is not a Type e.g Int32 or Int64
			if (memberAccess.Expression is IdentifierNameSyntax identifier
			    && Type.GetType($"System.{identifier.Identifier.Text}") != null)
			{
				return [ ];
			}

			if (!model.TryGetSymbol<IMethodSymbol>(invocation, symbolStore, out var method))
			{
				// Fallback: try to resolve from Compilation for optimized/synthetic nodes
				method = TryResolveLinqMethodFromCompilation(
					model.Compilation,
					memberAccess.Name.Identifier.Text,
					invocation.ArgumentList.Arguments.Count + 1);

				if (method is null)
				{
					return [ ];
				}
			}

			methods.Add(new UnrolledLinqMethod(model, linqMethod, method, visit, parameters, typeArguments));

			// Move inward to the receiver of this call
			current = memberAccess.Expression;
		}

		// Append the source expression last (before reversal) so it becomes element 0.
		if (current is not null)
		{
			methods.Add(new UnrolledLinqMethod(model, PossibleLinqMethod.Source, null!, visit, [ current ], [ ]));
		}

		// We collected from outermost → innermost; reverse so callers get call order.
		methods.Reverse();

		if (IsResultMethod(methods[^1].Method))
		{
			return methods.ToArray();
		}

		return [ ];
	}

	/// <summary>
	///   Attempts to resolve a LINQ extension method symbol from the compilation
	///   when the semantic model cannot resolve it (e.g., for synthetic/optimized nodes).
	/// </summary>
	private static IMethodSymbol? TryResolveLinqMethodFromCompilation(Compilation compilation, string methodName, int parameterCount)
	{
		var enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable");

		return enumerable?.GetMembers(methodName)
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == parameterCount);
	}

	public static bool TryUnrollLinqChain(SyntaxNode node, Func<SyntaxNode?, SyntaxNode?> visit, SemanticModel model, IDictionary<SyntaxNode, bool> additionalMethods, ConcurrentDictionary<ulong, ISymbol> symbolStore, [NotNullWhen(true)] out SyntaxNode? result, IDictionary<string, VariableItem>? variables = null)
	{
		var chain = ParseLinqChain(model, visit, node, symbolStore);

		// A chain of [Source, TerminalMethod] (length 2) is already well-handled
		// by the FunctionOptimizers — only unroll when intermediate steps exist.
		if (chain.Length < 2
		    || !model.TryGetTypeSymbol(chain[0].Parameters[0], symbolStore, out var type))
		{
			result = null;
			return false;
		}

		for (var i = 0; i < chain.Length; i++)
		{
			chain[i].CollectionType = type;
		}

		// Mark the terminal step when every intermediate step is 1:1, so the iteration count
		// equals the source size and terminals like Average can divide by collection.Length/Count
		// instead of counting in the loop. Excluded for constant sources: there the count-variable
		// path constant-folds away entirely (the rewriter unrolls count++), which beats a .Length call.
		chain[^1].IsCountPreserved = !IsConstantCollectionArg(chain[0].Parameters[0], variables)
		                             && chain.Skip(1).Take(chain.Length - 2).All(s => CountPreservingMethods.Contains(s.Method));

		if (!Unrollers.TryGetValue(chain[^1].Method, out var lastUnroller))
		{
			result = null;
			return false;
		}

		const string collectionName = "collection";

		var elementName = lastUnroller.GetCollectionElement(chain[^1], collectionName);
		var elements = new List<StatementSyntax>();

		ParseLoopBody(chain, elements, ref elementName, visit);

		// under loop
		var statements = ParseAboveLoop(chain);

		ParseBeforeMainLoop(chain, statements, visit);

		lastUnroller.CreateLoop(chain[^1], type, elements, collectionName, statements);

		ParseAfterMainLoop(chain, statements, visit);

		ParsePossibleReturnStatement(chain, statements);

		// create localmethod with statements
		var parameterType = model.TryGetTypeSymbol(chain[0].Parameters[0], symbolStore, out var returnType)
			? returnType.GetTypeSyntax(false)
			: PredefinedType(Token(SyntaxKind.ObjectKeyword));

		var body = Block(statements);
		var visitedBody = visit(body) as BlockSyntax ?? body;

		// Detect identifiers used in the visited body that are not declared within it
		// (captured outer variables from lambdas, e.g. `mean` in `x => (x - mean) * (x - mean)`)
		var capturedVars = FindCapturedVariables(visitedBody, collectionName, chain, model);

		// Optimisation: when the collection argument is a fully constant literal array and there are
		// no un-resolved captured variables, substitute the collection into the body and let the
		// partial rewriter evaluate the loop at compile time — no helper method is needed.
		if (capturedVars.Count == 0
		    && TryEvaluateWithConstantCollection(visitedBody, collectionName, chain[0].Parameters[0], visit, variables, out var inlined))
		{
			result = inlined;
			return true;
		}

		// Build parameter list: collection + any captured outer variables
		var methodParameters = new List<ParameterSyntax>
		{
			Parameter(Identifier(collectionName)).WithType(parameterType)
		};

		foreach (var (varName, varType) in capturedVars)
		{
			methodParameters.Add(Parameter(Identifier(varName)).WithType(varType));
		}

		var localMethod = MethodDeclaration(chain[^1].MethodSymbol.ReturnType.GetTypeSyntax(false), Identifier($"{chain[^1].Method}_{body.GetDeterministicHashString()}"))
			.WithParameterList(ParameterList(SeparatedList(methodParameters)))
			.AddModifiers(Token(SyntaxKind.PrivateKeyword))
			.AddModifiers(Token(SyntaxKind.StaticKeyword))
			.WithBody(visitedBody);

		additionalMethods.TryAdd(localMethod, true);

		// Build argument list: collection + any captured outer variables
		var callArguments = new List<ArgumentSyntax>
		{
			Argument(chain[0].Parameters[0])
		};

		foreach (var (varName, _) in capturedVars)
		{
			callArguments.Add(Argument(IdentifierName(varName)));
		}

		result = InvocationExpression(IdentifierName(localMethod.Identifier))
			.WithArgumentList(ArgumentList(SeparatedList(callArguments)));
		return true;
	}

	/// <summary>
	///   Attempts to evaluate a LINQ helper body at compile time by substituting a constant
	///   collection argument for the <paramref name="collectionParam" /> placeholder.
	///   Succeeds only when the partial rewriter can fully reduce the body to a literal return value.
	///   When <paramref name="variables" /> is provided, the rewriter's variable state is fully
	///   snapshotted before the inner evaluation and restored afterwards, preventing pollution
	///   of the outer constant-folding context.
	/// </summary>
	private static bool TryEvaluateWithConstantCollection(
		BlockSyntax visitedBody, string collectionParam, ExpressionSyntax collectionArg,
		Func<SyntaxNode?, SyntaxNode?> visit, IDictionary<string, VariableItem>? variables,
		[NotNullWhen(true)] out SyntaxNode? result)
	{
		// Precondition: this inline path is only valid for a *constant* collection. For an opaque
		// runtime source (e.g. a method parameter) the loop unrolls to zero iterations and reduction
		// terminals collapse to their empty-sequence literal (All=>true, Any=>false, Count=>0, Sum=>0).
		// That literal passes the no-identifiers gate below and silently replaces the real computation.
		// Bail unless the source is a known-constant collection so the helper-method path emits a real loop.
		if (!IsConstantCollectionArg(collectionArg, variables))
		{
			result = null;
			return false;
		}

		// Substitute every use of the collection parameter with the actual argument expression
		var substituted = visitedBody.ReplaceNodes(
			visitedBody.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(id => id.Identifier.Text == collectionParam
				             && !(id.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Span == id.Span)),
			(_, _) => collectionArg);

		// Snapshot the outer variable state so the inner evaluation cannot pollute it.
		var snapshot = variables is not null ? SnapshotVariables(variables) : null;

		// Run the partial rewriter — for a constant CollectionExpression it will unroll the
		// foreach and fold all arithmetic, collapsing the block to `return <literal>`.
		var evaluated = visit(substituted) as BlockSyntax;

		// Restore the outer variable state regardless of whether evaluation succeeded.
		if (snapshot is not null)
		{
			RestoreVariables(variables!, snapshot);
		}

		if (evaluated is null)
		{
			result = null;
			return false;
		}

		// Accept when the last statement is `return <literal>` — the expression must be fully
		// reduced to a constant with no remaining identifier references.  Preceding statements
		// may be dead declarations / increment side-effects from constant-folded loop unrolling.
		if (evaluated.Statements.Count > 0
		    && evaluated.Statements[^1] is ReturnStatementSyntax { Expression: { } retExpr }
		    && !retExpr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any())
		{
			result = retExpr;
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	///   True when <paramref name="arg" /> is a compile-time-known collection (a literal, a collection
	///   expression / array creation, or an identifier tracked with a constant value). An opaque runtime
	///   source — a method parameter or any untracked identifier — returns false, which keeps the inline
	///   constant-fold from treating it as an empty sequence. An empty *constant* collection is fine: its
	///   empty-sequence result is genuinely correct.
	/// </summary>
	private static bool IsConstantCollectionArg(ExpressionSyntax arg, IDictionary<string, VariableItem>? variables)
	{
		return arg switch
		{
			LiteralExpressionSyntax => true,
			CollectionExpressionSyntax => true,
			ArrayCreationExpressionSyntax => true,
			ImplicitArrayCreationExpressionSyntax => true,
			ParenthesizedExpressionSyntax paren => IsConstantCollectionArg(paren.Expression, variables),
			IdentifierNameSyntax id => variables is not null
			                           && variables.TryGetValue(id.Identifier.Text, out var v) && v.HasValue,
			_ => false
		};
	}

	/// <summary>Takes a full snapshot of the variable tracking dictionary.</summary>
	private static Dictionary<string, VariableSnapshot> SnapshotVariables(IDictionary<string, VariableItem> variables)
	{
		var snap = new Dictionary<string, VariableSnapshot>(StringComparer.Ordinal);

		foreach (var kvp in variables)
		{
			snap[kvp.Key] = new VariableSnapshot
			{
				Value = kvp.Value.Value,
				HasValue = kvp.Value.HasValue,
				IsAltered = kvp.Value.IsAltered,
				IsInitialized = kvp.Value.IsInitialized
			};
		}
		return snap;
	}

	/// <summary>
	///   Restores the variable tracking dictionary to the state captured by
	///   <see cref="SnapshotVariables" />, removing any variables that were added
	///   during the inner evaluation.
	/// </summary>
	private static void RestoreVariables(IDictionary<string, VariableItem> variables, Dictionary<string, VariableSnapshot> snapshot)
	{
		// Remove variables that were added during the inner evaluation
		foreach (var key in variables.Keys.Where(k => !snapshot.ContainsKey(k)).ToList())
		{
			variables.Remove(key);
		}

		// Restore the state of variables that existed before the inner evaluation
		foreach (var kvp in snapshot)
		{
			if (variables.TryGetValue(kvp.Key, out var item))
			{
				item.Value = kvp.Value.Value;
				item.HasValue = kvp.Value.HasValue;
				item.IsAltered = kvp.Value.IsAltered;
				item.IsInitialized = kvp.Value.IsInitialized;
			}
		}
	}

	/// <summary>
	///   Finds identifiers used in <paramref name="visitedBody" /> that are not locally declared
	///   within it (captured outer-scope variables). Returns them paired with their inferred type.
	/// </summary>
	private static List<(string Name, TypeSyntax Type)> FindCapturedVariables(
		BlockSyntax visitedBody, string collectionParam, UnrolledLinqMethod[] chain, SemanticModel model)
	{
		// Collect names that are locally defined in the body
		var localNames = new HashSet<string>(StringComparer.Ordinal) { collectionParam };

		foreach (var varDecl in visitedBody.DescendantNodes().OfType<VariableDeclaratorSyntax>())
		{
			localNames.Add(varDecl.Identifier.Text);
		}

		foreach (var forEach in visitedBody.DescendantNodes().OfType<ForEachStatementSyntax>())
		{
			localNames.Add(forEach.Identifier.Text);
		}

		// Any identifier in the body that is not locally declared is a captured outer variable.
		// Exclude identifiers in type position (e.g. `var`, `double`, `int` used as type names)
		// and identifiers used as member/method names (right-hand side of a `.`).
		var capturedNames = visitedBody.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(static id => !IsTypeOrMemberNameIdentifier(id))
			.Select(static id => id.Identifier.Text)
			.Where(name => !localNames.Contains(name))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (capturedNames.Count == 0)
		{
			return [ ];
		}

		var result = new List<(string Name, TypeSyntax Type)>();

		foreach (var capturedName in capturedNames)
		{
			TypeSyntax? typeSyntax = null;
			var isTypeName = false;

			// Look through original lambda expressions in the chain — the semantic model can resolve
			// identifiers there, giving us both the symbol kind (type vs. variable) and the value type.
			foreach (var step in chain)
			{
				foreach (var param in step.Parameters)
				{
					var originalId = param.DescendantNodesAndSelf()
						.OfType<IdentifierNameSyntax>()
						.FirstOrDefault(id => id.Identifier.Text == capturedName);

					if (originalId is null) continue;

					// If the identifier resolves to a type symbol it is a type name (e.g. Double, Char, Math)
					// — NOT a captured variable.
					var symbolInfo = model.GetSymbolInfo(originalId);

					if (symbolInfo.Symbol is ITypeSymbol)
					{
						isTypeName = true;
						break;
					}

					var typeInfo = model.GetTypeInfo(originalId);

					if (typeInfo.Type is ITypeSymbol typeSymbol)
					{
						typeSyntax = typeSymbol.GetTypeSyntax(false);
						break;
					}
				}

				if (isTypeName || typeSyntax != null) break;
			}

			// Skip type names and identifiers whose value type could not be determined.
			// (Unknown identifiers are likely injected by the unroller template and should not
			//  be passed as extra parameters.)
			if (isTypeName || typeSyntax is null) continue;

			result.Add((capturedName, typeSyntax));
		}

		return result;
	}

	/// <summary>
	///   Returns true for identifiers that should not be treated as captured outer variables:
	///   type-position identifiers (e.g. `var` in `var x = ...` or `foreach (var item in ...)`)
	///   and member/method names on the right side of a dot (e.g. `Append` in `result.Append(...)`).
	/// </summary>
	private static bool IsTypeOrMemberNameIdentifier(IdentifierNameSyntax id)
	{
		return id.Parent switch
		{
			// `var x = expr` or `int x = expr` — the type annotation, not a reference
			VariableDeclarationSyntax decl when decl.Type == id => true,
			// `foreach (var item in ...)` — the element type annotation
			ForEachStatementSyntax forEach when forEach.Type == id => true,
			// Parameter type annotation
			ParameterSyntax param when param.Type == id => true,
			// `receiver.Member(...)` — exclude the member/method name, not the receiver
			MemberAccessExpressionSyntax ma when ma.Name.Span == id.Span => true,
			// Cast or type-of: `(Double)x`, `typeof(Double)` — exclude the type name
			CastExpressionSyntax cast when cast.Type == id => true,
			TypeOfExpressionSyntax typeOf when typeOf.Type == id => true,
			_ => false
		};
	}

	private static void ParsePossibleReturnStatement(UnrolledLinqMethod[] chain, List<StatementSyntax> statements)
	{
		if (Unrollers.TryGetValue(chain[^1].Method, out var unroller))
		{
			unroller.UnrollUnderLoop(chain[^1], statements);
		}
	}

	/// <summary>
	///   Calls <see cref="BaseLinqUnroller.UnrollBeforeMainLoop" /> for each chain step,
	///   passing a partial loop body that starts from the step after the current one.
	///   Used by Prepend to add elements before the main loop.
	/// </summary>
	private static void ParseBeforeMainLoop(UnrolledLinqMethod[] chain, List<StatementSyntax> resultStatements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		for (var i = 1; i < chain.Length; i++)
		{
			if (Unrollers.TryGetValue(chain[i].Method, out var unroller))
			{
				var partialBody = BuildPartialLoopBody(chain, i + 1, visit);
				unroller.UnrollBeforeMainLoop(chain[i], partialBody, resultStatements);
			}
		}
	}

	/// <summary>
	///   Calls <see cref="BaseLinqUnroller.UnrollAfterMainLoop" /> for each chain step,
	///   passing a partial loop body that starts from the step after the current one.
	///   Used by Append, Concat, Union, and DefaultIfEmpty to add elements after the main loop.
	/// </summary>
	private static void ParseAfterMainLoop(UnrolledLinqMethod[] chain, List<StatementSyntax> resultStatements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		for (var i = 1; i < chain.Length; i++)
		{
			if (Unrollers.TryGetValue(chain[i].Method, out var unroller))
			{
				var partialBody = BuildPartialLoopBody(chain, i + 1, visit);
				unroller.UnrollAfterMainLoop(chain[i], partialBody, resultStatements);
			}
		}
	}

	/// <summary>
	///   Builds a partial loop body by replaying <see cref="BaseLinqUnroller.UnrollLoopBody" />
	///   for chain steps starting from <paramref name="fromIndex" />. The element name starts
	///   as <c>item</c> (matching the foreach loop variable in extra iterations).
	/// </summary>
	internal static List<StatementSyntax> BuildPartialLoopBody(UnrolledLinqMethod[] chain, int fromIndex, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		ExpressionSyntax elementName = IdentifierName("item");
		var elements = new List<StatementSyntax>();

		for (var i = fromIndex; i < chain.Length; i++)
		{
			if (i < chain.Length - 1
			    && chain[i].Method is PossibleLinqMethod.ToList or PossibleLinqMethod.ToArray or PossibleLinqMethod.AsEnumerable)
			{
				continue;
			}

			if (Unrollers.TryGetValue(chain[i].Method, out var unroller))
			{
				unroller.UnrollLoopBody(chain[i], elements, ref elementName);
			}
		}

		var combined = ConstExprPartialRewriter.CombineConsecutiveIfStatements(List(elements), visit);
		elements.Clear();
		elements.AddRange(combined);
		return elements;
	}

	private static List<StatementSyntax> ParseAboveLoop(UnrolledLinqMethod[] chain)
	{
		var statements = new List<StatementSyntax>();

		foreach (var chainMethod in chain)
		{
			if (Unrollers.TryGetValue(chainMethod.Method, out var unroller))
			{
				unroller.UnrollAboveLoop(chainMethod, statements);
			}
		}

		return statements;
	}

	private static void ParseLoopBody(UnrolledLinqMethod[] chain, List<StatementSyntax> elements, ref ExpressionSyntax elementName, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		for (var i = 1; i < chain.Length; i++)
		{
			if (i < chain.Length - 1
			    && chain[i].Method is PossibleLinqMethod.ToList or PossibleLinqMethod.ToArray or PossibleLinqMethod.AsEnumerable)
			{
				continue;
			}

			if (Unrollers.TryGetValue(chain[i].Method, out var unroller))
			{
				unroller.UnrollLoopBody(chain[i], elements, ref elementName);
			}
			else
			{
				elements.Add(YieldStatement(SyntaxKind.YieldReturnStatement, elementName));
			}

			if (elements[^1] is ContinueStatementSyntax)
			{
				elements.RemoveAt(elements.Count - 1);
				break;
			}

			if (elements.Any(a => a is BreakStatementSyntax or ReturnStatementSyntax))
			{
				break;
			}
		}

		// Combine consecutive if-statements with identical jump bodies into a single if using ||.
		// e.g. if (!set.Add(x)) continue; if (x == 1) continue; => if (!set.Add(x) || x == 1) continue;
		var combined = ConstExprPartialRewriter.CombineConsecutiveIfStatements(List(elements), visit);
		elements.Clear();
		elements.AddRange(combined);
	}

	private static bool IsResultMethod(PossibleLinqMethod method)
	{
		return method is PossibleLinqMethod.Aggregate
			or PossibleLinqMethod.Average
			or PossibleLinqMethod.Count
			or PossibleLinqMethod.LongCount
			or PossibleLinqMethod.ElementAt
			or PossibleLinqMethod.ElementAtOrDefault
			or PossibleLinqMethod.Max
			or PossibleLinqMethod.MaxBy
			or PossibleLinqMethod.Min
			or PossibleLinqMethod.MinBy
			or PossibleLinqMethod.Sum
			or PossibleLinqMethod.Single
			or PossibleLinqMethod.SingleOrDefault
			or PossibleLinqMethod.First
			or PossibleLinqMethod.FirstOrDefault
			or PossibleLinqMethod.Last
			or PossibleLinqMethod.LastOrDefault
			or PossibleLinqMethod.All
			or PossibleLinqMethod.Any
			or PossibleLinqMethod.Contains
			or PossibleLinqMethod.SequenceEqual
			or PossibleLinqMethod.ToDictionary
			or PossibleLinqMethod.ToHashSet
			or PossibleLinqMethod.ToLookup;
	}

	private struct VariableSnapshot
	{
		public object? Value;
		public bool HasValue;
		public bool IsAltered;
		public bool IsInitialized;
	}
}

public enum PossibleLinqMethod
{
	/// <summary>The source collection expression that starts the LINQ chain.</summary>
	Source,
	Aggregate,
	AggregateBy,
	All,
	Any,
	Append,
	AsEnumerable,
	Average,
	Cast,
	Chunk,
	Concat,
	Contains,
	Count,
	CountBy,
	DefaultIfEmpty,
	Distinct,
	DistinctBy,
	ElementAt,
	ElementAtOrDefault,
	Except,
	ExceptBy,
	First,
	FirstOrDefault,
	GroupBy,
	GroupJoin,
	Index,
	InfiniteSequence,
	Intersect,
	IntersectBy,
	Join,
	Last,
	LastOrDefault,
	LeftJoin,
	LongCount,
	Max,
	MaxBy,
	Min,
	MinBy,
	OfType,
	Order,
	OrderBy,
	OrderByDescending,
	OrderDescending,
	Prepend,
	Reverse,
	RightJoin,
	Select,
	SelectMany,
	SequenceEqual,
	Single,
	SingleOrDefault,
	Skip,
	SkipLast,
	SkipWhile,
	Sum,
	Take,
	TakeLast,
	TakeWhile,
	ThenBy,
	ThenByDescending,
	ToArray,
	ToDictionary,
	ToHashSet,
	ToList,
	ToLookup,
	TryGetNonEnumeratedCount,
	Union,
	UnionBy,
	Where,
	Zip
}

public struct UnrolledLinqMethod(SemanticModel model, PossibleLinqMethod method, IMethodSymbol methodSymbol, Func<SyntaxNode?, SyntaxNode?> visit, ExpressionSyntax[] parameters, TypeSyntax[] typeArguments)
{
	public override string ToString()
	{
		var typeArgs = TypeArguments.Length > 0
			? $"<{String.Join(", ", TypeArguments.Select(t => t.ToString()))}>"
			: String.Empty;
		return $"{Method}{typeArgs}({String.Join(", ", Parameters.Select(p => p.ToString()))})";
	}

	public SemanticModel Model { get; } = model;
	public PossibleLinqMethod Method { get; set; } = method;
	public IMethodSymbol MethodSymbol { get; set; } = methodSymbol;
	public ITypeSymbol CollectionType { get; set; }

	/// <summary>
	///   True when every intermediate chain step between the source and this terminal step is
	///   1:1 (preserves element count), so the source collection's size equals the iteration count.
	///   Set on the terminal step by <see cref="LinqUnroller.TryUnrollLinqChain" />.
	/// </summary>
	public bool IsCountPreserved { get; set; }

	public Func<SyntaxNode?, SyntaxNode?> Visit { get; set; } = visit;
	public ExpressionSyntax[] Parameters { get; set; } = parameters;
	public TypeSyntax[] TypeArguments { get; set; } = typeArguments;

	public readonly void Deconstruct(out PossibleLinqMethod Method, out IMethodSymbol MethodSymbol, out ITypeSymbol CollectionType, out Func<SyntaxNode?, SyntaxNode?> Visit, out ExpressionSyntax[] Parameters, out TypeSyntax[] TypeArguments)
	{
		Method = this.Method;
		MethodSymbol = this.MethodSymbol;
		CollectionType = this.CollectionType;
		Visit = this.Visit;
		Parameters = this.Parameters;
		TypeArguments = this.TypeArguments;
	}
}