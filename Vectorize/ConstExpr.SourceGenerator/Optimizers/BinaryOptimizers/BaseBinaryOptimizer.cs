using System;
using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer
{
	public abstract BinaryOperatorKind Kind { get; }
	
	public ExpressionSyntax Left { get; init; }
	
	public ExpressionSyntax Right { get; init; }
	
	public ITypeSymbol Type => Operation.Type ?? throw new InvalidOperationException("Operation type is null");

	public FloatingPointEvaluationMode FloatingPointMode { get; init; }
	
	public IBinaryOperation Operation { get; init; }
	
	public abstract bool TryOptimize(bool hasLeftValue, object? leftValue, bool hasRightValue, object? rightValue, out SyntaxNode? result);

	protected static bool IsPure(IOperation? op)
	{
		return op switch
		{
			ILocalReferenceOperation => true,
			IParameterReferenceOperation => true,
			ILiteralOperation => true,
			IConversionOperation conv => IsPure(conv.Operand),
			IParenthesizedOperation par => IsPure(par.Operand),
			IFieldReferenceOperation f => f.Field.IsConst || f.Field.IsReadOnly,
			IBinaryOperation b => IsPure(b.LeftOperand) && IsPure(b.RightOperand),
			IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } u => IsPure(u.Operand),
			_ => false
		};
	}
}