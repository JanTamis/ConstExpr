using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Converts expression-bodied members back to block bodies when
///   <c>csharp_style_expression_bodied_*</c> is <c>false</c>.
/// </summary>
/// <remarks>
///   <para>
///     Only the block-body direction is handled. Producing expression bodies stays the
///     responsibility of the generator's own narrow whitelist (see
///     <c>ConstExprSourceGenerator.CanBeExpressionBody</c>), which accepts literals, collection
///     expressions and prefix-unary expressions only. Widening that whitelist is a separate
///     decision from honouring the setting.
///   </para>
///   <para>
///     Runs before <c>NormalizeWhitespace</c>, because the block it produces has no trivia yet.
///   </para>
/// </remarks>
public sealed class ExpressionBodyRewriter(FormattingOptions options) : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies the rewriter, or returns <paramref name="node" /> untouched when no member kind is
	///   configured to avoid expression bodies.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, FormattingOptions options)
	{
		var wanted = options.ExpressionBodiedMethods == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedLocalFunctions == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedLambdas == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedProperties == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedIndexers == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedAccessors == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedConstructors == ExpressionBodyPreference.Never
		             || options.ExpressionBodiedOperators == ExpressionBodyPreference.Never;

		return wanted
			? new ExpressionBodyRewriter(options).Visit(node)
			: node;
	}

	public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
	{
		var visited = base.VisitPropertyDeclaration(node) as PropertyDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedProperties != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		// A property's expression body is shorthand for a getter, so it becomes "get { return x; }".
		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithAccessorList(GetOnlyAccessorList(expression));
	}

	public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
	{
		var visited = base.VisitIndexerDeclaration(node) as IndexerDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedIndexers != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithAccessorList(GetOnlyAccessorList(expression));
	}

	public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
	{
		var visited = base.VisitAccessorDeclaration(node) as AccessorDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedAccessors != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		// Only a getter yields a value; set/init/add/remove accessors evaluate for effect.
		StatementSyntax statement = visited.IsKind(SyntaxKind.GetAccessorDeclaration)
			? ReturnStatement(expression)
			: ExpressionStatement(expression);

		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(statement));
	}

	public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
	{
		var visited = base.VisitConstructorDeclaration(node) as ConstructorDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedConstructors != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		// A constructor never returns a value, so its body is always an expression statement.
		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(ExpressionStatement(expression)));
	}

	public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
	{
		var visited = base.VisitOperatorDeclaration(node) as OperatorDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedOperators != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		// An operator always returns a value.
		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(ReturnStatement(expression)));
	}

	public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
	{
		var visited = base.VisitConversionOperatorDeclaration(node) as ConversionOperatorDeclarationSyntax;

		if (visited is null
		    || options.ExpressionBodiedOperators != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(ReturnStatement(expression)));
	}

	private static AccessorListSyntax GetOnlyAccessorList(ExpressionSyntax expression)
	{
		return AccessorList(SingletonList(
			AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
				.WithBody(Block(ReturnStatement(expression)))));
	}

	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var visited = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;

		if (visited is null || options.ExpressionBodiedMethods != ExpressionBodyPreference.Never)
		{
			return visited;
		}

		return UseExpressionBodyRefactoring.TryConvertMethodToBlockBody(visited, out var result)
			? result.WithTriviaFrom(visited)
			: visited;
	}

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		var visited = base.VisitLocalFunctionStatement(node) as LocalFunctionStatementSyntax;

		if (visited is null
		    || options.ExpressionBodiedLocalFunctions != ExpressionBodyPreference.Never
		    || visited.ExpressionBody is not { Expression: var expression })
		{
			return visited;
		}

		var isVoid = visited.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.VoidKeyword };

		StatementSyntax statement = expression is ThrowExpressionSyntax throwExpression
			? ThrowStatement(throwExpression.Expression)
			: isVoid
				? ExpressionStatement(expression)
				: ReturnStatement(expression);

		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(statement));
	}

	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		return ConvertLambda(base.VisitSimpleLambdaExpression(node));
	}

	public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
	{
		return ConvertLambda(base.VisitParenthesizedLambdaExpression(node));
	}

	/// <summary>
	///   Rewrites <c>x => expr</c> into <c>x => { return expr; }</c>.
	/// </summary>
	/// <remarks>
	///   Only bodies that can only ever produce a value are converted. There is no semantic model
	///   here, so for an invocation, assignment, <c>await</c> or object creation it is impossible to
	///   tell a <c>Func</c> from an <c>Action</c> - <c>x => list.Add(y)</c> needs
	///   <c>{ list.Add(y); }</c> while <c>x => Compute()</c> needs <c>{ return Compute(); }</c>, and
	///   guessing wrong produces code that does not compile. Those lambdas are left as expression
	///   bodies; <see cref="UseExpressionBodyRefactoring.TryConvertToBlockBody" /> handles them
	///   where a model is available.
	/// </remarks>
	private SyntaxNode? ConvertLambda(SyntaxNode? visited)
	{
		if (visited is not LambdaExpressionSyntax lambda
		    || options.ExpressionBodiedLambdas != ExpressionBodyPreference.Never
		    || lambda.ExpressionBody is not { } expression
		    || !IsAlwaysValueProducing(expression))
		{
			return visited;
		}

		var body = Block(ReturnStatement(expression));

		return lambda switch
		{
			SimpleLambdaExpressionSyntax simple => simple.WithExpressionBody(null).WithBlock(body),
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.WithExpressionBody(null).WithBlock(body),
			_ => visited
		};
	}

	/// <summary>
	///   Whether <paramref name="expression" /> can never stand on its own as a statement, and so
	///   must be the value of the enclosing lambda.
	/// </summary>
	private static bool IsAlwaysValueProducing(ExpressionSyntax expression)
	{
		return expression is LiteralExpressionSyntax or BinaryExpressionSyntax or ConditionalExpressionSyntax
			or MemberAccessExpressionSyntax or IdentifierNameSyntax or CastExpressionSyntax
			or IsPatternExpressionSyntax or ElementAccessExpressionSyntax or TupleExpressionSyntax
			or CollectionExpressionSyntax or SwitchExpressionSyntax
			or PrefixUnaryExpressionSyntax
			{
				RawKind: (int) SyntaxKind.UnaryMinusExpression or (int) SyntaxKind.UnaryPlusExpression
				or (int) SyntaxKind.LogicalNotExpression or (int) SyntaxKind.BitwiseNotExpression
			};
	}
}