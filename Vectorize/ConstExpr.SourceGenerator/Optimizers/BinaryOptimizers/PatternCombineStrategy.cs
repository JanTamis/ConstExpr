using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

/// <summary>
///   Combines two comparable expressions over the same target into a single is-pattern combinator.
///   Handles all three left/right type combinations in one pass, inspired by Roslyn's
///   <c>CSharpUsePatternCombinatorsAnalyzer</c>:
///   <list type="bullet">
///     <item><c>x &gt; 0 &amp;&amp; x &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///     <item><c>x is &gt; 0 &amp;&amp; x &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///     <item><c>x is &gt; 0 &amp;&amp; x is &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///     <item><c>x &gt; 0 || x &lt; -10</c>  →  <c>x is &gt; 0 or &lt; -10</c></item>
///   </list>
///   Also handles reversed comparisons (<c>0 &lt; x</c>) that the previous strategies missed.
/// </summary>
public class PatternCombineStrategy(BinaryOperatorKind operatorKind) : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var left = TryParseAsPattern(context.Left.Syntax);

		if (left is null)
			return TryDeepCombine(context, out optimized);

		var right = TryParseAsPattern(context.Right.Syntax);

		if (right is null
		    || !LeftEqualsRight(left.Value.Target, right.Value.Target, context.Variables))
		{
			optimized = null;
			return false;
		}

		var patternKind = operatorKind switch
		{
			BinaryOperatorKind.ConditionalAnd => SyntaxKind.AndPattern,
			BinaryOperatorKind.ConditionalOr => SyntaxKind.OrPattern,
			_ => SyntaxKind.None
		};

		if (patternKind == SyntaxKind.None)
		{
			optimized = null;
			return false;
		}

		// Two identical patterns in an OR-combine would produce `x is null or null`.
		// This happens when alias analysis deems two different parameters equal (e.g. both
		// receive the same call-site argument). Merging them loses the second operand from
		// the generated method body, so bail out and keep the original || expression.
		if (patternKind == SyntaxKind.OrPattern
		    && AreEquivalent(left.Value.Pattern, right.Value.Pattern))
		{
			optimized = null;
			return false;
		}

		var combined = BinaryPattern(patternKind, left.Value.Pattern, right.Value.Pattern);
		optimized = IsPatternExpression(left.Value.Target, combined);

		return true;
	}

	// ponytail: handles one level of nesting; deeper chains re-enter via Visit
	private bool TryDeepCombine(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		var right = TryParseAsPattern(context.Right.Syntax);
		if (right is null)
			return false;

		var expectedKind = operatorKind == BinaryOperatorKind.ConditionalAnd
			? SyntaxKind.LogicalAndExpression
			: SyntaxKind.LogicalOrExpression;

		if (context.Left.Syntax is not BinaryExpressionSyntax leftBinary)
			return false;

		if (!leftBinary.IsKind(expectedKind))
			return false;

		var leftRight = TryParseAsPattern(leftBinary.Right);
		if (leftRight is null
		    || !LeftEqualsRight(leftRight.Value.Target, right.Value.Target, context.Variables))
			return false;

		var patternKind = operatorKind == BinaryOperatorKind.ConditionalAnd
			? SyntaxKind.AndPattern
			: SyntaxKind.OrPattern;

		var combined = BinaryPattern(patternKind, leftRight.Value.Pattern, right.Value.Pattern);

		optimized = BinaryExpression(expectedKind, leftBinary.Left,
			IsPatternExpression(leftRight.Value.Target, combined));
		return true;
	}
}