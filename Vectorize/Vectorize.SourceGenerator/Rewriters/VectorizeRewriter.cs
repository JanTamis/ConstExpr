using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Vectorize.Rewriters ;

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

			// i < array.Length - Vector<T>.Length
			var condition = BinaryExpression(SyntaxKind.LessThanExpression,
				IdentifierName("i"),
				BinaryExpression(SyntaxKind.SubtractExpression,
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, node.Expression, IdentifierName("Length")),
					GetMemberAccessExpression($"Vector<{type}>.Count")));

			// i += Vector<T>.Length
			var incrementor = AssignmentExpression(SyntaxKind.AddAssignmentExpression,
				IdentifierName("i"),
				GetMemberAccessExpression($"Vector<{type}>.Count"));

			if (node.Statement is BlockSyntax block)
			{
				var foreachItem = node.Expression;
				var access = GetMemberAccessExpression("Vector.LoadUnsafe");

				var refExpression = RefExpression(InvocationExpression(GetMemberAccessExpression("MemoryMarshal.GetReference"), ArgumentList(SeparatedList([ Argument(foreachItem) ]))));
				var arguments = ArgumentList(SeparatedList([ Argument(refExpression), Argument(CastExpression(IdentifierName("uint"), IdentifierName("i"))) ]));

				// Vector.LoadUnsafe(ref MemoryMarshal.GetReference(array), (uint)i)
				var temp = InvocationExpression(access, arguments);
				var variable = LocalDeclarationStatement(
					VariableDeclaration(
						GetVarIdentifier(),
						SeparatedList([VariableDeclarator(node.Identifier.Text).WithInitializer(EqualsValueClause(temp))])));
				
				block = (BlockSyntax) VisitBlock(block);

				var forStatement = ForStatement(block.WithStatements(block.Statements.Insert(0, variable)))
					.WithCondition(condition)
					.WithIncrementors(SeparatedList<ExpressionSyntax>([incrementor]));

				return forStatement;
			}

			return ForStatement((StatementSyntax) Visit(node.Statement))
				.WithCondition(condition)
				.WithIncrementors(SeparatedList<ExpressionSyntax>([incrementor]));
		}

		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			// xVector
			return node.Update(Identifier($"{node.Identifier.Text}Vector"));
		}

		public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
		{
			var accessExpression = GetMemberAccessExpression("Vector.ConditionalSelect");
			var arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) Visit(node.Condition)), Argument((ExpressionSyntax) Visit(node.WhenTrue)), Argument((ExpressionSyntax) Visit(node.WhenFalse)) ]));
			
			// Vector.ConditionalSelect(condition, whenTrue, whenFalse)
			return InvocationExpression(accessExpression, arguments);
		}

		public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
		{
			var left = Visit(node.Left);
			var right = Visit(node.Right);

			MemberAccessExpressionSyntax? access = null;
			ArgumentListSyntax? arguments = null;
			
			switch (node.Kind())
			{
				case SyntaxKind.AddExpression:
					access = GetMemberAccessExpression("Vector.Add");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.SubtractExpression:
					access = GetMemberAccessExpression("Vector.Subtract");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.MultiplyExpression:
					access = GetMemberAccessExpression("Vector.Multiply");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.DivideExpression:
					access = GetMemberAccessExpression("Vector.Divide");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.LessThanExpression:
					if (IsZero(node.Right))
					{
						access = GetMemberAccessExpression("Vector.IsNegative");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left) ]));
					}
					else if (IsZero(node.Left))
					{
						access = GetMemberAccessExpression("Vector.IsPositive");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) right) ]));
					}
					else
					{
						access = GetMemberAccessExpression("Vector.LessThan");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					}
					break;
				case SyntaxKind.LessThanOrEqualExpression:
					access = GetMemberAccessExpression("Vector.LessThanOrEqual");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.GreaterThanExpression:
					if (IsZero(node.Right))
					{
						access = GetMemberAccessExpression("Vector.IsPositive");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left) ]));
					}
					else if (IsZero(node.Left))
					{
						access = GetMemberAccessExpression("Vector.IsNegative");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) right) ]));
					}
					else
					{
						access = GetMemberAccessExpression("Vector.GreaterThan");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					}
					break;
				case SyntaxKind.GreaterThanOrEqualExpression:
					access = GetMemberAccessExpression("Vector.GreaterThanOrEqual");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
				case SyntaxKind.EqualsExpression:
					if (IsZero(node.Right))
					{
						access = GetMemberAccessExpression("Vector.IsZero");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left) ]));
					}
					else if (IsZero(node.Left))
					{
						access = GetMemberAccessExpression("Vector.IsZero");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) right) ]));
					}
					else
					{
						access = GetMemberAccessExpression("Vector.Equals");
						arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					}
					break;
				case SyntaxKind.NotEqualsExpression:
					access = GetMemberAccessExpression("Vector.NotEquals");
					arguments = ArgumentList(SeparatedList([ Argument((ExpressionSyntax) left), Argument((ExpressionSyntax) right) ]));
					break;
			}

			if (access is not null)
			{
				return InvocationExpression(access, arguments);
			}
			
			return base.VisitBinaryExpression(node);
		}

		public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
		{
			if (node.Kind() == SyntaxKind.NumericLiteralExpression)
			{
				var value = node.Token.Value;

				if (IsZero(node))
				{
					return GetMemberAccessExpression($"Vector<{GetFriendlyName(value.GetType())}>.Zero");
				}

				if (IsOne(node))
				{
					return GetMemberAccessExpression($"Vector<{GetFriendlyName(value.GetType())}>.One");
				}
				
				// Vector.Create(value)
				var arguments = ArgumentList(SeparatedList([ Argument(node) ]));
				var access = GetMemberAccessExpression("Vector.Create");
				return InvocationExpression(access, arguments);
			}
			
			return base.VisitLiteralExpression(node);
		}

		public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
		{
			// return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, node.Expression, InvocationExpression(GetMemberAccessExpression("Vector.Sum"), ArgumentList(SeparatedList([ Argument((ExpressionSyntax)Visit(node.Expression)) ]))));

			return base.VisitReturnStatement(node);
		}

		private IdentifierNameSyntax GetVarIdentifier()
		{
			return IdentifierName("var");
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
				_ => null,
			};
		}

		private ITypeSymbol? GetElementType(ITypeSymbol? type)
		{
			return type switch 
			{
				INamedTypeSymbol named => named.TypeArguments[0],
				IArrayTypeSymbol array => array.ElementType, 
				_ => null,
			};
		}
		
		private string GetFriendlyName(Type type)
		{
			return type.Name switch
			{
				"Single" => "float",
				"Double" => "double",
				_ => type.Name.ToLower(),
			};
		}

		private bool IsZero(ExpressionSyntax node)
		{
			return node is LiteralExpressionSyntax { Token.Value: var value } && value switch
			{
				int i => i == 0,
				float f => f == 0,
				double d => d == 0,
				_ => false,
			};
		}

		private bool IsOne(ExpressionSyntax node)
		{
			return node is LiteralExpressionSyntax { Token.Value: var value } && value switch
			{
				int i => i == 1,
				float f => f == 1f,
				double d => d == 1d,
				_ => false,
			};
		}
	}