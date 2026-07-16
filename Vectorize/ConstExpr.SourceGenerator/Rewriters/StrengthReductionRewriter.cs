using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs induction-variable strength reduction: multiplication of a loop counter by an
///   integer constant is replaced by an accumulator that advances together with the counter,
///   turning a multiply per iteration into an add:
///   <code>
///   for (var i = 0; i &lt; n; i++) { sum += i * 10; }
///   =>
///   for (int i = 0, iMul10 = 0; i &lt; n; i++, iMul10 += 10) { sum += iMul10; }
///   </code>
///   Placing the accumulator update in the incrementor section makes <c>continue</c> correct for
///   free, and in unchecked integer arithmetic the accumulator wraps identically to the product.
///   Scope is deliberately narrow (v1): the header must be exactly
///   <c>var i = &lt;int literal&gt;; i &lt; e</c> (or <c>&lt;=</c>) with a single <c>i++</c> or
///   <c>i += &lt;int literal&gt;</c> incrementor; the counter must not be written, shadowed, or
///   ref/out-captured in the body; the body must contain no lambdas or <c>checked</c> contexts;
///   and products must be literal <c>i * c</c> / <c>c * i</c> with <c>c &gt;= 2</c>. At most two
///   distinct constants are reduced (readability); more, or an accumulator-name collision,
///   leaves the loop unchanged.
/// </summary>
public sealed class StrengthReductionRewriter : CSharpSyntaxRewriter
{
	private readonly HashSet<string> _takenNames;

	private StrengthReductionRewriter(HashSet<string> takenNames)
	{
		_takenNames = takenNames;
	}

	/// <summary>
	///   Applies induction-variable strength reduction to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		// Every identifier already present anywhere in the tree is off-limits as an accumulator
		// name; names this rewriter generates are added as it goes.
		var takenNames = new HashSet<string>(node.DescendantTokens()
			.Where(token => token.IsKind(SyntaxKind.IdentifierToken))
			.Select(token => token.Text));

		return new StrengthReductionRewriter(takenNames).Visit(node);
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		// Bottom-up, so nested loops reduce before their enclosing loop.
		var visited = (ForStatementSyntax) base.VisitForStatement(node)!;

		return TryReduce(visited) ?? visited;
	}

	private ForStatementSyntax? TryReduce(ForStatementSyntax loop)
	{
		if (loop.Declaration is not
		    {
			    Type.IsVar: true,
			    Variables: [ { Initializer.Value: LiteralExpressionSyntax { Token.Value: int initialValue } } declarator ]
		    })
		{
			return null;
		}

		var counter = declarator.Identifier.Text;

		if (loop.Condition is not BinaryExpressionSyntax condition
		    || !condition.IsKind(SyntaxKind.LessThanExpression) && !condition.IsKind(SyntaxKind.LessThanOrEqualExpression)
		    || condition.Left is not IdentifierNameSyntax conditionLeft
		    || conditionLeft.Identifier.Text != counter)
		{
			return null;
		}

		int step;

		switch (loop.Incrementors)
		{
			case [ PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax incremented } post ]
				when post.IsKind(SyntaxKind.PostIncrementExpression) && incremented.Identifier.Text == counter:
			{
				step = 1;
				break;
			}

			case [ AssignmentExpressionSyntax { Left: IdentifierNameSyntax target, Right: LiteralExpressionSyntax { Token.Value: int stepValue } } assignment ]
				when assignment.IsKind(SyntaxKind.AddAssignmentExpression) && target.Identifier.Text == counter:
			{
				step = stepValue;
				break;
			}

			default:
			{
				return null;
			}
		}

		var body = loop.Statement;

		if (LoopInvariance.CollectWrittenInLoop(body).Contains(counter)
		    || LoopInvariance.CollectLoopLocals(body).Contains(counter)
		    || body.DescendantNodes().Any(d => d is AnonymousFunctionExpressionSyntax or CheckedExpressionSyntax or CheckedStatementSyntax)
		    || body.DescendantNodes().OfType<ArgumentSyntax>()
			    .Any(a => a.RefOrOutKeyword.RawKind != 0 && a.Expression is IdentifierNameSyntax refId && refId.Identifier.Text == counter))
		{
			return null;
		}

		var constants = new List<int>();

		foreach (var multiply in body.DescendantNodes().OfType<BinaryExpressionSyntax>())
		{
			if (TryGetProductConstant(multiply, counter, out var constant) && !constants.Contains(constant))
			{
				constants.Add(constant);
			}
		}

		if (constants.Count is 0 or > 2)
		{
			return null;
		}

		var declarators = new List<VariableDeclaratorSyntax> { declarator };
		var incrementors = loop.Incrementors.ToList();
		var newBody = body;

		foreach (var constant in constants)
		{
			var name = $"{counter}Mul{constant}";

			if (!_takenNames.Add(name))
			{
				return null;
			}

			declarators.Add(VariableDeclarator(Identifier(name))
				.WithInitializer(EqualsValueClause(CreateLiteral(unchecked(initialValue * constant)))));

			incrementors.Add(AssignmentExpression(
				SyntaxKind.AddAssignmentExpression,
				IdentifierName(name),
				CreateLiteral(unchecked(step * constant))));

			newBody = (StatementSyntax) new ProductReplacer(counter, constant, name).Visit(newBody)!;
		}

		// `var i = 0, iMul10 = 0;` would be CS0819 (implicitly typed declarations allow a single
		// declarator), so the header type becomes explicit `int` — guaranteed correct because the
		// initializer is an int literal.
		return loop
			.WithDeclaration(VariableDeclaration(
				PredefinedType(Token(SyntaxKind.IntKeyword)).WithTriviaFrom(loop.Declaration.Type),
				SeparatedList(declarators)))
			.WithIncrementors(SeparatedList(incrementors))
			.WithStatement(newBody);
	}

	private static bool TryGetProductConstant(BinaryExpressionSyntax node, string counter, out int constant)
	{
		if (node.IsKind(SyntaxKind.MultiplyExpression))
		{
			if (node.Left is IdentifierNameSyntax { Identifier.Text: var leftName }
			    && leftName == counter
			    && node.Right is LiteralExpressionSyntax { Token.Value: int rightValue and >= 2 })
			{
				constant = rightValue;
				return true;
			}

			if (node.Right is IdentifierNameSyntax { Identifier.Text: var rightName }
			    && rightName == counter
			    && node.Left is LiteralExpressionSyntax { Token.Value: int leftValue and >= 2 })
			{
				constant = leftValue;
				return true;
			}
		}

		constant = 0;
		return false;
	}

	/// <summary>
	///   Replaces every <c>counter * constant</c> / <c>constant * counter</c> product with the
	///   accumulator identifier.
	/// </summary>
	private sealed class ProductReplacer(string counter, int constant, string accumulator) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
		{
			if (TryGetProductConstant(node, counter, out var found) && found == constant)
			{
				return IdentifierName(accumulator).WithTriviaFrom(node);
			}

			return base.VisitBinaryExpression(node);
		}
	}
}