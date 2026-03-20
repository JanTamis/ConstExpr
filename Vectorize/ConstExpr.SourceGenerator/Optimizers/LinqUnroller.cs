using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;
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
		{ PossibleLinqMethod.Contains, new ContainsLinqUnroller() },
		{ PossibleLinqMethod.Sum, new SumLinqUnroller() },
		{ PossibleLinqMethod.Count, new CountLinqUnrolled() },
		{ PossibleLinqMethod.Any, new AnyLinqUnroller() },
		{ PossibleLinqMethod.Average, new AverageLinqUnroller() },
		{ PossibleLinqMethod.Aggregate, new AggregateLinqUnroller() },
		{ PossibleLinqMethod.Distinct, new DistinctLinqUnroller() },
		{ PossibleLinqMethod.DistinctBy, new DistinctByLinqUnroller() },
	};

	/// <summary>
	/// Walks a LINQ method-chain rooted at <paramref name="node"/> and returns every
	/// recognised <see cref="PossibleLinqMethod"/> step together with its argument
	/// expressions, ordered from the first (innermost) call to the last (outermost).
	/// Stops as soon as a call with an unrecognised method name is encountered.
	/// </summary>
	public static UnrolledLinqMethod[] ParseLinqChain(SemanticModel model, Func<SyntaxNode?, SyntaxNode?> visit, SyntaxNode node)
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

			if (!model.TryGetSymbol<IMethodSymbol>(invocation, out var method))
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

		return methods.ToArray();
	}

	/// <summary>
	/// Attempts to resolve a LINQ extension method symbol from the compilation
	/// when the semantic model cannot resolve it (e.g., for synthetic/optimized nodes).
	/// </summary>
	private static IMethodSymbol? TryResolveLinqMethodFromCompilation(Compilation compilation, string methodName, int parameterCount)
	{
		var enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable");

		return enumerable?.GetMembers(methodName)
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == parameterCount);
	}

	public static SyntaxNode TryUnrollLinqChain(SyntaxNode node, Func<SyntaxNode?, SyntaxNode?> visit, SemanticModel model, IDictionary<SyntaxNode, bool> additionalMethods)
	{
		var chain = ParseLinqChain(model, visit, node);

		if (chain.Length <= 1
		    || !model.TryGetTypeSymbol(chain[0].Parameters[0], out var type))
		{
			return node;
		}

		for (var i = 0; i < chain.Length; i++)
		{
			chain[i].CollectionType = type;
		}

		if (!Unrollers.TryGetValue(chain[^1].Method, out var lastUnroller))
		{
			return node;
		}

		var collectionName = "collection";

		var elementName = lastUnroller.GetCollectionElement(chain[^1], collectionName);
		var elements = new List<StatementSyntax>();

		ParseLoopBody(chain, elements, ref elementName);

		// under loop
		var statements = ParseAboveLoop(chain);

		lastUnroller.CreateLoop(chain[^1], type, elements, collectionName, statements);

		ParsePossibleReturnStatement(chain, statements);

		// create localmethod with statements
		var parameterType = model.TryGetTypeSymbol(chain[0].Parameters[0], out var returnType) 
			? returnType.GetTypeSyntax(false) 
			: PredefinedType(Token(SyntaxKind.ObjectKeyword));

		var body = Block(statements);

		var localMethod = LocalFunctionStatement(chain[^1].MethodSymbol.ReturnType.GetTypeSyntax(false), Identifier($"{chain[^1].Method}_{body.GetDeterministicHashString()}"))
			.WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier(collectionName)).WithType(parameterType))))
			.WithBody(body);

		additionalMethods.TryAdd(localMethod, true);

		return InvocationExpression(IdentifierName(localMethod.Identifier))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain[0].Parameters[0]))));
	}

	private static void ParsePossibleReturnStatement(UnrolledLinqMethod[] chain, List<StatementSyntax> statements)
	{
		if (Unrollers.TryGetValue(chain[^1].Method, out var unroller))
		{
			unroller.UnrollUnderLoop(chain[^1], statements);
		}
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

	private static void ParseLoopBody(UnrolledLinqMethod[] chain, List<StatementSyntax> elements, ref ExpressionSyntax elementName)
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
		}
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
	Zip,
}

public struct UnrolledLinqMethod(SemanticModel model, PossibleLinqMethod method, IMethodSymbol methodSymbol, Func<SyntaxNode?, SyntaxNode?> visit, ExpressionSyntax[] parameters, TypeSyntax[] typeArguments)
{
	public override string ToString()
	{
		var typeArgs = TypeArguments.Length > 0
			? $"<{string.Join(", ", TypeArguments.Select(t => t.ToString()))}>"
			: string.Empty;
		return $"{Method}{typeArgs}({string.Join(", ", Parameters.Select(p => p.ToString()))})";
	}

	public SemanticModel Model { get; } = model;
	public PossibleLinqMethod Method { get; set; } = method;
	public IMethodSymbol MethodSymbol { get; set; } = methodSymbol;
	public ITypeSymbol CollectionType { get; set; }
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