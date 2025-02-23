using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Vectorize.Helpers;

namespace Vectorize.Visitors;

// TODO: Add support for more operationsS
public class ExpressionVisitor(Compilation compilation, IEnumerable<ParameterExpression> parameters) : OperationVisitor<Dictionary<string, object?>, Expression?>
{
	public override Expression? DefaultVisit(IOperation operation, Dictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return Expression.Constant(value);
		}

		foreach (var currentOperation in operation.ChildOperations)
		{
			Visit(currentOperation, argument);
		}

		return null;
	}

	public override Expression? VisitBlock(IBlockOperation operation, Dictionary<string, object?> argument)
	{
		var items = operation.Operations
			.Select(item => Visit(item, argument))
			.SelectMany<Expression, Expression>(s => s is BlockExpression blockExpression ? blockExpression.Expressions : [s])
			.ToArray();

		return Expression.Block(parameters, items);
	}

	public override Expression? VisitReturn(IReturnOperation operation, Dictionary<string, object?> argument)
	{
		var returnType = SyntaxHelpers.GetTypeByType(compilation, operation.ReturnedValue.Type);
		var returnLabel = Expression.Label(returnType);
		var returnValue = Visit(operation.ReturnedValue, argument);

		return Expression.Block(
			Expression.Return(returnLabel, returnValue, returnType),
			Expression.Label(returnLabel, Expression.Default(returnType))
		);
	}

	public override Expression? VisitBinaryOperator(IBinaryOperation operation, Dictionary<string, object?> argument)
	{
		var kind = operation.OperatorKind switch
		{
			BinaryOperatorKind.Add => ExpressionType.Add,
			BinaryOperatorKind.Subtract => ExpressionType.Subtract,
			BinaryOperatorKind.Multiply => ExpressionType.Multiply,
			BinaryOperatorKind.Divide => ExpressionType.Divide,
			BinaryOperatorKind.Remainder => ExpressionType.Modulo,
			BinaryOperatorKind.Equals => ExpressionType.Equal,
			BinaryOperatorKind.NotEquals => ExpressionType.NotEqual,
			BinaryOperatorKind.LessThan => ExpressionType.LessThan,
			BinaryOperatorKind.LessThanOrEqual => ExpressionType.LessThanOrEqual,
			BinaryOperatorKind.GreaterThan => ExpressionType.GreaterThan,
			BinaryOperatorKind.GreaterThanOrEqual => ExpressionType.GreaterThanOrEqual,
			_ => throw new NotImplementedException(),
		};
		
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);
		
		return Expression.MakeBinary(kind, left, right);
	}

	public override Expression? VisitParameterReference(IParameterReferenceOperation operation, Dictionary<string, object?> argument)
	{
		return parameters.FirstOrDefault(x => x.Name == operation.Parameter.Name);
	}
}