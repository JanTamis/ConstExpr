using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

/// <summary>
/// Combines two comparable expressions over the same target into a single is-pattern combinator.
/// Handles all three left/right type combinations in one pass, inspired by Roslyn's
/// <c>CSharpUsePatternCombinatorsAnalyzer</c>:
/// <list type="bullet">
///   <item><c>x &gt; 0 &amp;&amp; x &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///   <item><c>x is &gt; 0 &amp;&amp; x &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///   <item><c>x is &gt; 0 &amp;&amp; x is &lt; 10</c>  →  <c>x is &gt; 0 and &lt; 10</c></item>
///   <item><c>x &gt; 0 || x &lt; -10</c>  →  <c>x is &gt; 0 or &lt; -10</c></item>
/// </list>
/// Also handles reversed comparisons (<c>0 &lt; x</c>) that the previous strategies missed.
/// </summary>
public class PatternCombineStrategy(BinaryOperatorKind operatorKind) : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var left = TryParseAsPattern(context.Left.Syntax);
		
		if (left is null)
		{
			optimized = null;
			return false;
		}

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

		var combined = BinaryPattern(patternKind, left.Value.Pattern, right.Value.Pattern);
		optimized = IsPatternExpression(left.Value.Target, combined);
		
		return true;
	}
}

