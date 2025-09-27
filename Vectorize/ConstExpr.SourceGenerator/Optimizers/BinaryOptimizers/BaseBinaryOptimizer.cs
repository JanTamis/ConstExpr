using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Helpers;
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
	
	public ExpressionSyntax Right { get; init; }
	
	public ITypeSymbol Type { get; init; }

	public FloatingPointEvaluationMode FloatingPointMode { get; init; }
	
	public abstract bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result);
	
	public static BaseBinaryOptimizer? Create(BinaryOperatorKind kind, ITypeSymbol type, ExpressionSyntax leftExpr, ExpressionSyntax rightExpr, FloatingPointEvaluationMode mode)
	{
		return kind switch
		{
			BinaryOperatorKind.Add => new BinaryAddOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Subtract => new BinarySubtractOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Multiply => new BinaryMultiplyOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Divide => new BinaryDivideOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.Remainder => new BinaryModuloOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.LeftShift => new BinaryLeftShiftOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
			BinaryOperatorKind.RightShift => new BinaryRightShiftOptimizer { Left = leftExpr, Right = rightExpr, Type = type, FloatingPointMode = mode },
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
			_ => false
		};
	}
}