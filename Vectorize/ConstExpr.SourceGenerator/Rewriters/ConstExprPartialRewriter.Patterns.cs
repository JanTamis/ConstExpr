using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Pattern matching visitor methods for the ConstExprPartialRewriter.
/// Handles switch statements, is-pattern expressions, and pattern evaluation.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitSwitchStatement(SwitchStatementSyntax node)
	{
		var visitedGoverning = Visit(node.Expression);

		if (TryGetLiteralValue(visitedGoverning ?? node.Expression, out var governingValue))
		{
			return EvaluateSwitchAtCompileTime(node, governingValue);
		}

		// When switch value is unknown, we need to visit all sections to rewrite them,
		// but then invalidate any variables assigned within the switch since we don't
		// know which branch will execute at runtime
		var result = VisitSwitchStatementSections(node, visitedGoverning as ExpressionSyntax ?? node.Expression);

		InvalidateAssignedVariables(node);

		return result;
	}

	/// <summary>
	/// Evaluates a switch statement at compile time when the governing expression is constant.
	/// </summary>
	private SyntaxNode? EvaluateSwitchAtCompileTime(SwitchStatementSyntax node, object? governingValue)
	{
		foreach (var section in node.Sections)
		{
			var matched = false;

			foreach (var label in section.Labels)
			{
				var res = LabelMatches(label, governingValue);

				if (res is true)
				{
					matched = true;
					break;
				}
			}

			if (matched)
			{
				return VisitMatchedSwitchSection(section);
			}
		}

		return null;
	}

	/// <summary>
	/// Visits a matched switch section.
	/// </summary>
	private SyntaxNode? VisitMatchedSwitchSection(SwitchSectionSyntax section)
	{
		var statements = new List<StatementSyntax>();

		foreach (var visited in section.Statements.Select(Visit).OfType<SyntaxNode>())
		{
			switch (visited)
			{
				case BlockSyntax block:
					foreach (var inner in block.Statements)
					{
						if (inner is BreakStatementSyntax)
            {
              continue;
            }

            statements.Add(inner);
					}
					break;
				case StatementSyntax stmt:
					if (stmt is BreakStatementSyntax)
          {
            break;
          }

          statements.Add(stmt);
					break;
				case ExpressionSyntax expr:
					statements.Add(ExpressionStatement(expr));
					break;
			}
		}

		return statements.Count switch
		{
			0 => null,
			1 => statements[0],
			_ => Block(statements)
		};
	}

	/// <summary>
	/// Visits switch statement sections.
	/// </summary>
	private SyntaxNode VisitSwitchStatementSections(SwitchStatementSyntax node, ExpressionSyntax exprSyntax)
	{
		var newSections = new List<SwitchSectionSyntax>(node.Sections.Count);

		foreach (var section in node.Sections)
		{
			var newStatements = new List<StatementSyntax>(section.Statements.Count);

			foreach (var visited in section.Statements.Select(Visit).OfType<SyntaxNode>())
			{
				switch (visited)
				{
					case BlockSyntax block:
						newStatements.AddRange(block.Statements);
						break;
					case StatementSyntax stmt:
						newStatements.Add(stmt);
						break;
					case ExpressionSyntax expr:
						newStatements.Add(ExpressionStatement(expr));
						break;
				}
			}

			newSections.Add(section.WithStatements(List(newStatements)));
		}

		return node
			.WithExpression(exprSyntax)
			.WithSections(List(newSections));
	}

	/// <summary>
	/// Checks if a switch label matches the governing value.
	/// </summary>
	private bool? LabelMatches(SwitchLabelSyntax label, object? governingValue)
	{
		return label switch
		{
			DefaultSwitchLabelSyntax => true,
			CaseSwitchLabelSyntax constCase =>
				TryGetConstantValue(semanticModel.Compilation, loader, Visit(constCase.Value) ?? constCase.Value, new VariableItemDictionary(variables), token, out var caseValue)
					? Equals(governingValue, caseValue)
					: null,
			CasePatternSwitchLabelSyntax patCase =>
				EvaluatePattern(patCase.Pattern, governingValue) is not bool patMatch
					? null
					: patCase.WhenClause is null
						? patMatch
						: EvaluateWhen(patCase.WhenClause) switch
						{
							true => patMatch,
							false => false,
							null => null,
						},
			_ => null
		};
	}

	/// <summary>
	/// Evaluates a pattern against a value.
	/// </summary>
	private bool? EvaluatePattern(PatternSyntax pattern, object? value)
	{
		try
		{
			return pattern switch
			{
				DiscardPatternSyntax => true,
				ConstantPatternSyntax constPat => EvaluateConstantPattern(constPat, value),
				RelationalPatternSyntax relPat => EvaluateRelationalPattern(relPat, value),
				BinaryPatternSyntax binPat => EvaluateBinaryPattern(binPat, value),
				UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) => !EvaluatePattern(unary.Pattern, value),
				ParenthesizedPatternSyntax parPat => EvaluatePattern(parPat.Pattern, value),
				VarPatternSyntax => true,
				DeclarationPatternSyntax declPat => EvaluateDeclarationPattern(declPat, value),
				TypePatternSyntax typePat => EvaluateTypePattern(typePat, value),
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Evaluates a constant pattern.
	/// </summary>
	private bool? EvaluateConstantPattern(ConstantPatternSyntax constPat, object? value)
	{
		var visited = constPat.Expression;
		
		return TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var patVal)
			? Equals(value, patVal)
			: null;
	}

	/// <summary>
	/// Evaluates a relational pattern.
	/// </summary>
	private bool? EvaluateRelationalPattern(RelationalPatternSyntax relPat, object? value)
	{
		var visited = Visit(relPat.Expression) ?? relPat.Expression;

		if (!TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var rightVal))
		{
			return null;
		}

		var op = relPat.OperatorToken.Kind();

		var result = op switch
		{
			SyntaxKind.LessThanToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, rightVal),
			SyntaxKind.LessThanEqualsToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThanOrEqual, value, rightVal),
			SyntaxKind.GreaterThanToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, rightVal),
			SyntaxKind.GreaterThanEqualsToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThanOrEqual, value, rightVal),
			_ => null,
		};

		return result is true;
	}

	/// <summary>
	/// Evaluates a binary pattern.
	/// </summary>
	private bool? EvaluateBinaryPattern(BinaryPatternSyntax binPat, object? value)
	{
		var l = EvaluatePattern(binPat.Left, value);
		var r = EvaluatePattern(binPat.Right, value);

		if (l is null || r is null)
		{
			return null;
		}

		return binPat.OperatorToken.Kind() switch
		{
			SyntaxKind.OrKeyword => l.Value || r.Value,
			SyntaxKind.AndKeyword => l.Value && r.Value,
			_ => null,
		};
	}

	/// <summary>
	/// Evaluates a declaration pattern.
	/// </summary>
	private bool? EvaluateDeclarationPattern(DeclarationPatternSyntax declPat, object? value)
	{
		return EvaluateTypeMatchPattern(declPat.Type, value);
	}

	/// <summary>
	/// Evaluates a type pattern.
	/// </summary>
	private bool? EvaluateTypePattern(TypePatternSyntax typePat, object? value)
	{
		return EvaluateTypeMatchPattern(typePat.Type, value);
	}

	/// <summary>
	/// Evaluates if a value matches a type pattern.
	/// </summary>
	private bool? EvaluateTypeMatchPattern(TypeSyntax typeSyntax, object? value)
	{
		if (!semanticModel.Compilation.TryGetSemanticModel(typeSyntax, out var model))
		{
			return null;
		}

		var typeInfo = model.GetTypeInfo(typeSyntax, token).Type;

		if (typeInfo is null)
		{
			return null;
		}

		if (value is null)
		{
			return typeInfo.IsReferenceType || typeInfo.NullableAnnotation == NullableAnnotation.Annotated;
		}

		var valueType = value.GetType();
		var typeDisplayString = typeInfo.ToDisplayString();
		var typeFullName = valueType.FullName;
		var typeName = valueType.Name;

		// Check for exact match or name match
		if (string.Equals(typeDisplayString, typeFullName, StringComparison.Ordinal) ||
		    string.Equals(typeInfo.Name, typeName, StringComparison.Ordinal))
		{
			return true;
		}

		// Check for interface implementation
		if (typeInfo.TypeKind == TypeKind.Interface)
		{
			return valueType.GetInterfaces().Any(i => i.FullName == typeFullName);
		}

		// Check for base class
		var baseType = valueType.BaseType;

		while (baseType != null)
		{
			if (baseType.FullName == typeFullName || baseType.Name == typeName)
			{
				return true;
			}
			baseType = baseType.BaseType;
		}

		return false;
	}

	/// <summary>
	/// Evaluates a when clause.
	/// </summary>
	private bool? EvaluateWhen(WhenClauseSyntax when)
	{
		var visited = Visit(when.Condition) ?? when.Condition;
		return TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var val)
			? val is true
			: null;
	}

	public override SyntaxNode? VisitIsPatternExpression(IsPatternExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		var exprToEvaluate = expression ?? node.Expression;

		if (TryGetConstantValue(semanticModel.Compilation, loader, exprToEvaluate, new VariableItemDictionary(variables), token, out var value))
		{
			var result = EvaluatePattern(node.Pattern, value);

			if (result.HasValue)
			{
				return CreateLiteral(result.Value);
			}
		}
		
		// Handle unary "not" around constant patterns: `x is not 0` -> `x != 0`
		if (node.Pattern is UnaryPatternSyntax unary && unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) && unary.Pattern is ConstantPatternSyntax constInner)
		{
			var left = expression as ExpressionSyntax ?? node.Expression;
			var right = Visit(constInner.Expression) as ExpressionSyntax ?? constInner.Expression;

			return BinaryExpression(SyntaxKind.NotEqualsExpression, left, right);
		}

		// Handle unary "not" around relational patterns: `x is not > 0` -> `x <= 0` (negated operator)
		if (node.Pattern is UnaryPatternSyntax unaryRel && unaryRel.OperatorToken.IsKind(SyntaxKind.NotKeyword) && unaryRel.Pattern is RelationalPatternSyntax relInner)
		{
			var left = expression as ExpressionSyntax ?? node.Expression;
			var right = Visit(relInner.Expression) as ExpressionSyntax ?? relInner.Expression;

			var negatedKind = relInner.OperatorToken.Kind() switch
			{
				SyntaxKind.LessThanToken => SyntaxKind.GreaterThanOrEqualExpression, // !(x < y) -> x >= y
				SyntaxKind.LessThanEqualsToken => SyntaxKind.GreaterThanExpression, // !(x <= y) -> x > y
				SyntaxKind.GreaterThanToken => SyntaxKind.LessThanOrEqualExpression, // !(x > y) -> x <= y
				SyntaxKind.GreaterThanEqualsToken => SyntaxKind.LessThanExpression, // !(x >= y) -> x < y
				_ => (SyntaxKind?)null,
			};

			if (negatedKind.HasValue)
			{
				return BinaryExpression(negatedKind.Value, left, right);
			}
		}

		// Handle constant pattern: `x is 0` -> `x == 0`
		if (node.Pattern is ConstantPatternSyntax constPat)
		{
			var left = expression as ExpressionSyntax ?? node.Expression;
			var right = Visit(constPat.Expression) as ExpressionSyntax ?? constPat.Expression;

			return BinaryExpression(SyntaxKind.EqualsExpression, left, right);
		}

		// If the pattern is a relational pattern (e.g. `x is > 0`), rewrite it to a binary
		// expression (`x > 0`) so further rewrites/optimizations and compilation see the
		// simpler form. We use the visited subexpressions when available.
		if (node.Pattern is RelationalPatternSyntax relPat)
		{
			var left = expression as ExpressionSyntax ?? node.Expression;
			var right = Visit(relPat.Expression) as ExpressionSyntax ?? relPat.Expression;

			var binaryKind = relPat.OperatorToken.Kind() switch
			{
				SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
				SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
				SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
				SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
				_ => (SyntaxKind?)null,
			};

			if (binaryKind.HasValue)
			{
				return BinaryExpression(binaryKind.Value, left, right);
			}
		}

		// Try to optimize OR patterns into bitmask checks
		if (TryOptmizePattern(node.WithExpression(exprToEvaluate as ExpressionSyntax ?? node.Expression), out var optimized))
		{
			return optimized;
		}

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitSwitchExpression(SwitchExpressionSyntax node)
	{
		var governing = Visit(node.GoverningExpression);

		// Try to evaluate switch at compile time when governing expression is constant
		if (TryGetLiteralValue(governing ?? node.GoverningExpression, out var governingValue))
		{
			foreach (var arm in node.Arms)
			{
				var patternResult = EvaluatePattern(arm.Pattern, governingValue);

				if (patternResult is true)
				{
					// Check when clause if present
					if (arm.WhenClause is not null)
					{
						var whenResult = EvaluateWhen(arm.WhenClause);

						if (whenResult is false)
						{
							continue;
						}

						if (whenResult is null)
						{
							break; // Cannot determine statically
						}
					}

					return Visit(arm.Expression);
				}
			}
		}

		// Simplify: x switch { _ => a } → a (single discard arm)
		if (node.Arms is [ { Pattern: DiscardPatternSyntax, WhenClause: null } ])
		{
			return Visit(node.Arms[0].Expression);
		}

		// Simplify: x switch { true => a, false => b } → x ? a : b
		if (node.Arms.Count == 2 &&
		    semanticModel.GetTypeInfo(node.GoverningExpression).Type?.SpecialType == SpecialType.System_Boolean)
		{
			var trueArm = node.Arms.FirstOrDefault(a => a.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } });
			var falseArm = node.Arms.FirstOrDefault(a => a.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } });

			if (trueArm is { WhenClause: null } 
			    && falseArm is { WhenClause: null })
			{
				return ConditionalExpression(
					governing as ExpressionSyntax ?? node.GoverningExpression,
					Visit(trueArm.Expression) as ExpressionSyntax ?? trueArm.Expression,
					Visit(falseArm.Expression) as ExpressionSyntax ?? falseArm.Expression);
			}
		}

		// Visit all arms
		var visitedArms = node.Arms
			.Select(arm => arm
				.WithExpression(Visit(arm.Expression) as ExpressionSyntax ?? arm.Expression))
			.ToArray();

		return node
			.WithGoverningExpression(governing as ExpressionSyntax ?? node.GoverningExpression)
			.WithArms(SeparatedList(visitedArms));
	}
}

