using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Vectorize.Rewriters; 

public class VectorizeRewriter(SemanticModel semanticModel, CancellationToken token) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var value = node.Initializer?.Value;

		if (value is LiteralExpressionSyntax { Token.Value: var tokenValue })
		{
			var type = semanticModel.GetTypeInfo(value).Type.ToDisplayString();
			
			if (tokenValue.Equals(0) || tokenValue.Equals(0f) || tokenValue.Equals(0d))
			{
				// var xVector = Vector<T>.Zero;
				return node.Update(Identifier($"{node.Identifier.Text}Vector"), node.ArgumentList, node.Initializer.Update(node.Initializer.EqualsToken, GetMemberAccessExpression($"Vector<{type}>.Zero")));
			}
			
			if (tokenValue.Equals(1) || tokenValue.Equals(1f) || tokenValue.Equals(1d))
			{
				// var xVector = Vector<T>.One;
				return node.Update(Identifier($"{node.Identifier.Text}Vector"), node.ArgumentList, node.Initializer.Update(node.Initializer.EqualsToken, GetMemberAccessExpression($"Vector<{type}>.One")));
			}
		}
		
		var arguments = ArgumentList(SeparatedList([ Argument(node.Initializer.Value) ]));
		var access = GetMemberAccessExpression("Vector.Create");
		var invocation = InvocationExpression(access, arguments);

		// var xVector = Vector.Create(value);
		return node.Update(VisitToken(Identifier($"{node.Identifier.Text}Vector")), node.ArgumentList, node.Initializer.Update(node.Initializer.EqualsToken, invocation));
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var type = GetVarIdentifier();

		return node.Update(type, VisitList(node.Variables));
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var type = GetElementType(GetType(semanticModel.GetOperation(node.Expression, token)));
		
		// var i = 0
		var variable = GetVarDeclaration("i", SyntaxKind.NumericLiteralExpression, Literal(0));

		// i < array.Length - Vector<T>.Length
		var condition = BinaryExpression(SyntaxKind.LessThanExpression,
			IdentifierName("i"),
			BinaryExpression(SyntaxKind.SubtractExpression,
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, node.Expression, IdentifierName("Length")),
				GetMemberAccessExpression($"Vector<{type}>.Length")));

		// i += Vector<T>.Length
		var incrementor = AssignmentExpression(SyntaxKind.AddAssignmentExpression,
			IdentifierName("i"),
			GetMemberAccessExpression($"Vector<{type}>.Length"));

		// for (var i = 0; i < array.Length - Vector<T>.Length; i += Vector<T>.Length)
		var forStatement = ForStatement((StatementSyntax) Visit(node.Statement))
			.WithDeclaration(variable)
			.WithCondition(condition)
			.WithIncrementors(SeparatedList<ExpressionSyntax>([incrementor]));

		return forStatement;
	}

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		var operation = semanticModel.GetOperation(node, token);

		if (operation is ILocalReferenceOperation { Local: { IsForEach: true } localSymbol })
		{
			var foreachStatement = (ForEachStatementSyntax) localSymbol.DeclaringSyntaxReferences[0].GetSyntax();
			var foreachItem = foreachStatement.Expression;

			var access = GetMemberAccessExpression("Vector.LoadUnsafe");

			var refExpression = RefExpression(InvocationExpression(GetMemberAccessExpression("MemoryMarshal.GetReference"), ArgumentList(SeparatedList([ Argument(foreachItem) ]))));
			var arguments = ArgumentList(SeparatedList([ Argument(refExpression), Argument(CastExpression(IdentifierName("uint"), IdentifierName("i"))) ]));

			// Vector.LoadUnsafe(ref MemoryMarshal.GetReference(array), (uint)i)
			return InvocationExpression(access, arguments);
		}

		// xVector
		return node.Update(Identifier($"{node.Identifier.Text}Vector"));
	}

	private IdentifierNameSyntax GetVarIdentifier()
	{
		return IdentifierName("var");
	}

	private VariableDeclarationSyntax GetVarDeclaration(string identifier, SyntaxKind kind, SyntaxToken value)
	{
		return VariableDeclaration(
			GetVarIdentifier(),
			SeparatedList([VariableDeclarator(identifier).WithInitializer(EqualsValueClause(LiteralExpression(kind, value)))]));
	}

	private MemberAccessExpressionSyntax GetMemberAccessExpression(string expression)
	{
		var parts = expression.Split('.');
		return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(parts[0]), IdentifierName(parts[1]));
	}

	private ITypeSymbol? GetType(IOperation? operation)
	{
		return operation switch 
		{
			IParameterReferenceOperation parameter => parameter.Type, 
			ILocalReferenceOperation local => local.Type, 
			IFieldReferenceOperation field => field.Type, 
			IPropertyReferenceOperation property => property.Type, 
			IArrayElementReferenceOperation array => array.Type, 
			IConversionOperation conversion => conversion.Type, 
			_ => null
		};
	}

	private ITypeSymbol? GetElementType(ITypeSymbol? type)
	{
		return type switch 
		{
			INamedTypeSymbol named => named.TypeArguments[0],
			IArrayTypeSymbol array => array.ElementType, 
			_ => null
		};
	}
}