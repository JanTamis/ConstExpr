using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer
{
	public abstract BinaryOperatorKind Kind { get; }
	
	public ExpressionSyntax Left { get; init; }
	public ITypeSymbol? LeftType { get; init; }
	
	public ExpressionSyntax Right { get; init; }
	public ITypeSymbol? RightType { get; init; }
	
	public ITypeSymbol Type { get; init; }

	public FloatingPointEvaluationMode FloatingPointMode { get; init; }
	
	public abstract bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result);
	
	public static BaseBinaryOptimizer? Create(BinaryOperatorKind kind, ITypeSymbol type, ExpressionSyntax leftExpr, ITypeSymbol? leftType, ExpressionSyntax rightExpr, ITypeSymbol? rightType, FloatingPointEvaluationMode mode)
	{
		return kind switch
		{
			BinaryOperatorKind.Add => new BinaryAddOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Subtract => new BinarySubtractOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Multiply => new BinaryMultiplyOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Divide => new BinaryDivideOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Remainder => new BinaryModuloOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.LeftShift => new BinaryLeftShiftOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.RightShift => new BinaryRightShiftOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.LessThan => new BinaryLessThanOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.GreaterThan => new BinaryGreaterThanOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.LessThanOrEqual => new BinaryLessThanOrEqualOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.GreaterThanOrEqual => new BinaryGreaterThanOrEqualOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Equals => new BinaryEqualsOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.NotEquals => new BinaryNotEqualsOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.And => new BinaryAndOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Or => new BinaryOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.ExclusiveOr => new BinaryExclusiveOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.ConditionalAnd => new BinaryConditionalAndOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.ConditionalOr => new BinaryConditionalOrOptimizer { Left = leftExpr, LeftType = leftType, Right = rightExpr, RightType = rightType, Type = type, FloatingPointMode = mode },
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
}