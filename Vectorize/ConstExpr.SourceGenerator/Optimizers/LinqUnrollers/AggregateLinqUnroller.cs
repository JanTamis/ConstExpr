using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class AggregateLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length == 1)
		{
			if (IsInvokedOnArray(method.CollectionType)
			    || IsInvokedOnCollection(method.CollectionType))
			{
				// var result = collection[0];
				var elementAccess = ElementAccessExpression(IdentifierName("collection"))
					.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))));

				statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(VariableDeclarator(ResultName)
						.WithInitializer(EqualsValueClause(elementAccess))))));
			}
			else
			{
				// using var e = collection.GetEnumerator();
				statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(VariableDeclarator("e")
						.WithInitializer(EqualsValueClause(InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("collection"), IdentifierName("GetEnumerator")))))))));

				// if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
				statements.Add(IfStatement(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("MoveNext")))),
					ThrowStatement(ObjectCreationExpression(IdentifierName("InvalidOperationException"))
						.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("Sequence contains no elements")))))))));

				// var result = e.Current;
				statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(VariableDeclarator(ResultName)
						.WithInitializer(EqualsValueClause(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"))))))));
			}

			return;
		}

		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(ResultName)
				.WithInitializer(EqualsValueClause(method.Parameters[0]))))));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// 1-param overload: Aggregate((acc, el) => ...) — lambda is Parameters[0]
		// 2/3-param overloads: Aggregate(seed, (acc, el) => ...) — lambda is Parameters[1]
		var lambdaIndex = method.Parameters.Length == 1 ? 0 : 1;

		if (lambdaIndex >= method.Parameters.Length
		    || !TryGetLambda(method.Parameters[lambdaIndex], out var lambda))
		{
			return;
		}

		// For the 1-param enumerator path (non-array, non-IList), the current element is e.Current
		var element = method.Parameters.Length == 1
		              && !IsInvokedOnArray(method.CollectionType)
		              && !IsInvokedOnCollection(method.CollectionType)
			? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"))
			: elementName;

		// Replace the first lambda param (accumulator) with "result"
		var bodyWithResult = ReplaceLambda(lambda, IdentifierName(ResultName));

		if (bodyWithResult == null)
			return;

		// Replace the second lambda param (element) with the element expression
		var finalBody = bodyWithResult;

		if (lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
		{
			var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
			var identifiers = bodyWithResult
				.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Where(n => n.Identifier.Text == secondParam)
				.ToList();

			finalBody = bodyWithResult.ReplaceNodes(identifiers, (_, _) => element);
		}

		statements.Add(ExpressionStatement(method.Visit(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(ResultName), finalBody)) as ExpressionSyntax));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length == 3
		    && TryGetLambda(method.Parameters[2], out var lambda))
		{
			// result = func(result, element);
			statements.Add(ReturnStatement(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(ResultName))!));
		}
		else
		{
			statements.Add(ReturnStatement(IdentifierName(ResultName)));
		}
	}

	public override void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length == 1)
		{
			if (IsInvokedOnArray(collectionType)
			    || IsInvokedOnCollection(collectionType))
			{
				var countProperty = IsInvokedOnArray(collectionType) ? "Length" : "Count";

				resultStatements.Add(CreateForLoop(collectionName, "i", countProperty, Block(statements), CreateLiteral(1)!));
			}
			else
			{
				resultStatements.Add(WhileStatement(
					InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("MoveNext"))),
					Block(statements)));
			}
		}
		else
		{
			base.CreateLoop(method, collectionType, statements, collectionName, resultStatements);
		}
	}

	public override ExpressionSyntax GetCollectionElement(UnrolledLinqMethod method, string collectionName)
	{
		if (method.Parameters.Length == 1)
		{
			if (IsInvokedOnArray(method.CollectionType)
			    || IsInvokedOnCollection(method.CollectionType))
			{
				return ElementAccessExpression(IdentifierName(collectionName))
					.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("i")))));
			}
			
			return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("e"), IdentifierName("Current"));
		}
		
		return base.GetCollectionElement(method, collectionName);
	}
}