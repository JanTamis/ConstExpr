using System;
using System.Collections.Concurrent;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

public class VectorizerRewriter(
	SemanticModel semanticModel,
	ITypeSymbol vectorType,
	ConcurrentDictionary<ulong, ISymbol> symbolStore) : CSharpSyntaxRewriter
{
	private readonly ITypeSymbol _VectorType = semanticModel.Compilation.GetTypeByMetadataName("System.Numerics.Vector")!;

	// ── Leaf nodes ────────────────────────────────────────────────────────────

	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		var typeSyntax = ParseTypeName(vectorType.ToDisplayString());
		var vectorTyped = GenericName(Identifier("Vector"))
			.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(typeSyntax)));

		var tokenValue = node.Token.Value;

		if (IsZero(tokenValue))
			return MemberAccessExpression(vectorTyped, IdentifierName("Zero"));

		if (IsOne(tokenValue))
			return MemberAccessExpression(vectorTyped, IdentifierName("One"));

		// -1 (all bits set) is the canonical all-ones SIMD mask.
		if (IsAllBitsSet(tokenValue))
			return MemberAccessExpression(vectorTyped, IdentifierName("AllBitsSet"));

		return CreateInvocation("Create", node);
	}

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return node;
		//return CreateInvocation("Create", node);
	}

	/// <summary>
	///   Wraps static constant fields and read-only properties (e.g. <c>int.MaxValue</c>,
	///   <c>float.NaN</c>) in <c>Vector.Create(...)</c> as a lane broadcast.
	///   Expressions that already produce a Vector type (e.g. <c>Vector&lt;int&gt;.Zero</c>)
	///   are left untouched.
	/// </summary>
	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		return CreateInvocation("Create", node);
	}

	/// <summary>
	///   Translates <c>default(T)</c> to <c>Vector&lt;T&gt;.Zero</c>.
	/// </summary>
	public override SyntaxNode? VisitDefaultExpression(DefaultExpressionSyntax node)
	{
		// default(T) → Vector<T>.Zero
		return MemberAccessExpression(
			GenericName(Identifier("Vector"))
				.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(node.Type))),
			IdentifierName("Zero"));
	}

	/// <summary>
	///   Strips <c>checked</c> / <c>unchecked</c> wrappers — overflow semantics have no
	///   meaning in SIMD intrinsics.
	/// </summary>
	public override SyntaxNode? VisitCheckedExpression(CheckedExpressionSyntax node)
	{
		return Visit(node.Expression);
	}

	// ── Cast ─────────────────────────────────────────────────────────────────

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		// Resolve the source type BEFORE visiting so we still reference the original syntax tree.
		semanticModel.TryGetTypeSymbol(node.Expression, symbolStore, out var sourceType);

		var expression = (ExpressionSyntax) Visit(node.Expression);
		var sourceIsFloat = IsFloatingPoint(sourceType);

		// Choose the correct Vector128 method based on both source and target type:
		//  - float↔integer  → ConvertTo* (actual numeric conversion)
		//  - integer↔integer → As*        (bitwise reinterpretation)
		var methodName = node.Type.ToString() switch
		{
			// Target is floating-point: always a numeric conversion from integer.
			"float" or "Single" => "ConvertToSingleNative",
			"double" or "Double" => "ConvertToDoubleNative",
			// Target is integer: numeric conversion when source is floating-point, reinterpret otherwise.
			"int" or "Int32" => sourceIsFloat ? "ConvertToInt32Native" : "AsVectorInt32",
			"uint" or "UInt32" => sourceIsFloat ? "ConvertToUInt32Native" : "AsVectorUInt32",
			"long" or "Int64" => sourceIsFloat ? "ConvertToInt64Native" : "AsVectorInt64",
			"ulong" or "UInt64" => sourceIsFloat ? "ConvertToUInt64Native" : "AsVectorUInt64",
			"short" or "Int16" => "AsVectorInt16",
			"ushort" or "UInt16" => "AsVectorUInt16",
			"byte" or "Byte" => "AsVectorByte",
			"sbyte" or "SByte" => "AsVectorSByte",
			_ => null
		};

		if (methodName != null)
			return CreateInvocation(methodName, StripParenthesis(expression));

		// For other casts, preserve the cast with the rewritten inner expression.
		return node.WithExpression(expression);
	}

	// ── Unary operators ───────────────────────────────────────────────────────

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		var operand = (ExpressionSyntax) Visit(node.Operand);

		return node.Kind() switch
		{
			// Arithmetic negation: -x → Vector.Negate(x)
			SyntaxKind.UnaryMinusExpression => CreateInvocation("Negate", operand),
			// Bitwise complement: ~x → Vector.OnesComplement(x)
			SyntaxKind.BitwiseNotExpression => CreateInvocation("OnesComplement", operand),
			// Logical NOT on a mask vector: !x → Vector.OnesComplement(x)
			SyntaxKind.LogicalNotExpression => CreateInvocation("OnesComplement", operand),
			// Unary plus is a no-op; just keep the rewritten operand.
			SyntaxKind.UnaryPlusExpression => operand,
			_ => node.WithOperand(operand)
		};
	}

	// ── Structural / grouping ─────────────────────────────────────────────────

	public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		var inner = (ExpressionSyntax) Visit(node.Expression);
		return node.WithExpression(inner);
	}

	// ── Conditional (ternary) ────────────────────────────────────────────────

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		// condition ? whenTrue : whenFalse → Vector.ConditionalSelect(condition, whenTrue, whenFalse)
		var condition = (ExpressionSyntax) Visit(node.Condition);
		var whenTrue = (ExpressionSyntax) Visit(node.WhenTrue);
		var whenFalse = (ExpressionSyntax) Visit(node.WhenFalse);

		return CreateInvocation("ConditionalSelect", condition, whenTrue, whenFalse);
	}

	// ── Method invocations ────────────────────────────────────────────────────

	/// <summary>
	///   Maps vectorizable <c>Math</c> / <c>MathF</c> method calls to their
	///   <c>Vector&lt;T&gt;</c> equivalents.
	/// </summary>
	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (node.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.Text;

			if (_VectorType.HasMethod(methodName))
			{
				var rewrittenArgs = node.ArgumentList.Arguments
					.Select(a => (ExpressionSyntax) Visit(a.Expression))
					.ToArray();

				return CreateInvocation(methodName, rewrittenArgs);
			}
		}

		return base.VisitInvocationExpression(node);
	}

	// ── Binary operators ──────────────────────────────────────────────────────

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		// Optimise   a & ~b   →   Vector.AndNot(a, b)   before recursing.
		if (node.IsKind(SyntaxKind.BitwiseAndExpression)
		    && node.Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseNotExpression } notExpr)
		{
			var left = (ExpressionSyntax) Visit(node.Left);
			var right = (ExpressionSyntax) Visit(notExpr.Operand);

			return CreateInvocation("AndNot", left, right);
		}

		var rewrittenLeft = (ExpressionSyntax) Visit(node.Left);
		var rewrittenRight = (ExpressionSyntax) Visit(node.Right);

		return node.Kind() switch
		{
			// Element-wise arithmetic / bitwise — operator syntax is kept as-is because
			// Vector<T> overloads +, -, *, /, %, &, |, ^ natively.
			SyntaxKind.AddExpression
				or SyntaxKind.SubtractExpression
				or SyntaxKind.MultiplyExpression
				or SyntaxKind.DivideExpression
				or SyntaxKind.ModuloExpression
				or SyntaxKind.BitwiseAndExpression
				or SyntaxKind.BitwiseOrExpression
				or SyntaxKind.ExclusiveOrExpression => node.WithLeft(rewrittenLeft).WithRight(rewrittenRight),

			// Logical AND / OR on mask vectors: rewrite to bitwise AND / OR.
			SyntaxKind.LogicalAndExpression =>
				BinaryExpression(SyntaxKind.BitwiseAndExpression, rewrittenLeft, rewrittenRight),
			SyntaxKind.LogicalOrExpression =>
				BinaryExpression(SyntaxKind.BitwiseOrExpression, rewrittenLeft, rewrittenRight),

			// Shift count is always a scalar — Vector<T> overloads <<, >> (arithmetic for signed /
			// logical for unsigned) and >>> (logical) directly, so keep operator syntax and rewrite
			// only the shifted value. ponytail: scalar shift counts only — a per-element count
			// (x << y) has no Vector<T> << Vector<T> overload and isn't supported here.
			SyntaxKind.LeftShiftExpression
				or SyntaxKind.RightShiftExpression
				or SyntaxKind.UnsignedRightShiftExpression => node.WithLeft(rewrittenLeft),

			// Comparisons — no operator overload exists; use the Vector API directly.
			SyntaxKind.GreaterThanExpression => CreateInvocation("GreaterThan", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),
			SyntaxKind.GreaterThanOrEqualExpression => CreateInvocation("GreaterThanOrEqual", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),
			SyntaxKind.LessThanExpression => CreateInvocation("LessThan", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),
			SyntaxKind.LessThanOrEqualExpression => CreateInvocation("LessThanOrEqual", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),
			SyntaxKind.EqualsExpression => CreateInvocation("Equals", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),
			SyntaxKind.NotEqualsExpression => CreateInvocation("NotEquals", [ ParseTypeName(vectorType.ToDisplayString()) ], rewrittenLeft, rewrittenRight),

			_ => node.WithLeft(rewrittenLeft).WithRight(rewrittenRight)
		};
	}

	// ── Factory helpers ───────────────────────────────────────────────────────

	private static InvocationExpressionSyntax CreateInvocation(string name, params ExpressionSyntax[] arguments)
	{
		return InvocationExpression(MemberAccessExpression(IdentifierName("Vector"), IdentifierName(name)))
			.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
	}

	private static InvocationExpressionSyntax CreateInvocation(string name, TypeSyntax[] typeArguments, params ExpressionSyntax[] arguments)
	{
		ExpressionSyntax memberAccess = typeArguments.Length > 0
			? MemberAccessExpression(
				IdentifierName("Vector"),
				GenericName(Identifier(name))
					.WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArguments))))
			: MemberAccessExpression(IdentifierName("Vector"), IdentifierName(name));

		return InvocationExpression(memberAccess)
			.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
	}

	// ── Type helpers ──────────────────────────────────────────────────────────

	/// <summary>Returns <see langword="true" /> when <paramref name="type" /> is <c>float</c> or <c>double</c>.</summary>
	private static bool IsFloatingPoint(ITypeSymbol? type)
	{
		return type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double;
	}

	/// <summary>Returns <see langword="true" /> when <paramref name="value" /> represents a numeric zero.</summary>
	private static bool IsZero(object? value)
	{
		return value switch
		{
			int i => i == 0,
			uint u => u == 0u,
			long l => l == 0L,
			ulong ul => ul == 0UL,
			short s => s == 0,
			ushort us => us == 0,
			byte b => b == 0,
			sbyte sb => sb == 0,
			float f => f == 0f,
			double d => d == 0.0,
			_ => false
		};
	}

	/// <summary>Returns <see langword="true" /> when <paramref name="value" /> represents the multiplicative identity one.</summary>
	private static bool IsOne(object? value)
	{
		return value switch
		{
			int i => i == 1,
			uint u => u == 1u,
			long l => l == 1L,
			ulong ul => ul == 1UL,
			short s => s == 1,
			ushort us => us == 1,
			byte b => b == 1,
			sbyte sb => sb == 1,
			float f => f == 1f,
			double d => d == 1.0,
			_ => false
		};
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="value" /> has all bits set
	///   (i.e. <c>-1</c> for signed types or the maximum unsigned value), which maps to
	///   <c>Vector&lt;T&gt;.AllBitsSet</c> — the canonical all-ones SIMD mask.
	/// </summary>
	private static bool IsAllBitsSet(object? value)
	{
		return value switch
		{
			int i => i == -1,
			long l => l == -1L,
			short s => s == -1,
			sbyte sb => sb == -1,
			uint u => u == UInt32.MaxValue,
			ulong ul => ul == UInt64.MaxValue,
			ushort us => us == UInt16.MaxValue,
			byte b => b == Byte.MaxValue,
			_ => false
		};
	}

	private static ExpressionSyntax StripParenthesis(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax paren)
		{
			expression = paren.Expression;
		}

		return expression;
	}
}