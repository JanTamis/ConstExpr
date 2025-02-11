using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Vectorize.Rewriters;

	public class OptimizeRewriter(SemanticModel semanticModel, MethodDeclarationSyntax method, CancellationToken token) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitBlock(BlockSyntax node)
		{
			var statements = List(node.Statements
				.Select(Visit)
				.Where(x => x is not null)
				.OfType<StatementSyntax>());

			return statements.Count switch 
			{
				0 => null,
				1 => statements[0],
				_ => node.WithStatements(statements),
			};
		}

		public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
		{
			var condition = Visit(node.Condition);

			return condition.Kind() switch 
			{
				SyntaxKind.TrueLiteralExpression => Visit(node.Statement),
				SyntaxKind.FalseLiteralExpression => null,
				_ => node.WithCondition((ExpressionSyntax) condition).WithStatement((StatementSyntax) Visit(node.Statement))
			};
		}

		public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
		{
			var statement = Visit(node.Statement);

			if (statement is null)
			{
				return null;
			}

			if (statement is not BlockSyntax block)
			{
				return node.WithStatement(Block((StatementSyntax)statement));
			}
			
			block = (BlockSyntax) VisitBlock(block);

			return node.WithStatement(block);
		}

		public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
		{
			var condition = Visit(node.Condition);

			return condition.Kind() switch 
			{
				SyntaxKind.TrueLiteralExpression => Visit(node.Statement),
				SyntaxKind.FalseLiteralExpression => null,
				_ => node.WithCondition((ExpressionSyntax) condition).WithStatement((StatementSyntax) Visit(node.Statement))
			};
		}
		
		public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
		{
			var condition = Visit(node.Condition);

			return condition.Kind() switch 
			{
				SyntaxKind.TrueLiteralExpression => Visit(node.Statement),
				SyntaxKind.FalseLiteralExpression => null,
				_ => node.WithCondition((ExpressionSyntax) condition).WithStatement((StatementSyntax) Visit(node.Statement))
			};
		}

		public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
		{
			node = (ForStatementSyntax) base.VisitForStatement(node);

			return node.Condition.Kind() switch 
			{
				SyntaxKind.TrueLiteralExpression => node.Statement,
				SyntaxKind.FalseLiteralExpression => null,
				_ => node,
			};
		}
	}