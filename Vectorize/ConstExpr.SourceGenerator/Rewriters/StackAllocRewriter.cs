using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Rewrites a heap array creation held in a local into a stack allocation when it is provably
///   safe to do so:
///   <code>
///   var charCount = new int[256];   =>   Span&lt;int&gt; charCount = stackalloc int[256];
///   var data = new[] { 'K', 'H' };  =>   Span&lt;char&gt; data = stackalloc char[] { 'K', 'H' };
///   </code>
///   The array becomes a <c>Span&lt;T&gt;</c> backed by <c>stackalloc</c>, eliminating the heap
///   allocation for a throwaway local buffer. Because the result is a ref struct, any escape the
///   check below misses can only ever surface as a compile error, never as silent corruption — the
///   two hazards that <em>are</em> silent (a <c>stackalloc</c> inside a loop, or an unbounded size)
///   are owned outright by guards 3 and 4.
///   The pass is purely syntactic (it runs last, on a tree the semantic model no longer covers) and
///   deliberately conservative. It fires only on a single-declarator local whose initializer is a
///   sized <c>new T[N]</c>, an explicit <c>new T[] { … }</c>, or an implicit <c>new[] { … }</c>
///   (element type inferred from uniform <c>char</c>/<c>bool</c>/plain-<c>int</c> literals), where:
///   the element type is a predefined unmanaged primitive (1), the array is rank-1 (2), the element
///   count is a compile-time constant and <c>count * sizeof(T) &lt;= 1024</c> bytes (3), the
///   declaration is not inside a loop (4), the enclosing method is neither <c>async</c> nor an
///   iterator (5), and every use of the local is one of <c>name[i]</c>, <c>name.Length</c>,
///   <c>foreach (… in name)</c>, or the sole argument to <c>new string(name)</c> — the last being
///   safe because <c>string(ReadOnlySpan&lt;char&gt;)</c> copies (6). Anything outside this
///   allowlist leaves the declaration untouched.
/// </summary>
public sealed class StackAllocRewriter : CSharpSyntaxRewriter
{
	/// <summary>Upper bound on the stack footprint of a converted buffer, in bytes.</summary>
	private const int MaxStackAllocBytes = 1024;

	/// <summary>
	///   Applies the stackalloc conversion to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new StackAllocRewriter().Visit(node);
	}

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		// Analyse the original node (its parent chain is intact here, before any rewrite detaches
		// it) so the loop / escape checks can see the enclosing scope.
		return TryConvert(node) ?? base.VisitLocalDeclarationStatement(node);
	}

	private static LocalDeclarationStatementSyntax? TryConvert(LocalDeclarationStatementSyntax node)
	{
		if (node.Modifiers.Count != 0 || node.UsingKeyword.RawKind != 0
		                              || node.Declaration.Variables is not [ { Initializer.Value: { } initializer } declarator ])
		{
			return null;
		}

		TypeSyntax elementType;
		string keyword;
		int count;
		SeparatedSyntaxList<ExpressionSyntax>? elements;

		switch (initializer)
		{
			// new T[N] / new T[] { … } / new T[N] { … } — explicit element type.
			case ArrayCreationExpressionSyntax { Type: { RankSpecifiers: [ { Sizes: [ var size ] } ], ElementType: PredefinedTypeSyntax predefined } } creation:
			{
				keyword = predefined.Keyword.Text;
				elementType = predefined.WithoutTrivia();

				if (creation.Initializer is { } explicitInitializer)
				{
					elements = explicitInitializer.Expressions;
					count = explicitInitializer.Expressions.Count;
				}
				else if (size is LiteralExpressionSyntax { Token.Value: int literalSize })
				{
					elements = null;
					count = literalSize;
				}
				else
				{
					// Runtime size such as `new int[numbers.Length]` — unbounded, cannot stackalloc.
					return null;
				}

				break;
			}

			// new[] { … } — infer the element type from uniform literals.
			case ImplicitArrayCreationExpressionSyntax { Initializer.Expressions: { } implicitElements }:
			{
				if (InferPrimitiveKeyword(implicitElements) is not { } inferred)
				{
					return null;
				}

				keyword = inferred;
				elementType = PredefinedType(Token(KeywordKind(inferred)));
				elements = implicitElements;
				count = implicitElements.Count;
				break;
			}

			default:
			{
				return null;
			}
		}

		// Guards 1 + 3: unmanaged primitive of known size, constant non-empty count within the cap.
		if (PrimitiveSize(keyword) is not { } elementBytes || count <= 0 || (long) count * elementBytes > MaxStackAllocBytes)
		{
			return null;
		}

		var name = declarator.Identifier.Text;

		// Guards 4 + 5 + 6.
		if (IsInsideLoop(node) || IsInAsyncOrIterator(node) || !AllUsesAllowed(node, name))
		{
			return null;
		}

		var arrayType = elements is { } initializerElements
			? StackAllocArrayCreationExpression(
				ArrayType(elementType).WithRankSpecifiers(SingletonList(
					ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))),
				InitializerExpression(SyntaxKind.ArrayInitializerExpression, initializerElements))
			: StackAllocArrayCreationExpression(
				ArrayType(elementType).WithRankSpecifiers(SingletonList(
					ArrayRankSpecifier(SingletonSeparatedList(CreateLiteral(count))))));

		var spanType = GenericName("Span").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType)));

		return LocalDeclarationStatement(
				VariableDeclaration(spanType).WithVariables(SingletonSeparatedList(
					VariableDeclarator(declarator.Identifier).WithInitializer(EqualsValueClause(arrayType)))))
			.WithTriviaFrom(node);
	}

	/// <summary>
	///   Infers the element type of an implicit array from its initializer, but only for the
	///   unambiguous v1 cases: every element must be a literal and they must all be <c>char</c>,
	///   all <c>bool</c>, or all plain <c>int</c>. Anything else (mixed kinds, suffixed/decimal
	///   numerics, strings, <c>null</c>, negatives, non-literals) yields <c>null</c> so the caller
	///   bails.
	/// </summary>
	private static string? InferPrimitiveKeyword(SeparatedSyntaxList<ExpressionSyntax> elements)
	{
		string? keyword = null;

		foreach (var element in elements)
		{
			if (element is not LiteralExpressionSyntax literal)
			{
				return null;
			}

			var current = literal.Token.Value switch
			{
				char => "char",
				bool => "bool",
				int => "int",
				_ => null
			};

			if (current is null)
			{
				return null;
			}

			if (keyword is null)
			{
				keyword = current;
			}
			else if (keyword != current)
			{
				return null;
			}
		}

		return keyword;
	}

	private static SyntaxKind KeywordKind(string keyword)
	{
		return keyword switch
		{
			"char" => SyntaxKind.CharKeyword,
			"bool" => SyntaxKind.BoolKeyword,
			_ => SyntaxKind.IntKeyword
		};
	}

	/// <summary>
	///   Byte size of a predefined unmanaged primitive, or <c>null</c> for a type this pass does not
	///   convert (managed types such as <c>string</c>/<c>object</c> fall through to <c>null</c>).
	/// </summary>
	internal static int? PrimitiveSize(string keyword)
	{
		return keyword switch
		{
			"bool" or "byte" or "sbyte" => 1,
			"char" or "short" or "ushort" => 2,
			"int" or "uint" or "float" => 4,
			"long" or "ulong" or "double" => 8,
			"decimal" => 16,
			_ => null
		};
	}

	/// <summary>
	///   True when the declaration sits inside a loop within its own method — a <c>stackalloc</c>
	///   there is freed only when the method returns, so it grows the stack every iteration.
	/// </summary>
	private static bool IsInsideLoop(SyntaxNode node)
	{
		foreach (var ancestor in node.Ancestors())
		{
			switch (ancestor)
			{
				case ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
					return true;

				// Reached the enclosing callable — a loop outside it is a fresh frame per call, safe.
				case BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax:
					return false;
			}
		}

		return false;
	}

	/// <summary>
	///   <c>Span&lt;T&gt;</c> locals are illegal in <c>async</c> methods and in iterators
	///   (CS4012 / CS4013), so bail if the enclosing callable is either.
	/// </summary>
	private static bool IsInAsyncOrIterator(SyntaxNode node)
	{
		var container = node.Ancestors().FirstOrDefault(ancestor =>
			ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

		switch (container)
		{
			case MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.AsyncKeyword):
			case LocalFunctionStatementSyntax localFunction when localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword):
			case AnonymousFunctionExpressionSyntax lambda when lambda.AsyncKeyword.RawKind != 0:
				return true;
		}

		return container is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax
		       && container.DescendantNodes().OfType<YieldStatementSyntax>().Any();
	}

	/// <summary>
	///   Every reference to <paramref name="name" /> within its enclosing block must be a
	///   <c>Span</c>-safe use: indexing, <c>.Length</c>, a <c>foreach</c> source, or the sole
	///   argument to <c>new string(name)</c> (safe — that ctor copies from
	///   <c>ReadOnlySpan&lt;char&gt;</c>). A use inside a nested lambda / local function bails too,
	///   since a ref struct cannot be captured.
	/// </summary>
	private static bool AllUsesAllowed(LocalDeclarationStatementSyntax node, string name)
	{
		var scope = node.Ancestors().OfType<BlockSyntax>().FirstOrDefault();

		if (scope is null)
		{
			return false;
		}

		foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
		{
			if (identifier.Identifier.Text == name && !IsAllowedUse(identifier, scope))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsAllowedUse(IdentifierNameSyntax identifier, BlockSyntax scope)
	{
		foreach (var ancestor in identifier.Ancestors())
		{
			if (ancestor == scope)
			{
				break;
			}

			if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
			{
				return false;
			}
		}

		return identifier.Parent switch
		{
			ElementAccessExpressionSyntax elementAccess when elementAccess.Expression == identifier => true,
			MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == identifier && memberAccess.Name.Identifier.Text == "Length" => true,
			ForEachStatementSyntax forEach when forEach.Expression == identifier => true,
			ArgumentSyntax { Parent: ArgumentListSyntax { Arguments.Count: 1, Parent: ObjectCreationExpressionSyntax creation } } argument
				when argument.Expression == identifier && IsStringType(creation.Type) => true,
			_ => false
		};
	}

	private static bool IsStringType(TypeSyntax type)
	{
		return type switch
		{
			PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
			IdentifierNameSyntax name => name.Identifier.Text is "string" or "String",
			QualifiedNameSyntax qualified => qualified.Right.Identifier.Text is "string" or "String",
			_ => false
		};
	}
}