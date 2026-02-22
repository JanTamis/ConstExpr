using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers
{
	public abstract class BaseFunctionOptimizer
	{
		public abstract bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result);

		protected InvocationExpressionSyntax CreateInvocation(ITypeSymbol type, string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return InvocationExpression(
					MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						ParseTypeName(type.Name),
						IdentifierName(name)))
				.WithArgumentList(
					ArgumentList(
						SeparatedList(
							parameters.Select(Argument))));
		}

		protected InvocationExpressionSyntax CreateInvocation(string name, params IEnumerable<ExpressionSyntax> parameters)
		{
			return InvocationExpression(
							IdentifierName(name))
							.WithArgumentList(
								ArgumentList(
									SeparatedList(
										parameters.Select(Argument))));
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
