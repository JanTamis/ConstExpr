using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer
{
	public abstract BinaryOperatorKind Kind { get; }

	public ExpressionSyntax Left { get; init; }
	public ITypeSymbol? LeftType { get; init; }

	public ExpressionSyntax Right { get; init; }
	public ITypeSymbol? RightType { get; init; }

	public ITypeSymbol Type { get; init; }

	// public abstract bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result);

	public abstract IEnumerable<IBinaryStrategy> GetStrategies();

	public static BaseBinaryOptimizer? Create(BinaryOperatorKind kind, ITypeSymbol type, ExpressionSyntax leftExpr, ITypeSymbol? leftType, ExpressionSyntax rightExpr, ITypeSymbol? rightType, FloatingPointEvaluationMode mode)
	{
		return kind switch
		{
			BinaryOperatorKind.Add => new BinaryAddOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Subtract => new BinarySubtractOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Multiply => new BinaryMultiplyOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Divide => new BinaryDivideOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Remainder => new BinaryModuloOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.LeftShift => new BinaryLeftShiftOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.RightShift => new BinaryRightShiftOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.LessThan => new BinaryLessThanOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.GreaterThan => new BinaryGreaterThanOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.LessThanOrEqual => new BinaryLessThanOrEqualOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.GreaterThanOrEqual => new BinaryGreaterThanOrEqualOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Equals => new BinaryEqualsOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.NotEquals => new BinaryNotEqualsOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.And => new BinaryAndOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.Or => new BinaryOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.ExclusiveOr => new BinaryExclusiveOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.ConditionalAnd => new BinaryConditionalAndOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			BinaryOperatorKind.ConditionalOr => new BinaryConditionalOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type },
			_ => null
		};
	}

	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}

	protected bool LeftEqualsRight(IDictionary<string, VariableItem> variables)
	{
		return Left.IsEquivalentTo(Right) ||
					 (Left is IdentifierNameSyntax leftIdentifier
						&& Right is IdentifierNameSyntax rightIdentifier
						&& variables.TryGetValue(leftIdentifier.Identifier.Text, out var leftVar)
						&& variables.TryGetValue(rightIdentifier.Identifier.Text, out var rightVar)
						&& leftVar.Value is ArgumentSyntax leftArgument
						&& rightVar.Value is ArgumentSyntax rightArgument
						&& leftArgument.Expression.IsEquivalentTo(rightArgument.Expression));
	}
}