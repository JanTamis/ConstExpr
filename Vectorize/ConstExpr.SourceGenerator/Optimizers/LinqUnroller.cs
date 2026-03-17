using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
	};
	
	/// <summary>
	/// Walks a LINQ method-chain rooted at <paramref name="node"/> and returns every
	/// recognised <see cref="PossibleLinqMethod"/> step together with its argument
	/// expressions, ordered from the first (innermost) call to the last (outermost).
	/// Stops as soon as a call with an unrecognised method name is encountered.
	/// </summary>
	public static UnrolledLinqMethod[] ParseLinqChain(SyntaxNode node)
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
				: [];
			
			// check if memberAccess.Expression is not a Type e.g Int32 or Int64
			if (memberAccess.Expression is IdentifierNameSyntax identifier
			    && Type.GetType($"System.{identifier.Identifier.Text}") != null)
			{
				return [ ];
			}
			

			methods.Add(new UnrolledLinqMethod(linqMethod, parameters, typeArguments));

			// Move inward to the receiver of this call
			current = memberAccess.Expression;
		}

		// Append the source expression last (before reversal) so it becomes element 0.
		if (current is not null)
		{
			methods.Add(new UnrolledLinqMethod(PossibleLinqMethod.Source, [ current ], []));
		}

		// We collected from outermost → innermost; reverse so callers get call order.
		methods.Reverse();

		return methods.ToArray();
	}

	public static SyntaxNode TryUnrollLinqChain(SyntaxNode node, IMethodSymbol lastMethod, IDictionary<SyntaxNode, bool> additionalMethods, IDictionary<string, VariableItem> variables)
	{
		var chain = ParseLinqChain(node);

		if (chain.Length <= 1)
		{
			return node;
		}

		const string collectionName = "collection";

		ExpressionSyntax elementName = IdentifierName("item");

		var isArray = IsInvokedOnArray(chain[0].Parameters[0], variables, out var arrayTypeSymbol);
		var isList = IsInvokedOnList(chain[0].Parameters[0], variables, out var listType);

		var elements = new List<StatementSyntax?>();

		if (isArray || isList)
		{
			if (chain.Length > 2 || chain[^1].Method is not (PossibleLinqMethod.Sum or PossibleLinqMethod.Count))
			{
				// add variable creation var item = collection[i];
				elements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(VariableDeclarator("item").WithInitializer(EqualsValueClause(ElementAccessExpression(IdentifierName(collectionName))
						.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("i")))))))))));
			}
			else
			{
				// just use indexer directly in elementName
				elementName = ElementAccessExpression(IdentifierName(collectionName))
					.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("i")))));
			}
		}

		ParseLoopBody(chain, elements, ref elementName);

		// under loop
		var statements = ParseAboveLoop(lastMethod, chain);

		if (isArray || isList)
		{
			// add for loop
			statements.Add(ForStatement(Block(elements))
				.WithDeclaration(VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(VariableDeclarator("i").WithInitializer(EqualsValueClause(SyntaxHelpers.CreateLiteral(0)!))))
				)
				.WithCondition(BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName("i"), MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(collectionName), IdentifierName(isArray ? "Length" : "Count"))))
				.WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName("i"))))
			);
		}

		ParsePossibleReturnStatement(chain, statements);

		// create localmethod with statements
		TypeSyntax parameterType;

		if (isArray)
		{
			parameterType = arrayTypeSymbol!.GetTypeSyntax(false);
		}
		else if (isList)
		{
			parameterType = listType!.GetTypeSyntax(false);
		}
		else
		{
			// Fallback to object type if neither array nor list
			parameterType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
		}

		var body = Block(statements);

		var localMethod = LocalFunctionStatement(lastMethod.ReturnType.GetTypeSyntax(false), Identifier($"{chain[^1].Method}_{body.GetDeterministicHashString()}"))
			.WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("collection")).WithType(parameterType))))
			.WithBody(body);
		
		additionalMethods[localMethod] = true;

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

	private static List<StatementSyntax> ParseAboveLoop(IMethodSymbol lastMethod, UnrolledLinqMethod[] chain)
	{
		var statements = new List<StatementSyntax>();

		if (Unrollers.TryGetValue(chain[^1].Method, out var unroller))
		{
			unroller.UnrollAboveLoop(chain[^1], lastMethod, statements);
		}
		
		return statements;
	}

	private static void ParseLoopBody(UnrolledLinqMethod[] chain, List<StatementSyntax?> elements, ref ExpressionSyntax elementName)
	{
		for (var i = 1; i < chain.Length; i++)
		{
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

	/// <summary>
	/// Checks if the invocation is made on an array type.
	/// </summary>
	private static bool IsInvokedOnArray(ExpressionSyntax? expression, IDictionary<string, VariableItem> variables, [NotNullWhen(true)] out IArrayTypeSymbol? elementType)
	{
		elementType = null;

		if (expression is IdentifierNameSyntax identifier
		    && variables.TryGetValue(identifier.Identifier.Text, out var variable)
		    && variable.Type is IArrayTypeSymbol arrType)
		{
			elementType = arrType;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if the invocation is made on a List&lt;T&gt; type.
	/// </summary>
	private static bool IsInvokedOnList(ExpressionSyntax? expression, IDictionary<string, VariableItem> variables, [NotNullWhen(true)] out INamedTypeSymbol? elementType)
	{
		elementType = null;

		if (expression is IdentifierNameSyntax identifier
		    && variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			elementType = variable.Type as INamedTypeSymbol;

			return elementType is not null
			       && elementType.OriginalDefinition.SpecialType == SpecialType.None
			       && elementType.OriginalDefinition.ToString() == "System.Collections.Generic.List<T>";
		}

		return false;
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

public readonly record struct UnrolledLinqMethod(PossibleLinqMethod Method, ExpressionSyntax[] Parameters, TypeSyntax[] TypeArguments)
{
	public override string ToString()
	{
		var typeArgs = TypeArguments.Length > 0
			? $"<{string.Join(", ", TypeArguments.Select(t => t.ToString()))}>"
			: string.Empty;
		return $"{Method}{typeArgs}({string.Join(", ", Parameters.Select(p => p.ToString()))})";
	}
}

file sealed class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return node.Identifier.Text == identifier ? replacement : base.VisitIdentifierName(node);
	}
}