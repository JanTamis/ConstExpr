using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between block-body and expression-body forms for
/// lambdas and local functions.
/// Inspired by the Roslyn <c>UseExpressionBodyForLambdaCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Block body → expression body:  <c>x => { return expr; }</c>  →  <c>x => expr</c></item>
///   <item>Expression body → block body:  <c>x => expr</c>  →  <c>x => { return expr; }</c></item>
/// </list>
/// </summary>
public static class UseExpressionBodyRefactoring
{
	// -----------------------------------------------------------------------
	// Lambda: block → expression
	// -----------------------------------------------------------------------

	/// <summary>
	/// Converts a lambda with a block body containing a single return/expression statement
	/// into an expression-bodied lambda.
	/// </summary>
	public static bool TryConvertToExpressionBody(
		LambdaExpressionSyntax lambda,
		[NotNullWhen(true)] out LambdaExpressionSyntax? result)
	{
		result = null;

		if (lambda.Block is not { Statements: [ var single ] } block)
		{
			return false;
		}

		var expr = single switch
		{
			ReturnStatementSyntax { Expression: { } ret } => ret,
			ExpressionStatementSyntax es => es.Expression,
			_ => null
		};

		if (expr is null)
		{
			return false;
		}

		result = lambda switch
		{
			SimpleLambdaExpressionSyntax simple => simple
				.WithBlock(null)
				.WithExpressionBody(expr.WithTriviaFrom(block)),
			ParenthesizedLambdaExpressionSyntax paren => paren
				.WithBlock(null)
				.WithExpressionBody(expr.WithTriviaFrom(block)),
			_ => null
		};

		return result is not null;
	}

	// -----------------------------------------------------------------------
	// Lambda: expression → block
	// -----------------------------------------------------------------------

	/// <summary>
	/// Converts an expression-bodied lambda into a lambda with a block body
	/// containing a return statement (or expression statement for void-returning lambdas).
	/// When a <paramref name="semanticModel"/> is provided, detects void-returning lambdas
	/// and generates an expression statement instead of a return statement.
	/// </summary>
	public static bool TryConvertToBlockBody(
		LambdaExpressionSyntax lambda,
		SemanticModel? semanticModel,
		[NotNullWhen(true)] out LambdaExpressionSyntax? result)
	{
		result = null;

		if (lambda.ExpressionBody is not { } expr)
		{
			return false;
		}

		// Determine if the lambda is void-returning using the semantic model
		var isVoid = false;
		var typeInfo = semanticModel.GetTypeInfo(lambda);

		if (typeInfo.ConvertedType is INamedTypeSymbol { DelegateInvokeMethod.ReturnType: var returnType })
		{
			isVoid = returnType.SpecialType == SpecialType.System_Void;
		}

		StatementSyntax statement = isVoid
			? ExpressionStatement(expr)
			: ReturnStatement(expr);

		var body = Block(statement);

		result = lambda switch
		{
			SimpleLambdaExpressionSyntax simple => simple
				.WithExpressionBody(null)
				.WithBlock(body),
			ParenthesizedLambdaExpressionSyntax paren => paren
				.WithExpressionBody(null)
				.WithBlock(body),
			_ => null
		};

		return result is not null;
	}

	// -----------------------------------------------------------------------
	// Method / property: block → expression body
	// -----------------------------------------------------------------------

	/// <summary>
	/// Converts a method with a single-statement block body into an expression-bodied method.
	/// </summary>
	public static bool TryConvertMethodToExpressionBody(
		MethodDeclarationSyntax method,
		[NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = null;

		if (method.Body is not { Statements: [ var single ] })
		{
			return false;
		}

		var expr = single switch
		{
			ReturnStatementSyntax { Expression: { } ret } => ret,
			ExpressionStatementSyntax es => es.Expression,
			ThrowStatementSyntax { Expression: { } throwExpr }
				=> ThrowExpression(throwExpr),
			_ => null
		};

		if (expr is null)
		{
			return false;
		}

		result = method
			.WithBody(null)
			.WithExpressionBody(ArrowExpressionClause(expr))
			.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

		return true;
	}

	/// <summary>
	/// Converts an expression-bodied method into a block-bodied method.
	/// </summary>
	public static bool TryConvertMethodToBlockBody(
		MethodDeclarationSyntax method,
		[NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = null;

		if (method.ExpressionBody is not { Expression: var expr })
		{
			return false;
		}

		// If the return type is void, use expression statement; otherwise return.
		var isVoid = method.ReturnType is PredefinedTypeSyntax pts
		             && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);

		StatementSyntax statement = expr is ThrowExpressionSyntax throwExpr
			? ThrowStatement(throwExpr.Expression)
			: isVoid
				? ExpressionStatement(expr)
				: ReturnStatement(expr);

		result = method
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithBody(Block(statement));

		return true;
	}
}