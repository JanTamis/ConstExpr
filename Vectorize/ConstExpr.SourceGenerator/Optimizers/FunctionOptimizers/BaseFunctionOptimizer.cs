using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public abstract class BaseFunctionOptimizer
{
	public abstract bool TryOptimize(IMethodSymbol method, IList<ExpressionSyntax> parameters, out SyntaxNode? result);
	
	protected InvocationExpressionSyntax CreateInvocation(ITypeSymbol type, string name, params IEnumerable<ExpressionSyntax> parameters)
	{
		return SyntaxFactory.InvocationExpression(
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				SyntaxFactory.ParseTypeName(type.Name),
				SyntaxFactory.IdentifierName(name)))
		.WithArgumentList(
			SyntaxFactory.ArgumentList(
				SyntaxFactory.SeparatedList<ArgumentSyntax>(
					parameters.Select(SyntaxFactory.Argument))));
	}
	
	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			_ => false
		};
	}
}