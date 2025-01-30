using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			SyntaxKind.EqualsExpression when result is 0 => BoolLiteral(true),
			SyntaxKind.NotEqualsExpression when result is not 0 => BoolLiteral(true),
			SyntaxKind.GreaterThanExpression when result > 0 => BoolLiteral(true),
			SyntaxKind.GreaterThanOrEqualExpression when result >= 0 => BoolLiteral(true),
			SyntaxKind.LessThanExpression when result < 0 => BoolLiteral(true),
			SyntaxKind.LessThanOrEqualExpression when result <= 0 => BoolLiteral(true),
			_ => BoolLiteral(false),
		};
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		var condition = GetVariableValue(Visit(node.Condition), variables);

		return condition switch
		{
			true => Visit(node.WhenTrue),
			false => Visit(node.WhenFalse),
			_ => node
		};
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		var value = GetVariableValue(Visit(node.Expression), variables);

		if (node.Type is PredefinedTypeSyntax predefinedType)
		{
			var kind = predefinedType.Keyword.Kind() switch
			{
				SyntaxKind.BoolKeyword => TypeCode.Boolean,
				SyntaxKind.ByteKeyword => TypeCode.Byte,
				SyntaxKind.SByteKeyword => TypeCode.SByte,
				SyntaxKind.ShortKeyword => TypeCode.Int16,
				SyntaxKind.UShortKeyword => TypeCode.UInt16,
				SyntaxKind.IntKeyword => TypeCode.Int32,
				SyntaxKind.UIntKeyword => TypeCode.UInt32,
				SyntaxKind.LongKeyword => TypeCode.Int64,
				SyntaxKind.ULongKeyword => TypeCode.UInt64,
				SyntaxKind.FloatKeyword => TypeCode.Single,
				SyntaxKind.DoubleKeyword => TypeCode.Double,
				SyntaxKind.DecimalKeyword => TypeCode.Decimal,
				SyntaxKind.CharKeyword => TypeCode.Char,
				SyntaxKind.StringKeyword => TypeCode.String,
				_ => TypeCode.Empty
			};

			var result = Convert.ChangeType(value, kind);

			return LiteralExpression(GetSyntaxKind(value), Literal(result.ToString()));
		}

		return LiteralExpression(GetSyntaxKind(value), Literal(value.ToString()));
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		do
		{
			Visit(node.Statement);
		} while (GetVariableValue(Visit(node.Condition), variables) is true);
		
		return node;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		for (Visit(node.Declaration); GetVariableValue(Visit(node.Condition), variables) is true; VisitList(node.Incrementors))
		{
			Visit(node.Statement);
		}
		
		return node;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var variable = GetVariableValue(node.Expression, variables);

		if (variable is IEnumerable<object?> data)
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
	
	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = GetVariableValue(Visit(node.Condition), variables);
		
		if (condition is true)
		{
			Visit(node.Statement);
		}
		else if (node.Else is not null)
		{
			Visit(node.Else.Statement);
		}

		return node;
	}

	public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
	{
		var builder = new StringBuilder();
		
		foreach (var content in node.Contents)
		{
			builder.Append(GetVariableValue(Visit(content), variables));
		}
		
		return StringLiteral(builder.ToString());
	}

	public override SyntaxNode? VisitInterpolation(InterpolationSyntax node)
	{
		var value = GetVariableValue(node.Expression, variables);
		var format = node.FormatClause?.FormatStringToken.ValueText;
		
		if (format is not null && value is IFormattable formattable)
		{
			return StringLiteral(formattable.ToString(format, null));
		}
		
		return StringLiteral(value.ToString());
	}

	public override SyntaxNode? VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
	{
		var text = node.TextToken.ValueText;
		
		return StringLiteral(text);
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
	
	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		while (GetVariableValue(Visit(node.Condition), variables) is true)
		{
			Visit(node.Statement);
		}
		
		return node;
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		
		if (expression is MemberAccessExpressionSyntax memberAccess)
		{
			var name = memberAccess.Name.Identifier.Text;
			var memberName = memberAccess.Expression.ToString();

			if (TryGetVariableValue(memberAccess.Expression, variables, out var value))
			{
				// switch (name)
				// {
				// 	case "GetHashCode":
				// 		return NumberLiteral(value.GetHashCode());
				// 	case "ToString()":
				// 		return StringLiteral(value.ToString());
				// }
				
				var type = value.GetType();
				var method = type.GetMethod(name);
				
				if (method is not null)
				{
					var arguments = node.ArgumentList.Arguments
						.Select(argument => GetVariableValue(Visit(argument.Expression), variables))
						.ToArray();
					
					var result = method.Invoke(value, arguments);
					
					return LiteralExpression(GetSyntaxKind(result), Literal(result.ToString()));
				}
			}
			
			if (memberName is nameof(MathF))
			{
				var arguments = node.ArgumentList.Arguments
					.Select(argument => (float)GetVariableValue(Visit(argument.Expression), variables))
					.ToArray();
				
				return name switch
				{
					nameof(MathF.Abs) => NumberLiteral(MathF.Abs(arguments[0])),
					nameof(MathF.Acos) => NumberLiteral(MathF.Acos(arguments[0])),
					// nameof(MathF.Acosh) => NumberLiteral(MathF.Acosh((float) arguments[0])),
					nameof(MathF.Asin) => NumberLiteral(MathF.Asin(arguments[0])),
					// nameof(MathF.Asinh) => NumberLiteral(MathF.Asinh((float) arguments[0])),
					nameof(MathF.Atan) => NumberLiteral(MathF.Atan(arguments[0])),
					nameof(MathF.Atan2) => NumberLiteral(MathF.Atan2(arguments[0], arguments[1])),
					// nameof(MathF.Atanh) => NumberLiteral(MathF.Atanh((float) arguments[0])),
					"Cbrt" => NumberLiteral(MathF.Pow(arguments[0], 1f / 3f)),
					nameof(MathF.Ceiling) => NumberLiteral(MathF.Ceiling(arguments[0])),
					"Clamp" => NumberLiteral(MathF.Min(MathF.Max(arguments[0], arguments[1]), arguments[2])),
					nameof(MathF.Cos) => NumberLiteral(MathF.Cos(arguments[0])),
					nameof(MathF.Cosh) => NumberLiteral(MathF.Cosh(arguments[0])),
					nameof(MathF.Exp) => NumberLiteral(MathF.Exp(arguments[0])),
					nameof(MathF.Floor) => NumberLiteral(MathF.Floor(arguments[0])),
					// nameof(MathF.FusedMultiplyAdd) => NumberLiteral(MathF.FusedMultiplyAdd((float) arguments[0], (float) arguments[1], (float) arguments[2])),
					nameof(MathF.IEEERemainder) => NumberLiteral(MathF.IEEERemainder(arguments[0], arguments[1])),
					nameof(MathF.Log) => NumberLiteral(MathF.Log(arguments[0])),
					nameof(MathF.Log10) => NumberLiteral(MathF.Log10(arguments[0])),
					// nameof(MathF.Log2) => NumberLiteral(MathF.Log2((float) arguments[0])),
					nameof(MathF.Max) => NumberLiteral(MathF.Max(arguments[0], arguments[1])),
					nameof(MathF.Min) => NumberLiteral(MathF.Min(arguments[0], arguments[1])),
					nameof(MathF.Pow) => NumberLiteral(MathF.Pow(arguments[0], arguments[1])),
					nameof(MathF.Round) => NumberLiteral(MathF.Round(arguments[0])),
					nameof(MathF.Sign) => NumberLiteral(MathF.Sign(arguments[0])),
					nameof(MathF.Sin) => NumberLiteral(MathF.Sin(arguments[0])),
					nameof(MathF.Sinh) => NumberLiteral(MathF.Sinh(arguments[0])),
					nameof(MathF.Sqrt) => NumberLiteral(MathF.Sqrt(arguments[0])),
					nameof(MathF.Tan) => NumberLiteral(MathF.Tan(arguments[0])),
					nameof(MathF.Tanh) => NumberLiteral(MathF.Tanh(arguments[0])),
					nameof(MathF.Truncate) => NumberLiteral(MathF.Truncate(arguments[0])),
					_ => node
				};
			}
			
			if (memberName is nameof(Math))
			{
				var arguments = node.ArgumentList.Arguments
					.Select(argument => (double)GetVariableValue(Visit(argument.Expression), variables))
					.ToArray();
				
				return name switch
				{
					nameof(Math.Abs) => NumberLiteral(Math.Abs(arguments[0])),
					nameof(Math.Acos) => NumberLiteral(Math.Acos(arguments[0])),
					// nameof(Math.Acosh) => NumberLiteral(Math.Acosh((double) arguments[0])),
					nameof(Math.Asin) => NumberLiteral(Math.Asin(arguments[0])),
					// nameof(Math.Asinh) => NumberLiteral(Math.Asinh((double) arguments[0])),
					nameof(Math.Atan) => NumberLiteral(Math.Atan(arguments[0])),
					nameof(Math.Atan2) => NumberLiteral(Math.Atan2(arguments[0], arguments[1])),
					// nameof(Math.Atanh) => NumberLiteral(Math.Atanh((double) arguments[0])),
					"Cbrt" => NumberLiteral(Math.Pow(arguments[0], 1d / 3d)),
					nameof(Math.Ceiling) => NumberLiteral(Math.Ceiling(arguments[0])),
					"Clamp" => NumberLiteral(Math.Min(Math.Max(arguments[0], arguments[1]), arguments[2])),
					nameof(Math.Cos) => NumberLiteral(Math.Cos(arguments[0])),
					nameof(Math.Cosh) => NumberLiteral(Math.Cosh(arguments[0])),
					nameof(Math.Exp) => NumberLiteral(Math.Exp(arguments[0])),
					nameof(Math.Floor) => NumberLiteral(Math.Floor(arguments[0])),
					// nameof(Math.FusedMultiplyAdd) => NumberLiteral(Math.FusedMultiplyAdd((double) arguments[0], (double) arguments[1], (double) arguments[2])),
					nameof(Math.IEEERemainder) => NumberLiteral(Math.IEEERemainder(arguments[0], arguments[1])),
					nameof(Math.Log) => NumberLiteral(Math.Log(arguments[0])),
					nameof(Math.Log10) => NumberLiteral(Math.Log10(arguments[0])),
					// nameof(Math.Log2) => NumberLiteral(Math.Log2((double) arguments[0])),
					nameof(Math.Max) => NumberLiteral(Math.Max(arguments[0], arguments[1])),
					nameof(Math.Min) => NumberLiteral(Math.Min(arguments[0], arguments[1])),
					nameof(Math.Pow) => NumberLiteral(Math.Pow(arguments[0], arguments[1])),
					nameof(Math.Round) => NumberLiteral(Math.Round(arguments[0])),
					nameof(Math.Sign) => NumberLiteral(Math.Sign(arguments[0])),
					nameof(Math.Sin) => NumberLiteral(Math.Sin(arguments[0])),
					nameof(Math.Sinh) => NumberLiteral(Math.Sinh(arguments[0])),
					nameof(Math.Sqrt) => NumberLiteral(Math.Sqrt(arguments[0])),
					nameof(Math.Tan) => NumberLiteral(Math.Tan(arguments[0])),
					nameof(Math.Tanh) => NumberLiteral(Math.Tanh(arguments[0])),
					nameof(Math.Truncate) => NumberLiteral(Math.Truncate(arguments[0])),
					_ => node
				};
			}
		}
		
		return base.VisitInvocationExpression(node);
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		return base.VisitMemberAccessExpression(node);
	}

	public override SyntaxNode? VisitSwitchStatement(SwitchStatementSyntax node)
	{
		return base.VisitSwitchStatement(node);
	}
}