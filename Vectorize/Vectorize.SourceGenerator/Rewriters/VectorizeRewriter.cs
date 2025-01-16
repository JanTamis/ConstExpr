using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Vectorize.Rewriters;

public class VectorizeRewriter(SemanticModel semanticModel, CancellationToken token) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var arguments = ArgumentList(SeparatedList([ Argument(node.Initializer.Value), ]));
			
		var access = MemberAccessExpression(
			SyntaxKind.SimpleMemberAccessExpression,
			IdentifierName("Vector"),
			IdentifierName("Create"));
		
		var invocation = InvocationExpression(access, arguments);
		
		return node.Update(VisitToken(node.Identifier), node.ArgumentList, node.Initializer.Update(node.Initializer.EqualsToken, invocation));
	}
	
	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var type = IdentifierName(Identifier(node.Type.GetLeadingTrivia(), "var", node.Type.GetTrailingTrivia()));
		
		return node.Update(type, VisitList(node.Variables));
	}
}