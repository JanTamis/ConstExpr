using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Visitors;

public sealed class AccessWalker(IDictionary<string, int> variables) : CSharpSyntaxVisitor
{
	public override void DefaultVisit(SyntaxNode node)
	{
		foreach (var child in node.ChildNodes())
		{
			Visit(child);
		}
	}

	public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		variables.Add(node.Identifier.Text, 0);

		Visit(node.Initializer);
	}

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (!variables.ContainsKey(node.Identifier.Text))
		{
			return;
		}

		variables[node.Identifier.Text]++;
	}

	public override void VisitSwitchStatement(SwitchStatementSyntax node)
	{
		Visit(node.Expression);

		var newVariables = node.Sections
			.Select(s =>
			{
				var tempVariables = CloneVariables();
				var walker = new AccessWalker(tempVariables);

				walker.Visit(s);

				return tempVariables;
			})
			.Select(s => s)
			.SelectMany(s => s)
			.GroupBy(s => s.Key)
			.ToDictionary(g => g.Key, g => g.Max(s => s.Value));

		foreach (var variable in newVariables)
		{
			if (variables.ContainsKey(variable.Key))
			{
				variables[variable.Key] = variable.Value;
			}
			else
			{
				variables.Add(variable.Key, variable.Value);
			}
		}
	}

	public override void VisitIfStatement(IfStatementSyntax node)
	{
		Visit(node.Condition);

		var statementVariables = CloneVariables();
		var statementWalker = new AccessWalker(statementVariables);
		statementWalker.Visit(node.Statement);

		var elseVariables = CloneVariables();
		var elseWalker = new AccessWalker(elseVariables);
		elseWalker.Visit(node.Else);

		var newVariables = new[] { statementVariables, elseVariables }
			.SelectMany(s => s)
			.GroupBy(s => s.Key)
			.ToDictionary(g => g.Key, g => g.Max(s => s.Value));

		foreach (var variable in newVariables)
		{
			if (variables.ContainsKey(variable.Key))
			{
				variables[variable.Key] = variable.Value;
			}
			else
			{
				variables.Add(variable.Key, variable.Value);
			}
		}
	}

	public override void VisitBlock(BlockSyntax node)
	{
		foreach (var statement in node.Statements)
		{
			Visit(statement);
		}
	}

	public override void VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		Visit(node.Expression);
	}

	private IDictionary<string, int> CloneVariables()
	{
		return new Dictionary<string, int>(variables);
	}
}