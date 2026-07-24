using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Rewrites indexing into direct reference arithmetic, so the runtime no longer range-checks the
///   index:
///   <code>
///   var max = numbers[0];        =>   ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
///   sum += numbers[i];                var max = numbersRef;
///                                     sum += Unsafe.Add(ref numbersRef, (nuint) i);
///   </code>
///   Handles <c>T[]</c>, <c>Span&lt;T&gt;</c>, <c>ReadOnlySpan&lt;T&gt;</c> and <c>List&lt;T&gt;</c>;
///   only the entry point into the storage differs (see <see cref="ReferenceTo" />). One <c>ref</c>
///   local is hoisted per collection — at the top of the body for a parameter, right after the
///   declaration for a local — and every indexing of it is rewritten against that local. An index of
///   literal <c>0</c> becomes the <c>ref</c> local itself.
///   <para>
///     This pass does <em>not</em> prove indices stay in range; that is the caller's guarantee,
///     which is why <see cref="ConstExpr.Core.Enumerators.OptimizationFlags.BoundsCheckElimination" />
///     is opt-in and excluded from <c>All</c>. What is checked below are the conditions under which
///     the rewrite is well-formed at all: the access shape (1), the collection never being reassigned
///     out from under the hoisted reference (2), no capture into a lambda where a <c>ref</c> local
///     cannot go (3), array-store covariance, which a write through <c>Unsafe.Add</c> would silently
///     skip (4), and — for <c>List&lt;T&gt;</c> only — nothing that can swap its backing array (5).
///   </para>
/// </summary>
public sealed class BoundsCheckRewriter
{
	/// <summary>The storage kinds this pass knows how to take a reference into.</summary>
	private enum CollectionKind
	{
		Array,
		Span,
		ReadOnlySpan,
		List,
		String
	}

	/// <summary>
	///   A collection eligible for rewriting. Identified by name rather than by node, because each
	///   rewrite produces a fresh tree and the declaration has to be located again in it.
	/// </summary>
	private sealed record Candidate(string Name, CollectionKind Kind, bool AllowWrites, bool IsLocal);

	/// <summary>
	///   Applies bounds-check elimination to the supplied body. <paramref name="parameters" /> is the
	///   enclosing method's parameter list — the semantic model no longer covers the rewritten tree,
	///   so declared array types are read from there.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, ParameterListSyntax parameters, IDictionary<string, VariableItem> variables)
	{
		if (node is not BlockSyntax body)
		{
			return node;
		}

		// A ref local is illegal in an async method (CS8177) and in an iterator (CS8176). The body is
		// detached from its declaration by now, so infer both from what it contains.
		if (body.DescendantNodes().Any(child => child is YieldStatementSyntax or AwaitExpressionSyntax))
		{
			return body;
		}

		var taken = new HashSet<string>(body.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Select(identifier => identifier.Identifier.Text));

		// A declared-but-unused local never shows up as an IdentifierNameSyntax, so seed those too.
		foreach (var declarator in body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
		{
			taken.Add(declarator.Identifier.Text);
		}

		foreach (var parameter in parameters.Parameters)
		{
			taken.Add(parameter.Identifier.Text);
		}

		foreach (var candidate in CollectCandidates(body, parameters, variables))
		{
			body = Rewrite(body, candidate, taken);
		}

		return body;
	}

	private static IEnumerable<Candidate> CollectCandidates(BlockSyntax body, ParameterListSyntax parameters, IDictionary<string, VariableItem> variables)
	{
		foreach (var parameter in parameters.Parameters)
		{
			if (Classify(parameter.Type) is var (kind, allowWrites) && IsEligible(body, parameter.Identifier.Text, kind, 0))
			{
				yield return new Candidate(parameter.Identifier.Text, kind, allowWrites, false);
			}
		}

		foreach (var declaration in body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
		{
			if (declaration.Declaration.Variables is not [ var declarator ])
			{
				continue;
			}

			if (ClassifyLocal(declaration, declarator, variables) is var (kind, allowWrites) && IsEligible(body, declarator.Identifier.Text, kind, 1))
			{
				yield return new Candidate(declarator.Identifier.Text, kind, allowWrites, true);
			}
		}
	}

	/// <summary>
	///   Classifies a declared type, yielding the storage kind and whether writes through the
	///   reference are allowed. Writes are refused for a <c>ReadOnlySpan&lt;T&gt;</c> and for an array
	///   of anything but a predefined primitive — an array store is covariance-checked at runtime and
	///   <c>Unsafe.Add</c> would skip that check. <c>Span&lt;T&gt;</c> and <c>List&lt;T&gt;</c> are
	///   invariant, so they have no such check to lose.
	/// </summary>
	private static (CollectionKind Kind, bool AllowWrites)? Classify(TypeSyntax? type)
	{
		switch (type)
		{
			case ArrayTypeSyntax { RankSpecifiers: [ { Sizes.Count: 1 } ] } array:
				return (CollectionKind.Array, IsPrimitiveElement(array.ElementType));

			// A string has no indexer setter, so no write can exist to rewrite in the first place.
			case PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.StringKeyword }:
			case IdentifierNameSyntax { Identifier.Text: "String" }:
				return (CollectionKind.String, false);

			// System.Span<int> and the like.
			case QualifiedNameSyntax qualified:
				return Classify(qualified.Right);

			// A nullable annotation on a reference type (`string?`, `int[]?`) says nothing about how it
			// is indexed. A nullable *value* type unwraps to something this method rejects anyway.
			case NullableTypeSyntax nullable:
				return Classify(nullable.ElementType);

			// ponytail: matches the type name only, not the namespace it came from — the pass runs on a
			// tree the semantic model no longer covers. A user type of the same name emits a call it
			// does not have, which fails loudly as a compile error in the generated code rather than
			// silently. Same trade-off as IndexFromEndRewriter; add a semantic guard if that stops
			// being acceptable.
			case GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic:
				return generic.Identifier.Text switch
				{
					"Span" => (CollectionKind.Span, true),
					"ReadOnlySpan" => (CollectionKind.ReadOnlySpan, false),
					"List" => (CollectionKind.List, true),
					_ => null
				};

			default:
				return null;
		}
	}

	/// <summary>
	///   Classifies a local from its declared type, falling back to its initializer when it was
	///   declared with <c>var</c>, and finally to the type the interpreter already resolved for it
	///   (<see cref="Classify(ITypeSymbol)" />) for an initializer shape neither syntax case covers —
	///   <c>var chars = input.ToCharArray()</c>, <c>var copy = source.ToArray()</c>.
	/// </summary>
	private static (CollectionKind Kind, bool AllowWrites)? ClassifyLocal(LocalDeclarationStatementSyntax declaration, VariableDeclaratorSyntax declarator, IDictionary<string, VariableItem> variables)
	{
		if (Classify(declaration.Declaration.Type) is { } declared)
		{
			return declared;
		}

		// Deliberately no stackalloc case: `var b = stackalloc int[8]` is an `int*`, not a Span<int>,
		// and a pointer has no bounds check to remove in the first place.
		if (declarator.Initializer?.Value switch
		    {
			    ArrayCreationExpressionSyntax creation => Classify(creation.Type),
			    ObjectCreationExpressionSyntax objectCreation => Classify(objectCreation.Type),
			    _ => null
		    } is { } fromInitializerSyntax)
		{
			return fromInitializerSyntax;
		}

		return variables.TryGetValue(declarator.Identifier.Text, out var item) ? Classify(item.Type) : null;
	}

	/// <summary>
	///   Semantic counterpart to <see cref="Classify(TypeSyntax)" />. Sourced from
	///   <see cref="VariableItem.Type" />, which the interpreter records for every local — including
	///   one whose value can't be folded — while the semantic model still covered the original tree;
	///   by the time this pass runs the model no longer matches the rewritten nodes (see the class
	///   remarks), so this is looked up by name instead of re-querying it.
	/// </summary>
	private static (CollectionKind Kind, bool AllowWrites)? Classify(ITypeSymbol? type)
	{
		switch (type)
		{
			case IArrayTypeSymbol { Rank: 1 } array:
				return (CollectionKind.Array, IsPrimitiveElement(array.ElementType));

			case { SpecialType: SpecialType.System_String }:
				return (CollectionKind.String, false);

			case INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named:
				return (named.ContainingNamespace?.ToDisplayString(), named.Name) switch
				{
					("System", "Span") => (CollectionKind.Span, true),
					("System", "ReadOnlySpan") => (CollectionKind.ReadOnlySpan, false),
					("System.Collections.Generic", "List") => (CollectionKind.List, true),
					_ => null
				};

			default:
				return null;
		}
	}

	private static bool IsPrimitiveElement(TypeSyntax elementType)
	{
		return elementType is PredefinedTypeSyntax predefined && StackAllocRewriter.PrimitiveSize(predefined.Keyword.Text) is not null;
	}

	// Mirrors the keyword list in StackAllocRewriter.PrimitiveSize — no nint/nuint/IntPtr, those
	// aren't recognized there either.
	private static bool IsPrimitiveElement(ITypeSymbol elementType)
	{
		return elementType.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte
			or SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16
			or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single
			or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double
			or SpecialType.System_Decimal;
	}

	/// <summary>
	///   Conditions 2 and 3: the array is never reassigned, and never used inside a lambda. Plus a
	///   shadowing check — the name must be declared exactly <paramref name="expectedDeclarations" />
	///   times (0 for a parameter, 1 for a local), otherwise two same-named arrays in sibling scopes
	///   would both be rewritten against the first one's reference.
	/// </summary>
	private static bool IsEligible(BlockSyntax body, string name, CollectionKind kind, int expectedDeclarations)
	{
		return DeclarationCount(body, name) == expectedDeclarations
		       && (kind != CollectionKind.List || HasStableBackingArray(body, name))
		       && !body.DescendantNodes().Any(node => IsAssignmentTo(node, name))
		       && !body.DescendantNodes()
			       .OfType<IdentifierNameSyntax>()
			       .Where(identifier => identifier.Identifier.Text == name)
			       .Any(identifier => identifier.Ancestors()
				       .TakeWhile(ancestor => ancestor != body)
				       .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
	}

	/// <summary>
	///   Condition 5, <c>List&lt;T&gt;</c> only. <c>CollectionsMarshal.AsSpan</c> hands out a span over
	///   the list's <em>current</em> backing array; anything that grows or replaces it (<c>Add</c>,
	///   <c>Insert</c>, <c>Clear</c>, a capacity change) leaves the hoisted reference pointing at the
	///   old array. Since a list handed to another method can be mutated there too, only indexing and
	///   <c>.Count</c> pass. This hazard is unique to <c>List&lt;T&gt;</c> — an array or span cannot be
	///   resized at all.
	///   <para>
	///     <c>foreach</c> is deliberately not allowed either, even though it cannot resize: a write
	///     through the span skips the <c>_version</c> bump the list's own indexer does, so
	///     <c>foreach (var x in values) values[0] = x;</c> would stop throwing
	///     <see cref="System.InvalidOperationException" /> and silently mutate instead. The caller
	///     signed up for dropping bounds checks, not for losing mutation-during-enumeration detection.
	///   </para>
	/// </summary>
	private static bool HasStableBackingArray(BlockSyntax body, string name)
	{
		return body.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(identifier => identifier.Identifier.Text == name)
			.All(identifier => identifier.Parent switch
			{
				ElementAccessExpressionSyntax access => access.Expression == identifier,
				MemberAccessExpressionSyntax member => member.Expression == identifier && member.Name.Identifier.Text == "Count",
				_ => false
			});
	}

	/// <summary>How many times <paramref name="name" /> is introduced as a new variable in the body.</summary>
	private static int DeclarationCount(BlockSyntax body, string name)
	{
		return body.DescendantNodes().Count(node => node switch
		{
			VariableDeclaratorSyntax declarator => declarator.Identifier.Text == name,
			ForEachStatementSyntax forEach => forEach.Identifier.Text == name,
			SingleVariableDesignationSyntax designation => designation.Identifier.Text == name,
			CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier.Text == name,
			_ => false
		});
	}

	private static bool IsAssignmentTo(SyntaxNode node, string name)
	{
		return node switch
		{
			AssignmentExpressionSyntax assignment => IsNamed(assignment.Left, name),
			PrefixUnaryExpressionSyntax prefix => IsNamed(prefix.Operand, name),
			PostfixUnaryExpressionSyntax postfix => IsNamed(postfix.Operand, name),
			ArgumentSyntax { RefKindKeyword.RawKind: not 0 } argument => IsNamed(argument.Expression, name),
			_ => false
		};
	}

	private static bool IsNamed(ExpressionSyntax expression, string name)
	{
		return expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == name;
	}

	private static BlockSyntax Rewrite(BlockSyntax body, Candidate candidate, HashSet<string> taken)
	{
		// Located in the current tree, not carried over from collection time: an earlier candidate's
		// rewrite may already have rebuilt this statement. The name is unique (see IsEligible).
		var declaration = candidate.IsLocal
			? body.DescendantNodes()
				.OfType<LocalDeclarationStatementSyntax>()
				.First(local => local.Declaration.Variables[0].Identifier.Text == candidate.Name)
			: null;

		// Tracked up front: replacing the accesses inside the declaration destroys its node identity,
		// and the reference has to be inserted right after it.
		var tracked = declaration is null ? body : body.TrackNodes(declaration);

		var accesses = tracked.DescendantNodes()
			.OfType<ElementAccessExpressionSyntax>()
			.Where(access => IsRewritable(access, candidate))
			.ToList();

		if (accesses.Count == 0)
		{
			return body;
		}

		var name = UniqueName(candidate.Name, taken);
		var replaced = tracked.ReplaceNodes(accesses, (original, _) => ReplaceAccess(original, name));
		var reference = ReferenceDeclaration(name, candidate);

		if (declaration is null)
		{
			return replaced.WithStatements(replaced.Statements.Insert(0, reference));
		}

		var current = replaced.GetCurrentNode(declaration);

		return current is null
			? body
			: replaced.InsertNodesAfter(current, [ reference ]);
	}

	/// <summary>
	///   Condition 1: a single plain integer argument. An index-from-end (<c>arr[^1]</c>) or a range
	///   (<c>arr[1..]</c>) is a different indexer entirely and is left alone. Condition 4: a write
	///   through <c>Unsafe.Add</c> bypasses the array-store covariance check, so writes are rewritten
	///   only for primitive element types, where covariance cannot arise.
	/// </summary>
	private static bool IsRewritable(ElementAccessExpressionSyntax access, Candidate candidate)
	{
		return IsNamed(access.Expression, candidate.Name)
		       && access.ArgumentList.Arguments is [ { NameColon: null, RefKindKeyword.RawKind: 0 } argument ]
		       && !argument.Expression.IsKind(SyntaxKind.IndexExpression)
		       && argument.Expression is not RangeExpressionSyntax
		       && (candidate.AllowWrites || !IsWriteTarget(access));
	}

	private static bool IsWriteTarget(ElementAccessExpressionSyntax access)
	{
		return access.Parent switch
		{
			AssignmentExpressionSyntax assignment => assignment.Left == access,
			PrefixUnaryExpressionSyntax prefix => prefix.Operand == access,
			PostfixUnaryExpressionSyntax postfix => postfix.Operand == access,
			ArgumentSyntax { RefKindKeyword.RawKind: not 0 } => true,
			_ => false
		};
	}

	private static ExpressionSyntax ReplaceAccess(ElementAccessExpressionSyntax node, string name)
	{
		var index = node.ArgumentList.Arguments[0].Expression;

		// Offset zero is the reference itself; no arithmetic needed.
		if (index is LiteralExpressionSyntax { Token.Value: 0 })
		{
			return IdentifierName(name).WithTriviaFrom(node);
		}

		var offset = CastExpression(IdentifierName("nuint"), ParenthesizeIfNeeded(index));

		return InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("Unsafe"), IdentifierName("Add")))
			.WithArgumentList(ArgumentList(SeparatedList(
			[
				Argument(IdentifierName(name)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword)),
				Argument(offset)
			])))
			.WithTriviaFrom(node);
	}

	/// <summary>
	///   Hoisting above a null check is safe: none of these entry points dereference, they only
	///   compute an address, so a null array or list still throws its <c>NullReferenceException</c> at
	///   the first real use further down.
	/// </summary>
	private static LocalDeclarationStatementSyntax ReferenceDeclaration(string name, Candidate candidate)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(RefType(IdentifierName("var")))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(RefExpression(ReferenceTo(candidate)))))));
	}

	/// <summary>
	///   The entry point into each storage kind. An array exposes its first element directly; a span
	///   goes through <c>GetReference</c>; a list first has to be viewed as a span over its backing
	///   array — which is exactly why <see cref="HasStableBackingArray" /> guards it — and a string
	///   likewise via <c>AsSpan</c>.
	///   <para>
	///     A string deliberately does <em>not</em> use <c>string.GetPinnableReference()</c>, the
	///     obvious-looking API here. That one is an instance call that throws
	///     <see cref="System.NullReferenceException" /> the moment it runs on a null string, so
	///     hoisting it above a <c>if (text is null)</c> guard would turn a normal early return into a
	///     crash. <c>AsSpan()</c> maps null to an empty span and only computes an address, matching how
	///     the array and list entry points behave. Both compile to the same load.
	///   </para>
	/// </summary>
	private static ExpressionSyntax ReferenceTo(Candidate candidate)
	{
		var target = IdentifierName(candidate.Name);

		return candidate.Kind switch
		{
			CollectionKind.Array => Call("MemoryMarshal", "GetArrayDataReference", target),
			// CollectionKind.String => InvocationExpression(MemberAccessExpression(target, IdentifierName("GetPinnableReference"))),
			_ => Call("MemoryMarshal", "GetReference", candidate.Kind switch
			{
				CollectionKind.List => Call("CollectionsMarshal", "AsSpan", target),
				CollectionKind.String => InvocationExpression(MemberAccessExpression(target, IdentifierName("AsSpan"))),
				_ => target
			})
		};

	}

	private static InvocationExpressionSyntax Call(string receiver, string method, ExpressionSyntax argument)
	{
		return InvocationExpression(
				MemberAccessExpression(IdentifierName(receiver), IdentifierName(method)))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(argument))));
	}

	private static string UniqueName(string arrayName, HashSet<string> taken)
	{
		var name = $"{arrayName}Ref";

		for (var suffix = 2; !taken.Add(name); suffix++)
		{
			name = $"{arrayName}Ref{suffix}";
		}

		return name;
	}

	// The cast binds tighter than any binary operator, so "(nuint) i + 1" would offset by the wrong
	// amount; an already-atomic index needs no parens.
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
	{
		return expression is LiteralExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax
			or ParenthesizedExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
			? expression
			: ParenthesizedExpression(expression);
	}
}