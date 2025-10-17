using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers
{
	public abstract class BaseFunctionOptimizer
	{
		public abstract bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result);

		protected InvocationExpressionSyntax CreateInvocation(ITypeSymbol type, string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.ParseTypeName(type.Name),
						SyntaxFactory.IdentifierName(name)))
				.WithArgumentList(
					SyntaxFactory.ArgumentList(
						SyntaxFactory.SeparatedList(
							parameters.Select(SyntaxFactory.Argument))));
		}

		protected InvocationExpressionSyntax CreateInvocation(string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return SyntaxFactory.InvocationExpression(
							SyntaxFactory.IdentifierName(name))
							.WithArgumentList(
								SyntaxFactory.ArgumentList(
									SyntaxFactory.SeparatedList(
										parameters.Select(SyntaxFactory.Argument))));
		}

		protected static bool IsPure(SyntaxNode node)
		{
			return node switch
			{
				IdentifierNameSyntax => true,
				LiteralExpressionSyntax => true,
				ParenthesizedExpressionSyntax par => IsPure(par.Expression),
				PrefixUnaryExpressionSyntax u => IsPure(u.Operand),
				BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
				_ => false
			};
		}

		protected static MethodDeclarationSyntax ParseMethodFromString(string methodString)
		{
			var wrappedCode = $$"""
				public class TempClass
				{
					{{methodString}}
				}
				""";

			var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

			return syntaxTree.GetRoot()
				.DescendantNodes()
				.OfType<MethodDeclarationSyntax>()
				.First();
		}
	}
}
