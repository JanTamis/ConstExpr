using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
			SyntaxKind.ModuloAssignmentExpression => Modulo(variables[name], value),
			SyntaxKind.AndAssignmentExpression => BitwiseAnd(variables[name], value),
			SyntaxKind.ExclusiveOrAssignmentExpression => ExclusiveOr(variables[name], value),
			SyntaxKind.OrAssignmentExpression => BitwiseOr(variables[name], value),
			SyntaxKind.LeftShiftAssignmentExpression => LeftShift(variables[name], value),
			SyntaxKind.RightShiftAssignmentExpression => RightShift(variables[name], value),
			SyntaxKind.UnsignedRightShiftAssignmentExpression => UnsignedRightShift(variables[name], value),
			// SyntaxKind.CoalesceAssignmentExpression => Coalesce(variables[name], value),
			_ => variables[name]
		};

		return null;
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var left = GetVariableValue(Visit(node.Left), variables);
		var right = GetVariableValue(Visit(node.Right), variables);

		var kind = node.Kind();
		var method = left.GetType().GetRuntimeMethods().Where(w => w.IsPublic && w.IsStatic);

		return kind switch
		{
			SyntaxKind.AddExpression => CreateLiteral(Add(left, right)),
			SyntaxKind.SubtractExpression => CreateLiteral(Subtract(left, right)),
			SyntaxKind.MultiplyExpression => CreateLiteral(Multiply(left, right)),
			SyntaxKind.DivideExpression => CreateLiteral(Divide(left, right)),
			SyntaxKind.ModuloExpression => CreateLiteral(Modulo(left, right)),
			SyntaxKind.LeftShiftExpression => CreateLiteral(LeftShift(left, right)),
			SyntaxKind.RightShiftExpression => CreateLiteral(RightShift(left, right)),
			SyntaxKind.UnsignedRightShiftExpression => CreateLiteral(UnsignedRightShift(left, right)),
			SyntaxKind.LogicalOrExpression => CreateLiteral(left is true || right is true),
			SyntaxKind.LogicalAndExpression => CreateLiteral(left is true && right is true),
			SyntaxKind.BitwiseOrExpression => CreateLiteral(BitwiseOr(left, right)),
			SyntaxKind.BitwiseAndExpression => CreateLiteral(BitwiseAnd(left, right)),
			SyntaxKind.ExclusiveOrExpression => CreateLiteral(ExclusiveOr(left, right)),
			SyntaxKind.EqualsExpression when Comparer.Default.Compare(left, right) is 0 => CreateLiteral(true),
			SyntaxKind.NotEqualsExpression when Comparer.Default.Compare(left, right) is not 0 => CreateLiteral(true),
			SyntaxKind.LessThanExpression when Comparer.Default.Compare(left, right) < 0 => CreateLiteral(true),
			SyntaxKind.LessThanOrEqualExpression when Comparer.Default.Compare(left, right) <= 0 => CreateLiteral(true),
			SyntaxKind.GreaterThanExpression when Comparer.Default.Compare(left, right) > 0 => CreateLiteral(true),
			SyntaxKind.GreaterThanOrEqualExpression when Comparer.Default.Compare(left, right) >= 0 => CreateLiteral(true),
			// SyntaxKind.CoalesceExpression => CreateLiteral(Coalesce(left, right)),
			_ => CreateLiteral(false),
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

		return null;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		for (Visit(node.Declaration); GetVariableValue(Visit(node.Condition), variables) is true; VisitList(node.Incrementors))
		{
			Visit(node.Statement);
		}

		return null;
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

		return null;
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

		return null;
	}

	public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
	{
		var builder = new StringBuilder();

		foreach (var content in node.Contents)
		{
			builder.Append(GetVariableValue(Visit(content), variables));
		}

		return CreateLiteral(builder.ToString());
	}

	public override SyntaxNode? VisitInterpolation(InterpolationSyntax node)
	{
		var value = GetVariableValue(node.Expression, variables);
		var format = node.FormatClause?.FormatStringToken.ValueText;

		if (format is not null && value is IFormattable formattable)
		{
			return CreateLiteral(formattable.ToString(format, null));
		}

		return CreateLiteral(value.ToString());
	}

	public override SyntaxNode? VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
	{
		var text = node.TextToken.ValueText;

		return CreateLiteral(text);
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		var value = GetVariableValue(Visit(node.Expression), variables);

		return node.WithExpression(CreateLiteral(value));
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var name = node.Identifier.Text;
		var value = Visit(node.Initializer.Value);

		if (value is LiteralExpressionSyntax literal)
		{
			variables[name] = literal.Token.Value;
		}

		return null;
	}
	
	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		Visit(node.Declaration);

		return null;
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		while (GetVariableValue(Visit(node.Condition), variables) is true)
		{
			Visit(node.Statement);
		}

		return null;
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
					.Select(argument => (float) GetVariableValue(Visit(argument.Expression), variables))
					.ToArray();

				return name switch
				{
					nameof(MathF.Abs) => CreateLiteral(MathF.Abs(arguments[0])),
					nameof(MathF.Acos) => CreateLiteral(MathF.Acos(arguments[0])),
					// nameof(MathF.Acosh) => CreateLiteral(MathF.Acosh((float) arguments[0])),
					nameof(MathF.Asin) => CreateLiteral(MathF.Asin(arguments[0])),
					// nameof(MathF.Asinh) => CreateLiteral(MathF.Asinh((float) arguments[0])),
					nameof(MathF.Atan) => CreateLiteral(MathF.Atan(arguments[0])),
					nameof(MathF.Atan2) => CreateLiteral(MathF.Atan2(arguments[0], arguments[1])),
					// nameof(MathF.Atanh) => CreateLiteral(MathF.Atanh((float) arguments[0])),
					"Cbrt" => CreateLiteral(MathF.Pow(arguments[0], 1f / 3f)),
					nameof(MathF.Ceiling) => CreateLiteral(MathF.Ceiling(arguments[0])),
					"Clamp" => CreateLiteral(MathF.Min(MathF.Max(arguments[0], arguments[1]), arguments[2])),
					nameof(MathF.Cos) => CreateLiteral(MathF.Cos(arguments[0])),
					nameof(MathF.Cosh) => CreateLiteral(MathF.Cosh(arguments[0])),
					nameof(MathF.Exp) => CreateLiteral(MathF.Exp(arguments[0])),
					nameof(MathF.Floor) => CreateLiteral(MathF.Floor(arguments[0])),
					// nameof(MathF.FusedMultiplyAdd) => CreateLiteral(MathF.FusedMultiplyAdd((float) arguments[0], (float) arguments[1], (float) arguments[2])),
					nameof(MathF.IEEERemainder) => CreateLiteral(MathF.IEEERemainder(arguments[0], arguments[1])),
					nameof(MathF.Log) => CreateLiteral(MathF.Log(arguments[0])),
					nameof(MathF.Log10) => CreateLiteral(MathF.Log10(arguments[0])),
					// nameof(MathF.Log2) => CreateLiteral(MathF.Log2((float) arguments[0])),
					nameof(MathF.Max) => CreateLiteral(MathF.Max(arguments[0], arguments[1])),
					nameof(MathF.Min) => CreateLiteral(MathF.Min(arguments[0], arguments[1])),
					nameof(MathF.Pow) => CreateLiteral(MathF.Pow(arguments[0], arguments[1])),
					nameof(MathF.Round) => CreateLiteral(MathF.Round(arguments[0])),
					nameof(MathF.Sign) => CreateLiteral(MathF.Sign(arguments[0])),
					nameof(MathF.Sin) => CreateLiteral(MathF.Sin(arguments[0])),
					nameof(MathF.Sinh) => CreateLiteral(MathF.Sinh(arguments[0])),
					nameof(MathF.Sqrt) => CreateLiteral(MathF.Sqrt(arguments[0])),
					nameof(MathF.Tan) => CreateLiteral(MathF.Tan(arguments[0])),
					nameof(MathF.Tanh) => CreateLiteral(MathF.Tanh(arguments[0])),
					nameof(MathF.Truncate) => CreateLiteral(MathF.Truncate(arguments[0])),
					_ => node
				};
			}

			if (memberName is nameof(Math))
			{
				var arguments = node.ArgumentList.Arguments
					.Select(argument => (double) GetVariableValue(Visit(argument.Expression), variables))
					.ToArray();

				return name switch
				{
					nameof(Math.Abs) => CreateLiteral(Math.Abs(arguments[0])),
					nameof(Math.Acos) => CreateLiteral(Math.Acos(arguments[0])),
					// nameof(Math.Acosh) => CreateLiteral(Math.Acosh((double) arguments[0])),
					nameof(Math.Asin) => CreateLiteral(Math.Asin(arguments[0])),
					// nameof(Math.Asinh) => CreateLiteral(Math.Asinh((double) arguments[0])),
					nameof(Math.Atan) => CreateLiteral(Math.Atan(arguments[0])),
					nameof(Math.Atan2) => CreateLiteral(Math.Atan2(arguments[0], arguments[1])),
					// nameof(Math.Atanh) => CreateLiteral(Math.Atanh((double) arguments[0])),
					"Cbrt" => CreateLiteral(Math.Pow(arguments[0], 1d / 3d)),
					nameof(Math.Ceiling) => CreateLiteral(Math.Ceiling(arguments[0])),
					"Clamp" => CreateLiteral(Math.Min(Math.Max(arguments[0], arguments[1]), arguments[2])),
					nameof(Math.Cos) => CreateLiteral(Math.Cos(arguments[0])),
					nameof(Math.Cosh) => CreateLiteral(Math.Cosh(arguments[0])),
					nameof(Math.Exp) => CreateLiteral(Math.Exp(arguments[0])),
					nameof(Math.Floor) => CreateLiteral(Math.Floor(arguments[0])),
					// nameof(Math.FusedMultiplyAdd) => CreateLiteral(Math.FusedMultiplyAdd((double) arguments[0], (double) arguments[1], (double) arguments[2])),
					nameof(Math.IEEERemainder) => CreateLiteral(Math.IEEERemainder(arguments[0], arguments[1])),
					nameof(Math.Log) => CreateLiteral(Math.Log(arguments[0])),
					nameof(Math.Log10) => CreateLiteral(Math.Log10(arguments[0])),
					// nameof(Math.Log2) => CreateLiteral(Math.Log2((double) arguments[0])),
					nameof(Math.Max) => CreateLiteral(Math.Max(arguments[0], arguments[1])),
					nameof(Math.Min) => CreateLiteral(Math.Min(arguments[0], arguments[1])),
					nameof(Math.Pow) => CreateLiteral(Math.Pow(arguments[0], arguments[1])),
					nameof(Math.Round) => CreateLiteral(Math.Round(arguments[0])),
					nameof(Math.Sign) => CreateLiteral(Math.Sign(arguments[0])),
					nameof(Math.Sin) => CreateLiteral(Math.Sin(arguments[0])),
					nameof(Math.Sinh) => CreateLiteral(Math.Sinh(arguments[0])),
					nameof(Math.Sqrt) => CreateLiteral(Math.Sqrt(arguments[0])),
					nameof(Math.Tan) => CreateLiteral(Math.Tan(arguments[0])),
					nameof(Math.Tanh) => CreateLiteral(Math.Tanh(arguments[0])),
					nameof(Math.Truncate) => CreateLiteral(Math.Truncate(arguments[0])),
					_ => null,
				};
			}
		}

		return base.VisitInvocationExpression(node);
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		var value = GetVariableValue(node.Expression, variables);
		var name = node.Name.Identifier.Text;

		if (value is not null)
		{
			var type = value.GetType();
			var property = type.GetProperty(name);
			var field = type.GetField(name);

			if (property is not null)
			{
				return CreateLiteral(property.GetValue(value));
			}

			if (field is not null)
			{
				return CreateLiteral(field.GetValue(value));
			}
		}

		return null;
	}

	public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		var name = GetVariableName(Visit(node.Operand));
		var value = GetVariableValue(Visit(node.Operand), variables);

		var kind = node.Kind();

		variables[name] = kind switch
		{
			SyntaxKind.PostIncrementExpression => Add(value, 1),
			SyntaxKind.PostDecrementExpression => Subtract(value, 1),
			_ => variables[name]
		};

		return null;
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		var name = GetVariableName(Visit(node.Operand));
		var value = GetVariableValue(Visit(node.Operand), variables);

		var kind = node.Kind();

		variables[name] = kind switch
		{
			SyntaxKind.UnaryPlusExpression => value,
			SyntaxKind.UnaryMinusExpression => Subtract(0, value),
			SyntaxKind.BitwiseNotExpression => BitwiseNot(value),
			SyntaxKind.LogicalNotExpression => LogicalNot(value),
			SyntaxKind.PreIncrementExpression => Add(value, 1),
			SyntaxKind.PreDecrementExpression => Subtract(value, 1),
			_ => variables[name]
		};

		return null;
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var value = GetVariableValue(node.Expression, variables);
		var arguments = node.ArgumentList.Arguments
			.Select(s => GetVariableValue(Visit(s.Expression), variables))
			.ToArray();

		var type = value.GetType();
		var indexerProperty = type.GetRuntimeProperties()
			.Concat(type.BaseType.GetRuntimeProperties())
			.FirstOrDefault(p => p.GetIndexParameters().Length > 0);

		if (indexerProperty != null)
		{
			return CreateLiteral(indexerProperty.GetValue(value, arguments));
		}

		return null;
	}

	public override SyntaxNode? VisitSizeOfExpression(SizeOfExpressionSyntax node)
	{
		var type = node.Type;
		var size = type switch
		{
			PredefinedTypeSyntax predefinedType => predefinedType.Keyword.Kind() switch
			{
				SyntaxKind.BoolKeyword => Unsafe.SizeOf<bool>(),
				SyntaxKind.ByteKeyword => Unsafe.SizeOf<byte>(),
				SyntaxKind.SByteKeyword => Unsafe.SizeOf<sbyte>(),
				SyntaxKind.ShortKeyword => Unsafe.SizeOf<short>(),
				SyntaxKind.UShortKeyword => Unsafe.SizeOf<ushort>(),
				SyntaxKind.IntKeyword => Unsafe.SizeOf<int>(),
				SyntaxKind.UIntKeyword => Unsafe.SizeOf<uint>(),
				SyntaxKind.LongKeyword => Unsafe.SizeOf<long>(),
				SyntaxKind.ULongKeyword => Unsafe.SizeOf<ulong>(),
				SyntaxKind.FloatKeyword => Unsafe.SizeOf<float>(),
				SyntaxKind.DoubleKeyword => Unsafe.SizeOf<double>(),
				SyntaxKind.DecimalKeyword => Unsafe.SizeOf<decimal>(),
				SyntaxKind.CharKeyword => Unsafe.SizeOf<char>(),
				_ => throw new NotSupportedException($"Unsupported type: {predefinedType.Keyword.Kind()}")
			},
			_ => throw new NotSupportedException($"Unsupported type: {type.GetType().Name}")
		};

		return CreateLiteral(size);
	}

	public override SyntaxNode? VisitSwitchStatement(SwitchStatementSyntax node)
	{
		var switchValue = GetVariableValue(Visit(node.Expression), variables);

		foreach (var section in node.Sections)
		{
			foreach (var label in section.Labels)
			{
				if (label is CaseSwitchLabelSyntax caseLabel)
				{
					var caseValue = GetVariableValue(Visit(caseLabel.Value), variables);

					if (Equals(switchValue, caseValue))
					{
						foreach (var statement in section.Statements)
						{
							Visit(statement);
						}

						return null;
					}
				}
				else if (label is DefaultSwitchLabelSyntax)
				{
					foreach (var statement in section.Statements)
					{
						Visit(statement);
					}

					return null;
				}
			}
		}

		return null;
	}

	public override SyntaxNode? VisitTryStatement(TryStatementSyntax node)
	{
		try
		{
			Visit(node.Block);
		}
		catch (Exception ex)
		{
			foreach (var catchClause in node.Catches)
			{
				if (catchClause.Declaration is not null)
				{
					var exceptionType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type;

					if (exceptionType is not null && ex.GetType().IsAssignableTo(exceptionType))
					{
						variables[catchClause.Declaration.Identifier.Text] = ex;
						Visit(catchClause.Block);
						variables.Remove(catchClause.Declaration.Identifier.Text);
					}
				}
				else
				{
					Visit(catchClause.Block);
				}
			}
		}

		if (node.Finally is not null)
		{
			Visit(node.Finally.Block);
		}

		return null;
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);
		
		if (result is null)
		{
			return null;
		}
		
		return node.WithExpression((ExpressionSyntax) result);
	}

	public override SyntaxNode? VisitEmptyStatement(EmptyStatementSyntax node)
	{
		return null;
	}

	public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
	{
		return null;
	}
}
