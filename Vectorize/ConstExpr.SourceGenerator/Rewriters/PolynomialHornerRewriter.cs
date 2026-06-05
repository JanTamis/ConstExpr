using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Rewrites a polynomial in a single variable into Horner form, reducing the number of
///   multiplications and exposing fused-multiply-add opportunities:
///   <code>
///   a*x*x + b*x + c   =>   (a*x + b)*x + c
/// </code>
///   Runs as a pre-pass (before binary optimization) so the FMA strategy can later contract the
///   nested multiplies into <c>FusedMultiplyAdd(FusedMultiplyAdd(a, x, b), x, c)</c>.
///   Conservative by design — it only fires when:
///   <list type="bullet">
///     <item>the additive expression has a built-in numeric (ring) type (never string concatenation),</item>
///     <item>exactly one variable yields a clean degree ≥ 2 decomposition (each term is coef·xᵏ, coef x-free),</item>
///     <item>each power appears in at most one term (no like-terms to merge).</item>
///   </list>
///   Gated on <see cref="FastMathFlags.HornerPolynomial" /> because the reassociation can change
///   floating-point rounding.
/// </summary>
public sealed class PolynomialHornerRewriter(SemanticModel model, FastMathFlags flags) : CSharpSyntaxRewriter
{
	public static SyntaxNode Apply(SemanticModel model, FastMathFlags flags, SyntaxNode node)
	{
		return flags.HasFlag(FastMathFlags.HornerPolynomial)
			? new PolynomialHornerRewriter(model, flags).Visit(node)
			: node;
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		// Only the outermost additive node of a polynomial is rewritten; inner +/- nodes are
		// folded into it. A node whose parent is also +/- is "inner" and skipped here.
		if (IsAdditive(node)
		    && !(node.Parent is BinaryExpressionSyntax parent && IsAdditive(parent))
		    && TryRewritePolynomial(node, out var horner))
		{
			return horner;
		}

		return base.VisitBinaryExpression(node);
	}

	private static bool IsAdditive(ExpressionSyntax node)
	{
		return node.IsKind(SyntaxKind.AddExpression) || node.IsKind(SyntaxKind.SubtractExpression);
	}

	private bool TryRewritePolynomial(BinaryExpressionSyntax node, out ExpressionSyntax result)
	{
		result = node;

		// Must be a built-in numeric (ring) type — never string concatenation or custom operators.
		if (model.GetTypeInfo(node).Type is not { } type || !IsSupportedNumeric(type.SpecialType))
		{
			return false;
		}

		var terms = new List<(int Sign, ExpressionSyntax Expr)>();
		FlattenAdditive(node, 1, terms);

		if (terms.Count < 2)
		{
			return false;
		}

		var candidates = node.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Select(id => id.Identifier.Text)
			.Distinct()
			.ToList();

		string? chosenVar = null;
		Dictionary<int, (int Sign, ExpressionSyntax Coef)>? chosenCoeffs = null;

		foreach (var v in candidates)
		{
			if (!TryDecompose(terms, v, out var coeffs, out var maxDegree) || maxDegree < 2)
			{
				continue;
			}

			if (chosenVar is not null)
			{
				// More than one variable yields a degree ≥ 2 polynomial — ambiguous, bail.
				return false;
			}

			chosenVar = v;
			chosenCoeffs = coeffs;
		}

		if (chosenVar is null || chosenCoeffs is null)
		{
			return false;
		}

		result = BuildHorner(chosenVar, chosenCoeffs, type);
		return true;
	}

	private static void FlattenAdditive(ExpressionSyntax expr, int sign, List<(int Sign, ExpressionSyntax Expr)> terms)
	{
		expr = Unwrap(expr);

		switch (expr)
		{
			case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.AddExpression):
				FlattenAdditive(b.Left, sign, terms);
				FlattenAdditive(b.Right, sign, terms);
				break;
			case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.SubtractExpression):
				FlattenAdditive(b.Left, sign, terms);
				FlattenAdditive(b.Right, -sign, terms);
				break;
			default:
				terms.Add((sign, expr));
				break;
		}
	}

	private bool TryDecompose(
		List<(int Sign, ExpressionSyntax Expr)> terms,
		string variable,
		out Dictionary<int, (int Sign, ExpressionSyntax Coef)> coeffs,
		out int maxDegree)
	{
		coeffs = new Dictionary<int, (int, ExpressionSyntax)>();
		maxDegree = 0;

		foreach (var (sign, term) in terms)
		{
			if (!TryMonomial(term, variable, out var degree, out var coef))
			{
				return false;
			}

			// Conservative: each power must appear in exactly one term (no like-terms to merge).
			if (coeffs.ContainsKey(degree))
			{
				return false;
			}

			coeffs[degree] = (sign, coef);

			if (degree > maxDegree)
			{
				maxDegree = degree;
			}
		}

		return true;
	}

	private bool TryMonomial(ExpressionSyntax term, string variable, out int degree, out ExpressionSyntax coef)
	{
		degree = 0;
		coef = null!;

		var factors = new List<ExpressionSyntax>();
		FlattenMultiplicative(term, factors);

		var coefFactors = new List<ExpressionSyntax>();

		foreach (var factor in factors)
		{
			var f = Unwrap(factor);

			if (f is IdentifierNameSyntax id && id.Identifier.Text == variable)
			{
				degree++;
			}
			else if (!ContainsVariable(f, variable))
			{
				coefFactors.Add(f);
			}
			else
			{
				// A factor involves the variable but is not a bare occurrence (e.g. (x + 1) or f(x)).
				return false;
			}
		}

		coef = coefFactors.Count switch
		{
			0 => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)),
			1 => coefFactors[0],
			_ => coefFactors.Skip(1).Aggregate(coefFactors[0], MultiplyExpression)
		};

		return true;
	}

	private static void FlattenMultiplicative(ExpressionSyntax expr, List<ExpressionSyntax> factors)
	{
		expr = Unwrap(expr);

		if (expr is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.MultiplyExpression))
		{
			FlattenMultiplicative(b.Left, factors);
			FlattenMultiplicative(b.Right, factors);
		}
		else
		{
			factors.Add(expr);
		}
	}

	private ExpressionSyntax BuildHorner(string variable, Dictionary<int, (int Sign, ExpressionSyntax Coef)> coeffs, ITypeSymbol type)
	{
		var maxDegree = coeffs.Keys.Max();
		var v = IdentifierName(variable);

		// When FMA is enabled and the type exposes FusedMultiplyAdd, emit the contracted form
		// directly (the binary FMA strategy cannot see these freshly-built nodes).
		var useFma = flags.HasFlag(FastMathFlags.FusedMultiplyAdd) && HasFusedMultiplyAdd(type);
		var host = ParseName(type.Name);

		var (topSign, topCoef) = coeffs[maxDegree];
		var acc = topSign < 0 ? Negate(topCoef) : topCoef;

		for (var d = maxDegree - 1; d >= 0; d--)
		{
			var hasCoef = coeffs.TryGetValue(d, out var entry);
			var addend = hasCoef ? entry.Sign < 0 ? Negate(entry.Coef) : entry.Coef : null;

			if (useFma && addend is not null)
			{
				// host.FusedMultiplyAdd(acc, x, addend)
				acc = InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd")))
					.WithArgumentList(ArgumentList(SeparatedList([ Argument(acc), Argument(v), Argument(addend) ])));

				continue;
			}

			acc = MultiplyExpression(acc is BinaryExpressionSyntax ? ParenthesizedExpression(acc) : acc, v);

			if (addend is not null)
			{
				acc = entry.Sign < 0
					? SubtractExpression(acc, entry.Coef)
					: AddExpression(acc, entry.Coef);
			}
		}

		return acc;
	}

	private static bool HasFusedMultiplyAdd(ITypeSymbol type)
	{
		return type.HasMethod("FusedMultiplyAdd", m =>
			m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, type)));
	}

	private static ExpressionSyntax Negate(ExpressionSyntax expr)
	{
		return PrefixUnaryExpression(
			SyntaxKind.UnaryMinusExpression,
			expr is BinaryExpressionSyntax ? ParenthesizedExpression(expr) : expr);
	}

	private static bool ContainsVariable(ExpressionSyntax expr, string variable)
	{
		return expr.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Any(id => id.Identifier.Text == variable);
	}

	private static ExpressionSyntax Unwrap(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax paren)
		{
			expr = paren.Expression;
		}

		return expr;
	}

	private static bool IsSupportedNumeric(SpecialType type)
	{
		return type is SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Int32
			or SpecialType.System_Int64
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Byte
			or SpecialType.System_SByte;
	}
}