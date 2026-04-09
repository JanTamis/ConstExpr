using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts <c>is</c> type-check + cast patterns to the modern
/// <c>is T name</c> pattern matching syntax.
/// Inspired by the Roslyn <c>CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer</c>.
///
/// Pattern 1 — if-statement with cast:
/// <code>
/// if (obj is MyType)
/// {
///     var x = (MyType)obj;
///     x.DoSomething();
/// }
/// </code>
/// →
/// <code>
/// if (obj is MyType x)
/// {
///     x.DoSomething();
/// }
/// </code>
///
/// Pattern 2 — as-cast with null check:
/// <code>
/// var x = obj as MyType;
/// if (x != null)
/// {
///     x.DoSomething();
/// }
/// </code>
/// →
/// <code>
/// if (obj is MyType x)
/// {
///     x.DoSomething();
/// }
/// </code>
/// </summary>
public static class UsePatternMatchingRefactoring
{
	/// <summary>
	/// Converts an if-statement with a type-check condition and an inner cast assignment
	/// to use pattern matching instead.
	///
	/// Before: <c>if (obj is MyType) { var x = (MyType)obj; ... }</c>
	/// After:  <c>if (obj is MyType x) { ... }</c>
	/// </summary>
	public static bool TryConvertIsAndCastToPattern(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Condition must be: expr is Type
		if (ifStatement.Condition is not BinaryExpressionSyntax { Left: var checkedExpr, Right: TypeSyntax type } isExpr
		    || !isExpr.IsKind(SyntaxKind.IsExpression))
		{
			return false;
		}

		// Body must start with a cast assignment: var x = (Type)expr;

		if (ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 } body)
		{
			return false;
		}

		if (body.Statements[0] is not LocalDeclarationStatementSyntax { Declaration.Variables: [ var variable ] })
		{
			return false;
		}

		if (variable.Initializer?.Value is not CastExpressionSyntax cast)
		{
			return false;
		}

		// The cast type and checked type must match, and the cast target must match
		if (cast.Type.GetDeterministicHash() != type.GetDeterministicHash())
		{
			return false;
		}

		if (checkedExpr.GetDeterministicHash() != cast.Expression.GetDeterministicHash())
		{
			return false;
		}

		var variableName = variable.Identifier;

		// Build pattern: obj is MyType x
		var pattern = IsPatternExpression(
			checkedExpr,
			DeclarationPattern(
				type.WithoutTrivia(),
				SingleVariableDesignation(variableName)));

		// Remove the cast assignment from the body
		var newBody = body.WithStatements(body.Statements.RemoveAt(0));

		result = ifStatement
			.WithCondition(pattern)
			.WithStatement(newBody);

		return true;
	}

	/// <summary>
	/// Converts an as-cast followed by a null check into pattern matching.
	///
	/// Before:
	/// <code>
	/// var x = obj as MyType;
	/// if (x != null) { ... }
	/// </code>
	/// After:
	/// <code>
	/// if (obj is MyType x) { ... }
	/// </code>
	///
	/// Returns the replacement if-statement. The caller must remove the preceding
	/// local declaration from the containing block.
	/// </summary>
	public static bool TryConvertAsAndNullCheckToPattern(
		LocalDeclarationStatementSyntax localDecl,
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Local must be: var x = expr as Type;
		if (localDecl.Declaration.Variables.Count != 1)
		{
			return false;
		}

		var variable = localDecl.Declaration.Variables[0];

		if (variable.Initializer?.Value is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AsExpression } asExpr)
		{
			return false;
		}

		if (asExpr.Right is not TypeSyntax type)
		{
			return false;
		}

		var variableName = variable.Identifier.ValueText;
		var sourceExpr = asExpr.Left;

		// if condition must be: x != null  or  x is not null
		if (!IsNullCheckOnVariable(ifStatement.Condition, variableName))
		{
			return false;
		}

		var pattern = IsPatternExpression(
			sourceExpr.WithoutTrivia(),
			DeclarationPattern(
				type.WithoutTrivia(),
				SingleVariableDesignation(variable.Identifier)));

		result = ifStatement.WithCondition(pattern);
		return true;
	}

	/// <summary>
	/// Converts a negated is-expression or is-pattern to a <c>not</c> pattern (C# 9+).
	/// Inspired by the Roslyn <c>CSharpUseNotPatternDiagnosticAnalyzer</c>.
	///
	/// <list type="bullet">
	///   <item><c>!(x is T y)</c>   → <c>x is not T y</c></item>
	///   <item><c>!(x is null)</c>  → <c>x is not null</c></item>
	///   <item><c>!(x is T)</c>     → <c>x is not T</c></item>
	/// </list>
	/// </summary>
	public static bool TryConvertNegatedIsToNotPattern(
		PrefixUnaryExpressionSyntax logicalNot,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (!logicalNot.IsKind(SyntaxKind.LogicalNotExpression))
		{
			return false;
		}

		// Must be !(...)
		if (logicalNot.Operand is not ParenthesizedExpressionSyntax { Expression: var inner })
		{
			return false;
		}

		switch (inner)
		{
			// !(x is T y)  →  x is not T y
			// !(x is null) →  x is not null
			case IsPatternExpressionSyntax
			{
				Pattern: DeclarationPatternSyntax or ConstantPatternSyntax
			} isPattern:
			{
				result = IsPatternExpression(
						isPattern.Expression,
						UnaryPattern(
							Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace),
							isPattern.Pattern.WithoutLeadingTrivia()))
					.WithTriviaFrom(logicalNot);
				return true;
			}

			// !(x is T)  →  x is not T
			// Note: nullable types (e.g. !(x is T?)) are not converted here because
			// "x is not T?" is not legal in C#.  Callers must perform that check when
			// a SemanticModel is available.
			case BinaryExpressionSyntax
			{
				RawKind: (int) SyntaxKind.IsExpression,
				Left: var isLeft,
				Right: TypeSyntax isType
			}:
			{
				result = IsPatternExpression(
						isLeft,
						UnaryPattern(
							Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace),
							TypePattern(isType.WithoutTrivia())))
					.WithTriviaFrom(logicalNot);
				return true;
			}

			default:
				return false;
		}
	}

	/// <summary>
	/// Converts an as-cast with a binary member-access comparison to a recursive property pattern (C# 8+).
	/// Inspired by the Roslyn <c>CSharpAsAndMemberAccessDiagnosticAnalyzer</c>.
	///
	/// <list type="bullet">
	///   <item><c>(expr as T)?.Prop == constant</c>  → <c>expr is T { Prop: constant }</c></item>
	///   <item><c>(expr as T)?.Prop != null</c>      → <c>expr is T { Prop: not null }</c></item>
	/// </list>
	///
	/// For chained access (<c>?.A.B</c>), extended property patterns (C# 10) are produced.
	/// <para>
	/// Note: <c>== null</c> is intentionally rejected — <c>(x as T)?.Prop == null</c> and
	/// <c>x is T {{ Prop: null }}</c> have different semantics when the type check fails.
	/// </para>
	/// </summary>
	public static bool TryConvertAsAndMemberAccessToPropertyPattern(
		BinaryExpressionSyntax comparison,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result)
	{
		result = null;

		if (!comparison.IsKind(SyntaxKind.EqualsExpression) &&
		    !comparison.IsKind(SyntaxKind.NotEqualsExpression))
		{
			return false;
		}

		if (comparison.Left is not ConditionalAccessExpressionSyntax conditionalAccess)
		{
			return false;
		}

		if (!TryDecomposeAsAndMemberAccess(conditionalAccess, out var sourceExpr, out var type, out var memberPath))
		{
			return false;
		}

		PatternSyntax valuePattern;

		if (comparison.IsKind(SyntaxKind.EqualsExpression))
		{
			// (expr as T)?.Prop == null → NOT safe; semantics differ when the type check fails
			if (comparison.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression })
			{
				return false;
			}

			valuePattern = ConstantPattern(comparison.Right.WithoutTrivia());
		}
		else
		{
			// != null  →  not null  (semantically safe)
			if (comparison.Right is not LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression })
			{
				return false;
			}

			valuePattern = UnaryPattern(
				Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace),
				ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)));
		}

		result = BuildPropertyPatternExpression(sourceExpr, type, memberPath, valuePattern)
			.WithTriviaFrom(comparison);
		return true;
	}

	/// <summary>
	/// Converts an as-cast with an is-pattern member-access check to a recursive property pattern (C# 8+).
	/// Inspired by the Roslyn <c>CSharpAsAndMemberAccessDiagnosticAnalyzer</c>.
	///
	/// <c>(expr as T)?.Prop is pattern</c>  →  <c>expr is T { Prop: pattern }</c>
	///
	/// <para>
	/// The conversion is rejected when the inner pattern is a negated declaration, var or recursive
	/// pattern with a designation, because <c>not X y</c> is not legal in a nested property context.
	/// </para>
	/// </summary>
	public static bool TryConvertAsAndMemberAccessIsPatternToPropertyPattern(
		IsPatternExpressionSyntax isPattern,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result)
	{
		result = null;

		if (isPattern.Expression is not ConditionalAccessExpressionSyntax conditionalAccess)
		{
			return false;
		}

		if (!TryDecomposeAsAndMemberAccess(conditionalAccess, out var sourceExpr, out var type, out var memberPath))
		{
			return false;
		}

		// Roslyn rejects: (expr as T)?.Prop is not X y — not legal in nested context
		if (isPattern.Pattern is UnaryPatternSyntax
		    {
			    Pattern: DeclarationPatternSyntax or VarPatternSyntax or
			    RecursivePatternSyntax { Designation: not null }
		    })
		{
			return false;
		}

		result = BuildPropertyPatternExpression(sourceExpr, type, memberPath, isPattern.Pattern.WithoutTrivia())
			.WithTriviaFrom(isPattern);
		return true;
	}

	/// <summary>
	/// Decomposes <c>(expr as T)?.A.B…</c> into its source expression, target type, and
	/// the member access path as a plain expression suitable for use in a property pattern name.
	/// </summary>
	private static bool TryDecomposeAsAndMemberAccess(
		ConditionalAccessExpressionSyntax conditionalAccess,
		[NotNullWhen(true)] out ExpressionSyntax? sourceExpr,
		[NotNullWhen(true)] out TypeSyntax? type,
		[NotNullWhen(true)] out ExpressionSyntax? memberPath)
	{
		sourceExpr = null;
		type = null;
		memberPath = null;

		// Must be: (expr as T)?...
		if (conditionalAccess.Expression is not ParenthesizedExpressionSyntax
		    {
			    Expression: BinaryExpressionSyntax
			    {
				    RawKind: (int) SyntaxKind.AsExpression,
				    Left: var left,
				    Right: TypeSyntax asType
			    }
		    })
		{
			return false;
		}

		var path = BuildMemberPath(conditionalAccess.WhenNotNull);

		if (path is null)
		{
			return false;
		}

		sourceExpr = left;
		type = asType;
		memberPath = path;
		return true;
	}

	/// <summary>
	/// Converts the <c>WhenNotNull</c> part of a conditional access expression
	/// (e.g., <c>.Prop</c> or <c>.A.B</c>) into a plain expression usable as a
	/// property pattern name — either an <see cref="IdentifierNameSyntax"/> (single level)
	/// or a <see cref="MemberAccessExpressionSyntax"/> (chained, C# 10 extended property pattern).
	/// </summary>
	private static ExpressionSyntax? BuildMemberPath(ExpressionSyntax whenNotNull) => whenNotNull switch
	{
		// ?.Prop → just the identifier name
		MemberBindingExpressionSyntax memberBinding => memberBinding.Name,

		// ?.A.B → rebuild MemberBindingExpression(.A) as IdentifierName(A), yielding A.B
		MemberAccessExpressionSyntax { Expression: var innerExpr, Name: var name } =>
			BuildMemberPath(innerExpr) is { } left
				? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, left, name)
				: null,

		_ => null
	};

	/// <summary>
	/// Builds <c>sourceExpr is T { memberPath: valuePattern }</c>.
	/// Uses <see cref="NameColonSyntax"/> for a single identifier and
	/// <see cref="ExpressionColonSyntax"/> for extended property paths (C# 10).
	/// </summary>
	private static IsPatternExpressionSyntax BuildPropertyPatternExpression(
		ExpressionSyntax sourceExpr,
		TypeSyntax type,
		ExpressionSyntax memberPath,
		PatternSyntax valuePattern)
	{
		var nameOrExpressionColon = memberPath is IdentifierNameSyntax idName
			? (BaseExpressionColonSyntax) NameColon(idName)
			: ExpressionColon(memberPath, Token(SyntaxKind.ColonToken));

		var subpattern = Subpattern(nameOrExpressionColon, valuePattern);

		var propertyPattern = RecursivePattern()
			.WithType(type.WithoutTrivia())
			.WithPropertyPatternClause(
				PropertyPatternClause(SeparatedList([ subpattern ])));

		return IsPatternExpression(sourceExpr.WithoutTrivia(), propertyPattern);
	}

	/// <summary>
	/// Converts a chain of equality comparisons connected by <c>||</c> to a disjunctive
	/// <c>or</c> pattern (C# 9+).
	///
	/// <c>x == 0 || x == 20 || x == 40 || x == 60 || x == 80</c>
	/// → <c>x is 0 or 20 or 40 or 60 or 80</c>
	///
	/// Without a <paramref name="semanticModel"/>, only literal constants are accepted on the
	/// right-hand side. When a <paramref name="semanticModel"/> is supplied, named constants
	/// and enum members are also accepted.
	/// </summary>
	public static bool TryConvertOrEqualityChainToOrPattern(
		BinaryExpressionSyntax orChain,
		SemanticModel? semanticModel,
		Func<ExpressionSyntax, ExpressionSyntax> visit,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result)
	{
		result = null;

		if (!orChain.IsKind(SyntaxKind.LogicalOrExpression))
		{
			return false;
		}

		ExpressionSyntax? testedExpr = null;

		if (!TryBuildOrPattern(orChain, ref testedExpr, out var pattern, visit, semanticModel))
		{
			return false;
		}

		// Require at least two alternatives — a bare ConstantPattern means only one branch was found
		if (pattern is not BinaryPatternSyntax)
		{
			return false;
		}

		result = IsPatternExpression(testedExpr!.WithoutTrivia(), pattern)
			.WithTriviaFrom(orChain);
		return true;
	}

	/// <summary>
	/// Recursively walks a <c>||</c> chain and builds the corresponding <c>or</c> pattern,
	/// verifying that all equality comparisons test the same expression.
	/// </summary>
	private static bool TryBuildOrPattern(
		ExpressionSyntax expr,
		ref ExpressionSyntax? testedExpr,
		[NotNullWhen(true)] out PatternSyntax? pattern,
		Func<ExpressionSyntax, ExpressionSyntax> visit,
		SemanticModel? semanticModel)
	{
		pattern = null;

		// Recurse into || nodes — left-associative, so (a || b) || c → (pat_a or pat_b) or pat_c
		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } orExpr)
		{
			if (!TryBuildOrPattern(orExpr.Left, ref testedExpr, out var leftPat, visit, semanticModel))
			{
				return false;
			}

			if (!TryBuildOrPattern(orExpr.Right, ref testedExpr, out var rightPat, visit, semanticModel))
			{
				return false;
			}

			pattern = BinaryPattern(SyntaxKind.OrPattern, leftPat, rightPat);
			return true;
		}

		// Leaf: a single equality comparison
		return TryBuildOrPatternLeaf(expr, ref testedExpr, out pattern, visit, semanticModel);
	}

	/// <summary>
	/// Handles the leaf case of a <c>||</c> chain: <c>expr == constant</c> or <c>constant == expr</c>.
	/// </summary>
	private static bool TryBuildOrPatternLeaf(
		ExpressionSyntax expr,
		ref ExpressionSyntax? testedExpr,
		[NotNullWhen(true)] out PatternSyntax? pattern,
		Func<ExpressionSyntax, ExpressionSyntax> visit,
		SemanticModel? semanticModel)
	{
		pattern = null;

		if (expr is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } eqExpr)
		{
			return false;
		}

		ExpressionSyntax tested, constant;
		
		var leftVisited = visit(eqExpr.Left);
		var rightVisited = visit(eqExpr.Right);

		if (IsConstantLike(rightVisited, semanticModel))
		{
			tested = leftVisited;
			constant = rightVisited;
		}
		else if (IsConstantLike(eqExpr.Left, semanticModel))
		{
			tested = rightVisited;
			constant = leftVisited;
		}
		else
		{
			return false;
		}

		if (testedExpr is null)
		{
			testedExpr = tested;
		}
		else if (testedExpr.GetDeterministicHash() != tested.GetDeterministicHash())
		{
			return false; // Different expressions on the left-hand side
		}

		pattern = ConstantPattern(constant.WithoutTrivia());
		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="expr"/> can safely be used as a
	/// constant pattern value.
	/// <list type="bullet">
	///   <item>Always: plain literals (<c>0</c>, <c>"hi"</c>, <c>true</c>, <c>null</c>, …)</item>
	///   <item>Always: signed literals (<c>-1</c>, <c>+1</c>)</item>
	///   <item>With <paramref name="semanticModel"/>: any compile-time constant (enum members, <c>const</c> fields, …)</item>
	/// </list>
	/// </summary>
	private static bool IsConstantLike(ExpressionSyntax expr, SemanticModel? semanticModel)
	{
		if (expr is LiteralExpressionSyntax)
		{
			return true;
		}

		if (expr is PrefixUnaryExpressionSyntax
		    {
			    RawKind: (int) SyntaxKind.UnaryMinusExpression or (int) SyntaxKind.UnaryPlusExpression,
			    Operand: LiteralExpressionSyntax
		    })
		{
			return true;
		}

		return semanticModel?.GetConstantValue(expr).HasValue == true;
	}

	// ─────────────────────────────────────────────────────────────
	// Null-check pattern conversions  (UseIsNullCheck suite)
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Converts a null equality or inequality comparison to an is-null / is-not-null pattern (C# 7+).
	/// Inspired by the Roslyn <c>UseIsNullCheck</c> analyzer suite.
	///
	/// <list type="bullet">
	///   <item><c>x == null</c>         → <c>x is null</c></item>
	///   <item><c>x != null</c>         → <c>x is not null</c></item>
	///   <item><c>(object)x == null</c> → <c>x is null</c></item>
	///   <item><c>(object)x != null</c> → <c>x is not null</c></item>
	/// </list>
	/// </summary>
	public static bool TryConvertNullComparisonToNullPattern(
		BinaryExpressionSyntax comparison,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result)
	{
		result = null;

		if (!comparison.IsKind(SyntaxKind.EqualsExpression) &&
		    !comparison.IsKind(SyntaxKind.NotEqualsExpression))
		{
			return false;
		}

		ExpressionSyntax subject;

		if (comparison.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression })
		{
			subject = comparison.Left;
		}
		else if (comparison.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression })
		{
			subject = comparison.Right;
		}
		else
		{
			return false;
		}

		// Unwrap a cast to object: (object)x == null → x is null
		if (subject is CastExpressionSyntax
		    {
			    Type: PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.ObjectKeyword },
			    Expression: var inner
		    })
		{
			subject = inner;
		}

		var nullPattern = ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression));

		PatternSyntax pattern = comparison.IsKind(SyntaxKind.EqualsExpression)
			? nullPattern
			: UnaryPattern(Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace), nullPattern);

		result = IsPatternExpression(subject.WithoutTrivia(), pattern)
			.WithTriviaFrom(comparison);
		return true;
	}

	/// <summary>
	/// Converts a redundant <c>is object</c> type check to an <c>is not null</c> pattern (C# 9+).
	/// Inspired by the Roslyn <c>CSharpUseNullCheckOverTypeCheckDiagnosticAnalyzer</c>.
	///
	/// <c>x is object</c>  →  <c>x is not null</c>
	///
	/// <para>
	/// Without a <paramref name="semanticModel"/> the conversion is performed purely syntactically.
	/// Supply a model to enforce that <c>x</c> is a reference type (or reference-constrained generic),
	/// matching the stricter semantics of the Roslyn analyzer.
	/// </para>
	/// </summary>
	public static bool TryConvertIsObjectToIsNotNullPattern(
		BinaryExpressionSyntax isExpression,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result,
		SemanticModel? semanticModel = null)
	{
		result = null;

		if (!isExpression.IsKind(SyntaxKind.IsExpression))
		{
			return false;
		}

		if (isExpression.Right is not PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.ObjectKeyword })
		{
			return false;
		}

		// With a semantic model verify it's a reference type (not an unconstrained generic)
		if (semanticModel is not null)
		{
			var leftType = semanticModel.GetTypeInfo(isExpression.Left).Type;

			if (leftType is null || !leftType.IsReferenceType)
			{
				return false;
			}

			if (leftType is ITypeParameterSymbol { HasReferenceTypeConstraint: false })
			{
				return false;
			}
		}

		result = IsPatternExpression(
				isExpression.Left.WithoutTrivia(),
				UnaryPattern(
					Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace),
					ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression))))
			.WithTriviaFrom(isExpression);
		return true;
	}

	// ─────────────────────────────────────────────────────────────
	// && chain → and-pattern  (UsePatternCombinators suite)
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Converts a chain of comparisons connected by <c>&amp;&amp;</c> to a conjunctive
	/// <c>and</c> pattern (C# 9+).
	/// Inspired by the Roslyn <c>CSharpUsePatternCombinatorsDiagnosticAnalyzer</c>.
	///
	/// <list type="bullet">
	///   <item><c>x >= 0 &amp;&amp; x &lt;= 100</c>           → <c>x is >= 0 and &lt;= 100</c></item>
	///   <item><c>x != 0 &amp;&amp; x != 20</c>               → <c>x is not 0 and not 20</c></item>
	///   <item><c>x > 0 &amp;&amp; x is &lt; 100</c>          → <c>x is > 0 and &lt; 100</c></item>
	///   <item><c>x != null &amp;&amp; x.Length > 0</c>        → ✗ (different expressions — rejected)</item>
	/// </list>
	///
	/// Supports leaves that are: <c>==</c>, <c>!=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>,
	/// <c>&lt;=</c>, <c>is T</c> (plain type check), and <c>is pattern</c> expressions.
	///
	/// Without a <paramref name="semanticModel"/>, only literal constants are accepted as
	/// pattern values. Pass one to also accept enum members and <c>const</c> fields.
	/// </summary>
	public static bool TryConvertAndChainToAndPattern(
		BinaryExpressionSyntax andChain,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result,
		SemanticModel? semanticModel = null)
	{
		result = null;

		if (!andChain.IsKind(SyntaxKind.LogicalAndExpression))
		{
			return false;
		}

		ExpressionSyntax? testedExpr = null;

		if (!TryBuildAndPattern(andChain, ref testedExpr, out var pattern, semanticModel))
		{
			return false;
		}

		// Require at least two leaves
		if (pattern is not BinaryPatternSyntax)
		{
			return false;
		}

		result = IsPatternExpression(testedExpr!.WithoutTrivia(), pattern)
			.WithTriviaFrom(andChain);
		return true;
	}

	/// <summary>
	/// Recursively walks a <c>&amp;&amp;</c> chain and builds the corresponding <c>and</c> pattern.
	/// </summary>
	private static bool TryBuildAndPattern(
		ExpressionSyntax expr,
		ref ExpressionSyntax? testedExpr,
		[NotNullWhen(true)] out PatternSyntax? pattern,
		SemanticModel? semanticModel)
	{
		pattern = null;

		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } andExpr)
		{
			if (!TryBuildAndPattern(andExpr.Left, ref testedExpr, out var leftPat, semanticModel))
			{
				return false;
			}

			if (!TryBuildAndPattern(andExpr.Right, ref testedExpr, out var rightPat, semanticModel))
			{
				return false;
			}

			pattern = BinaryPattern(SyntaxKind.AndPattern, leftPat, rightPat);
			return true;
		}

		return TryBuildAndPatternLeaf(expr, ref testedExpr, out pattern, semanticModel);
	}

	/// <summary>
	/// Handles a single leaf of a <c>&amp;&amp;</c> chain, producing the corresponding pattern.
	/// </summary>
	private static bool TryBuildAndPatternLeaf(
		ExpressionSyntax expr,
		ref ExpressionSyntax? testedExpr,
		[NotNullWhen(true)] out PatternSyntax? pattern,
		SemanticModel? semanticModel)
	{
		pattern = null;

		// expr is pattern  →  reuse the existing pattern directly
		if (expr is IsPatternExpressionSyntax { Expression: var isLeft } isPatternExpr)
		{
			return TryRecordLeaf(isLeft, isPatternExpr.Pattern, ref testedExpr, out pattern);
		}

		if (expr is not BinaryExpressionSyntax binaryLeaf)
		{
			return false;
		}

		PatternSyntax? leafPat;
		ExpressionSyntax tested;

		switch ((SyntaxKind) binaryLeaf.RawKind)
		{
			// expr == constant  →  constant
			case SyntaxKind.EqualsExpression
				when TryNormalizeBinaryLeaf(binaryLeaf, semanticModel, out tested, out var eqConst):
				leafPat = ConstantPattern(eqConst.WithoutTrivia());
				break;

			// expr != constant  →  not constant
			case SyntaxKind.NotEqualsExpression
				when TryNormalizeBinaryLeaf(binaryLeaf, semanticModel, out tested, out var neqConst):
				leafPat = UnaryPattern(
					Token(SyntaxKind.NotKeyword).WithTrailingTrivia(ElasticSpace),
					ConstantPattern(neqConst.WithoutTrivia()));
				break;

			// expr >= / <= / > / < constant  →  relational pattern
			case SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
				or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
				when TryBuildRelationalPattern(binaryLeaf, semanticModel, out tested, out leafPat):
				break;

			// expr is T  →  TypePattern(T)
			case SyntaxKind.IsExpression when binaryLeaf.Right is TypeSyntax isType:
				tested = binaryLeaf.Left;
				leafPat = TypePattern(isType.WithoutTrivia());
				break;

			default:
				return false;
		}

		return TryRecordLeaf(tested, leafPat, ref testedExpr, out pattern);
	}

	/// <summary>
	/// Records a leaf's tested expression (verifying it matches previous leaves) and returns
	/// the produced pattern.
	/// </summary>
	private static bool TryRecordLeaf(
		ExpressionSyntax tested,
		PatternSyntax leafPattern,
		ref ExpressionSyntax? testedExpr,
		[NotNullWhen(true)] out PatternSyntax? pattern)
	{
		pattern = null;

		if (testedExpr is null)
		{
			testedExpr = tested;
		}
		else if (testedExpr.GetDeterministicHash() != tested.GetDeterministicHash())
		{
			return false;
		}

		pattern = leafPattern;
		return true;
	}

	/// <summary>
	/// Normalises a binary equality or inequality expression to <c>(tested, constant)</c>,
	/// regardless of which side the constant is on.
	/// </summary>
	private static bool TryNormalizeBinaryLeaf(
		BinaryExpressionSyntax binary,
		SemanticModel? semanticModel,
		out ExpressionSyntax tested,
		out ExpressionSyntax constant)
	{
		if (IsConstantLike(binary.Right, semanticModel))
		{
			tested = binary.Left;
			constant = binary.Right;
			return true;
		}

		if (IsConstantLike(binary.Left, semanticModel))
		{
			tested = binary.Right;
			constant = binary.Left;
			return true;
		}

		tested = constant = null!;
		return false;
	}

	/// <summary>
	/// Builds a <see cref="RelationalPatternSyntax"/> from a relational binary expression,
	/// flipping the operator when the constant is on the left-hand side.
	/// </summary>
	private static bool TryBuildRelationalPattern(
		BinaryExpressionSyntax binary,
		SemanticModel? semanticModel,
		out ExpressionSyntax tested,
		[NotNullWhen(true)] out PatternSyntax? pattern)
	{
		tested = null!;
		pattern = null;

		SyntaxKind opKind = (SyntaxKind) binary.RawKind;
		ExpressionSyntax constantExpr;
		SyntaxKind tokenKind;

		if (IsConstantLike(binary.Right, semanticModel))
		{
			tested = binary.Left;
			constantExpr = binary.Right;
			tokenKind = ToRelationalToken(opKind);
		}
		else if (IsConstantLike(binary.Left, semanticModel))
		{
			tested = binary.Right;
			constantExpr = binary.Left;
			tokenKind = FlipRelationalToken(opKind); // constant on left → flip operator
		}
		else
		{
			return false;
		}

		if (tokenKind == SyntaxKind.None)
		{
			return false;
		}

		pattern = RelationalPattern(Token(tokenKind), constantExpr.WithoutTrivia());
		return true;
	}

	private static SyntaxKind ToRelationalToken(SyntaxKind opKind) => opKind switch
	{
		SyntaxKind.GreaterThanExpression => SyntaxKind.GreaterThanToken,
		SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.GreaterThanEqualsToken,
		SyntaxKind.LessThanExpression => SyntaxKind.LessThanToken,
		SyntaxKind.LessThanOrEqualExpression => SyntaxKind.LessThanEqualsToken,
		_ => SyntaxKind.None
	};

	private static SyntaxKind FlipRelationalToken(SyntaxKind opKind) => opKind switch
	{
		SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanToken, // 5 > x  → x is < 5
		SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanEqualsToken, // 5 >= x → x is <= 5
		SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanToken, // 5 < x  → x is > 5
		SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanEqualsToken, // 5 <= x → x is >= 5
		_ => SyntaxKind.None
	};

	/// <summary>
	/// Returns <see langword="true"/> when the expression is a null check on a specific variable:
	/// <c>x != null</c> or <c>x is not null</c>.
	/// </summary>
	private static bool IsNullCheckOnVariable(ExpressionSyntax condition, string variableName)
	{
		switch (condition)
		{
			// x != null
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.NotEqualsExpression, Left: IdentifierNameSyntax leftId } binary
				when leftId.Identifier.ValueText == variableName
				     && binary.Right is LiteralExpressionSyntax rightLit
				     && rightLit.IsKind(SyntaxKind.NullLiteralExpression):
				return true;
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.NotEqualsExpression, Right: IdentifierNameSyntax rightId } binary
				when rightId.Identifier.ValueText == variableName
				     && binary.Left is LiteralExpressionSyntax leftLit
				     && leftLit.IsKind(SyntaxKind.NullLiteralExpression):
			// x is not null
			case IsPatternExpressionSyntax { Expression: IdentifierNameSyntax patternId } isPattern
				when patternId.Identifier.ValueText == variableName
				     && isPattern.Pattern is UnaryPatternSyntax notPattern
				     && notPattern.IsKind(SyntaxKind.NotPattern)
				     && notPattern.Pattern is ConstantPatternSyntax constPattern
				     && constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression):
				return true;
			default:
				return false;
		}

	}
}