using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vectorize.Helpers;

public static class SyntaxHelpers
{
	public static SyntaxKind GetSyntaxKind(object? value)
	{
		return value switch
		{
			int => SyntaxKind.NumericLiteralExpression,
			float => SyntaxKind.NumericLiteralExpression,
			double => SyntaxKind.NumericLiteralExpression,
			long => SyntaxKind.NumericLiteralExpression,
			decimal => SyntaxKind.NumericLiteralExpression,
			string => SyntaxKind.StringLiteralExpression,
			char => SyntaxKind.CharacterLiteralExpression,
			bool => SyntaxKind.TrueLiteralExpression,
			null => SyntaxKind.NullLiteralExpression,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public static object? GetVariableValue(SyntaxNode? expression, Dictionary<string, object?> variables)
	{
		return expression switch
		{
			LiteralExpressionSyntax literal => literal.Kind() switch
			{
				SyntaxKind.StringLiteralExpression => literal.Token.Value,
				SyntaxKind.CharacterLiteralExpression => literal.Token.Value,
				SyntaxKind.TrueLiteralExpression => true,
				SyntaxKind.FalseLiteralExpression => false,
				SyntaxKind.NullLiteralExpression => null,
				_ => literal.Token.Value,
			},
			IdentifierNameSyntax identifier => variables[identifier.Identifier.Text],
			MemberAccessExpressionSyntax simple => GetVariableValue(simple.Expression, variables),
			_ => null
		};
	}

	public static bool TryGetVariableValue(SyntaxNode? expression, Dictionary<string, object?> variables, out object? value)
	{
		switch (expression)
		{
			case LiteralExpressionSyntax literal:
				switch (literal.Kind())
				{
					case SyntaxKind.StringLiteralExpression:
					case SyntaxKind.CharacterLiteralExpression:
						value = literal.Token.Value;
						return true;
					case SyntaxKind.TrueLiteralExpression:
						value = true;
						return true;
					case SyntaxKind.FalseLiteralExpression:
						value = false;
						return true;
					case SyntaxKind.NullLiteralExpression:
						value = null;
						return true;
					default:
						value = literal.Token.Value;
						return true;
				}
			case IdentifierNameSyntax identifier:
				return variables.TryGetValue(identifier.Identifier.Text, out value);
			case MemberAccessExpressionSyntax simple:
				return TryGetVariableValue(simple.Expression, variables, out value);
			default:
				value = null;
				return true;
		}
	}

	public static string? GetVariableName(SyntaxNode expression)
	{
		return expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.Text,
			_ => null
		};
	}
	
	public static LiteralExpressionSyntax NumberLiteral(int value)
	{
		return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralToken, SyntaxFactory.Literal(value));
	}

	public static LiteralExpressionSyntax NumberLiteral(float value)
	{
		return SyntaxFactory.LiteralExpression(GetSyntaxKind(value), SyntaxFactory.Literal(value));
	}

	public static LiteralExpressionSyntax NumberLiteral(double value)
	{
		return SyntaxFactory.LiteralExpression(GetSyntaxKind(value), SyntaxFactory.Literal(value));
	}

	public static LiteralExpressionSyntax StringLiteral(string value)
	{
		return SyntaxFactory.LiteralExpression(GetSyntaxKind(value), SyntaxFactory.Literal(value));
	}

	public static LiteralExpressionSyntax BoolLiteral(bool value)
	{
		return SyntaxFactory.LiteralExpression(value 
			? SyntaxKind.TrueLiteralExpression 
			: SyntaxKind.FalseLiteralExpression);
	}

	public static object? Add(object left, object right)
	{
		return left switch
		{
			int i when right is int ri => i + ri,
			int i when right is float rf => i + rf,
			int i when right is double rd => i + rd,
			int i when right is long rl => i + rl,
			int i when right is decimal rdec => i + rdec,
			float f when right is int ri => f + ri,
			float f when right is float rf => f + rf,
			float f when right is double rd => f + rd,
			float f when right is long rl => f + rl,
			float f when right is decimal rdec => f + (float) rdec,
			double d when right is int ri => d + ri,
			double d when right is float rf => d + rf,
			double d when right is double rd => d + rd,
			double d when right is long rl => d + rl,
			double d when right is decimal rdec => d + (double) rdec,
			long l when right is int ri => l + ri,
			long l when right is float rf => l + rf,
			long l when right is double rd => l + rd,
			long l when right is long rl => l + rl,
			long l when right is decimal rdec => l + rdec,
			decimal dec when right is int ri => dec + ri,
			decimal dec when right is float rf => dec + (decimal) rf,
			decimal dec when right is double rd => dec + (decimal) rd,
			decimal dec when right is long rl => dec + rl,
			decimal dec when right is decimal rdec => dec + rdec,
			_ => null
		};
	}

	public static object? Subtract(object left, object right)
	{
		return left switch
		{
			int i when right is int ri => i - ri,
			int i when right is float rf => i - rf,
			int i when right is double rd => i - rd,
			int i when right is long rl => i - rl,
			int i when right is decimal rdec => i - rdec,
			float f when right is int ri => f - ri,
			float f when right is float rf => f - rf,
			float f when right is double rd => f - rd,
			float f when right is long rl => f - rl,
			float f when right is decimal rdec => f - (float) rdec,
			double d when right is int ri => d - ri,
			double d when right is float rf => d - rf,
			double d when right is double rd => d - rd,
			double d when right is long rl => d - rl,
			double d when right is decimal rdec => d - (double) rdec,
			long l when right is int ri => l - ri,
			long l when right is float rf => l - rf,
			long l when right is double rd => l - rd,
			long l when right is long rl => l - rl,
			long l when right is decimal rdec => l - rdec,
			decimal dec when right is int ri => dec - ri,
			decimal dec when right is float rf => dec - (decimal) rf,
			decimal dec when right is double rd => dec - (decimal) rd,
			decimal dec when right is long rl => dec - rl,
			decimal dec when right is decimal rdec => dec - rdec,
			_ => null
		};
	}

	public static object? Multiply(object left, object right)
	{
		return left switch
		{
			int i when right is int ri => i * ri,
			int i when right is float rf => i * rf,
			int i when right is double rd => i * rd,
			int i when right is long rl => i * rl,
			int i when right is decimal rdec => i * rdec,
			float f when right is int ri => f * ri,
			float f when right is float rf => f * rf,
			float f when right is double rd => f * rd,
			float f when right is long rl => f * rl,
			float f when right is decimal rdec => f * (float) rdec,
			double d when right is int ri => d * ri,
			double d when right is float rf => d * rf,
			double d when right is double rd => d * rd,
			double d when right is long rl => d * rl,
			double d when right is decimal rdec => d * (double) rdec,
			long l when right is int ri => l * ri,
			long l when right is float rf => l * rf,
			long l when right is double rd => l * rd,
			long l when right is long rl => l * rl,
			long l when right is decimal rdec => l * rdec,
			decimal dec when right is int ri => dec * ri,
			decimal dec when right is float rf => dec * (decimal) rf,
			decimal dec when right is double rd => dec * (decimal) rd,
			decimal dec when right is long rl => dec * rl,
			decimal dec when right is decimal rdec => dec * rdec,
			_ => null
		};
	}

	public static object? Divide(object left, object right)
	{
		return left switch
		{
			int i when right is int ri => i / ri,
			int i when right is float rf => i / rf,
			int i when right is double rd => i / rd,
			int i when right is long rl => i / rl,
			int i when right is decimal rdec => i / rdec,
			float f when right is int ri => f / ri,
			float f when right is float rf => f / rf,
			float f when right is double rd => f / rd,
			float f when right is long rl => f / rl,
			float f when right is decimal rdec => f / (float) rdec,
			double d when right is int ri => d / ri,
			double d when right is float rf => d / rf,
			double d when right is double rd => d / rd,
			double d when right is long rl => d / rl,
			double d when right is decimal rdec => d / (double) rdec,
			long l when right is int ri => l / ri,
			long l when right is float rf => l / rf,
			long l when right is double rd => l / rd,
			long l when right is long rl => l / rl,
			long l when right is decimal rdec => l / rdec,
			decimal dec when right is int ri => dec / ri,
			decimal dec when right is float rf => dec / (decimal) rf,
			decimal dec when right is double rd => dec / (decimal) rd,
			decimal dec when right is long rl => dec / rl,
			decimal dec when right is decimal rdec => dec / rdec,
			_ => null
		};
	}
}