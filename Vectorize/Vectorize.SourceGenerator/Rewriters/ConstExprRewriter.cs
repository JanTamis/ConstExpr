using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Rewriters;

public class ConstExprRewriter(SemanticModel semanticModel, MethodDeclarationSyntax method, Dictionary<string, object?> variables, CancellationToken token) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		var name = GetVariableName(Visit(node.Left));
		var value = GetVariableValue(Visit(node.Right), variables);

		var kind = node.Kind();

		variables[name] = kind switch
		{
			SyntaxKind.SimpleAssignmentExpression => value,
			SyntaxKind.AddAssignmentExpression => Add(variables[name], value),
			SyntaxKind.SubtractAssignmentExpression => Subtract(variables[name], value),
			SyntaxKind.MultiplyAssignmentExpression => Multiply(variables[name], value),
			SyntaxKind.DivideAssignmentExpression => Divide(variables[name], value),
			_ => variables[name]
		};

		return node;
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var left = GetVariableValue(Visit(node.Left), variables);
		var right = GetVariableValue(Visit(node.Right), variables);

		var result = Comparer.Default.Compare(left, right);
		var kind = node.Kind();

		return kind switch
		{
			SyntaxKind.EqualsExpression when result is 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			SyntaxKind.NotEqualsExpression when result is not 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			SyntaxKind.GreaterThanExpression when result > 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			SyntaxKind.GreaterThanOrEqualExpression when result >= 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			SyntaxKind.LessThanExpression when result < 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			SyntaxKind.LessThanOrEqualExpression when result <= 0 => LiteralExpression(SyntaxKind.TrueLiteralExpression, Literal("true")),
			_ => LiteralExpression(SyntaxKind.FalseLiteralExpression, Literal("false")),
		};
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var variable = GetVariableValue(node.Expression, variables);

		if (variable is object?[] data)
		{
			var tempVariable = node.Identifier.Text;

			foreach (var item in data)
			{
				variables[tempVariable] = item;

				Visit(node.Statement);
			}

			variables.Remove(tempVariable);
		}

		return node;
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		var value = GetVariableValue(Visit(node.Expression), variables);

		return node.WithExpression(LiteralExpression(GetSyntaxKind(value), Literal(value.ToString())));
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var name = node.Identifier.Text;
		var value = Visit(node.Initializer.Value);

		if (value is LiteralExpressionSyntax literal)
		{
			variables[name] = literal.Token.Value;
		}

		return node;
	}
}