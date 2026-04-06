using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

/// <summary>
/// Refactorer that inverts a conditional (ternary) expression by swapping the when-true
/// and when-false arms and negating the condition.
/// Inspired by the Roslyn <c>InvertConditionalCodeRefactoringProvider</c>.
///
/// <c>a ? b : c</c>  →  <c>!a ? c : b</c>
/// (with simplification of double-negation and relational operators)
/// </summary>
public static class InvertConditionalRefactoring
{
	/// <summary>
	/// Tries to invert a conditional expression by negating its condition and swapping the arms.
	/// </summary>
	public static bool TryInvertConditional(
		ConditionalExpressionSyntax node,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (node.ColonToken.IsMissing)
		{
			return false;
		}

		var negatedCondition = NegateExpression(node.Condition);
		var whenTrue = node.WhenFalse.WithTriviaFrom(node.WhenTrue);
		var whenFalse = node.WhenTrue.WithTriviaFrom(node.WhenFalse);

		result = ConditionalExpression(
			negatedCondition,
			node.QuestionToken,
			whenTrue,
			node.ColonToken,
			whenFalse);

		return true;
	}

	/// <summary>
	/// Negates an expression, simplifying where possible.
	/// </summary>
	private static ExpressionSyntax NegateExpression(ExpressionSyntax expression)
	{
		return NegateExpressionRefactoring.Negate(expression);
	}
}

