using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Vectorize.Operators;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Rewriters;

public class ConstExprRewriter(Compilation compilation, Dictionary<string, object?> variables, CancellationToken token) : CSharpSyntaxRewriter
{
	private readonly OperatorHelper OperatorHelper = new(variables);

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (TryGetOperation(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}
		
		return null;
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		if (TryGetOperation<IBinaryOperation>(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}

		return null;
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		if (TryGetOperation<IConditionalOperation>(node, out var operation))
		{
			OperatorHelper.GetConstantValue(operation);
		}

		return null;
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		if (TryGetOperation<IConversionOperation>(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}
		
		return null;
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		do
		{
			Visit(node.Statement);
		} while (GetVariableValue(compilation, Visit(node.Condition), variables) is true);

		return null;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		for (Visit(node.Declaration); GetVariableValue(compilation, Visit(node.Condition), variables) is true; VisitList(node.Incrementors))
		{
			Visit(node.Statement);
		}

		return null;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		if (TryGetOperation<IForEachLoopOperation>(node, out var operation))
		{
			OperatorHelper.GetConstantValue(operation);
		}

		return null;
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		if (TryGetOperation<IConditionalOperation>(node, out var operation))
		{
			OperatorHelper.GetConstantValue(operation);
		}

		return null;
	}

	public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
	{
		var builder = new StringBuilder();

		foreach (var content in node.Contents)
		{
			builder.Append(GetVariableValue(compilation, Visit(content), variables));
		}

		return CreateLiteral(builder.ToString());
	}

	public override SyntaxNode? VisitInterpolation(InterpolationSyntax node)
	{
		var value = GetVariableValue(compilation, node.Expression, variables);
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
		var value = GetVariableValue(compilation, Visit(node.Expression), variables);

		return node.WithExpression(CreateLiteral(value));
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		if (TryGetOperation<IVariableDeclaratorOperation>(node, out var operation))
		{
			OperatorHelper.GetConstantValue(operation);
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
		while (GetVariableValue(compilation, Visit(node.Condition), variables) is true)
		{
			Visit(node.Statement);
		}

		return null;
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (TryGetOperation<IInvocationOperation>(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}
		
		return null;
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		if (TryGetOperation(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}
		
		return null;
	}

	public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		if (TryGetSemanticModel(compilation, node, out var semanticModel) && semanticModel.GetConstantValue(node) is { HasValue: true, Value: var temp })
		{
			return CreateLiteral(temp);
		}

		var name = GetVariableName(Visit(node.Operand));
		var value = GetVariableValue(compilation, Visit(node.Operand), variables);

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
		if (GetSemanticModel(compilation, node).GetConstantValue(node) is { HasValue: true, Value: var temp })
		{
			return CreateLiteral(temp);
		}

		var name = GetVariableName(Visit(node.Operand));
		var value = GetVariableValue(compilation, Visit(node.Operand), variables);

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
		var value = GetVariableValue(compilation, node.Expression, variables);
		var arguments = node.ArgumentList.Arguments
			.Select(s => GetVariableValue(compilation, Visit(s.Expression), variables))
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
		if (TryGetOperation<ISizeOfOperation>(node, out var operation))
		{
			return CreateLiteral(OperatorHelper.GetConstantValue(operation));
		}
		
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
		var switchValue = GetVariableValue(compilation, Visit(node.Expression), variables);

		foreach (var section in node.Sections)
		{
			foreach (var label in section.Labels)
			{
				switch (label)
				{
					case CaseSwitchLabelSyntax caseLabel:
					{
						var caseValue = GetVariableValue(compilation, Visit(caseLabel.Value), variables);

						if (Equals(switchValue, caseValue))
						{
							foreach (var statement in section.Statements)
							{
								Visit(statement);
							}

							return null;
						}
						break;
					}
					case DefaultSwitchLabelSyntax:
					{
						foreach (var statement in section.Statements)
						{
							Visit(statement);
						}

						return null;
					}
				}
			}
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
	
	private IOperation? GetOperation(SyntaxNode node)
	{
		if (TryGetSemanticModel(compilation, node, out var semanticModel))
		{
			return semanticModel.GetOperation(node, token);
		}
		
		return null;
	}

	private bool TryGetOperation(SyntaxNode node, out IOperation? operation)
	{
		if (TryGetSemanticModel(compilation, node, out var semanticModel))
		{
			operation = semanticModel.GetOperation(node, token);

			return operation is not null;
		}

		operation = null;
		return false;
	}

	private bool TryGetOperation<TOperation>(SyntaxNode node, out TOperation? operation) where TOperation : IOperation
	{
		if (TryGetOperation(node, out var temp))
		{
			if (temp is TOperation result)
			{
				operation = result;
				return true;
			}
		}

		operation = default;
		return false;
	}
}