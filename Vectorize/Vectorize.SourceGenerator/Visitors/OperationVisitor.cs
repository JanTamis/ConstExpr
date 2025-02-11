using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Vectorize.Helpers;

namespace Vectorize.Visitors;

public partial class OperationVisitor
{
	public virtual object? Visit(IOperation? operation)
	{
		if (operation is null)
		{
			return null;
		}

		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return value;
		}
		
		return operation switch
		{
			IIsNullOperation isNullOperation => VisitIsNull(isNullOperation),
			IAddressOfOperation addressOfOperation => VisitAddressOf(addressOfOperation),
			IArrayCreationOperation arrayCreationOperation => VisitArrayCreation(arrayCreationOperation),
			IArrayElementReferenceOperation arrayElementReferenceOperation => VisitArrayElementReference(arrayElementReferenceOperation),
			ICoalesceAssignmentOperation coalesceAssignmentOperation => VisitCoalesceAssignment(coalesceAssignmentOperation),
			ICompoundAssignmentOperation compoundAssignmentOperation => VisitCompoundAssignment(compoundAssignmentOperation),
			IDeconstructionAssignmentOperation deconstructionAssignmentOperation => VisitDeconstructionAssignment(deconstructionAssignmentOperation),
			ISimpleAssignmentOperation simpleAssignmentOperation => VisitSimpleAssignment(simpleAssignmentOperation),
			IAttributeOperation attributeOperation => VisitAttribute(attributeOperation),
			IAwaitOperation awaitOperation => VisitAwait(awaitOperation),
			IBinaryOperation binaryOperation => VisitBinaryOperation(binaryOperation),
			IBinaryPatternOperation binaryPatternOperation => VisitBinaryPattern(binaryPatternOperation),
			IBlockOperation blockOperation => VisitBlock(blockOperation),
			IBranchOperation branchOperation => VisitBranch(branchOperation),
			IDefaultCaseClauseOperation defaultCaseClause => VisitDefaultCaseClause(defaultCaseClause),
			IPatternCaseClauseOperation patternCaseClause => VisitPatternCaseClause(patternCaseClause),
			IRangeCaseClauseOperation rangeCaseClause => VisitRangeCaseClause(rangeCaseClause),
			IRelationalCaseClauseOperation relationalCaseClause => VisitRelationalCaseClause(relationalCaseClause),
			ISingleValueCaseClauseOperation singleValueCaseClause => VisitSingleValueCaseClause(singleValueCaseClause),
			ICatchClauseOperation catchClauseOperation => VisitCatchClause(catchClauseOperation),
			_ => null,
		};
	}
	
	public virtual object? VisitIsNull(IIsNullOperation operation)
	{
		return Visit(operation.Operand) is null;
	}
	
	public virtual object? VisitAddressOf(IAddressOfOperation operation)
	{
		return Visit(operation.Reference);
	}
	
	public virtual object? VisitArrayCreation(IArrayCreationOperation operation)
	{
		return operation.Initializer?.ElementValues
			.Select(Visit)
			.ToArray();
	}
	
	public virtual object? VisitArrayElementReference(IArrayElementReferenceOperation operation)
	{
		var array = Visit(operation.ArrayReference);
		
		if (array is null)
		{
			return null;
		}
		
		var indexers = operation.Indices
			.Select(Visit)
			.ToArray();


		foreach (var pi in array.GetType().GetProperties())
		{
			if (pi.GetIndexParameters().Length == indexers.Length)
			{
				return pi.GetValue(array, indexers);
			}
		}
		
		return null;
	}
	
	public virtual object? VisitCoalesceAssignment(ICoalesceAssignmentOperation operation)
	{
		var target = Visit(operation.Target);
		var value = Visit(operation.Value);
		
		return target ?? value;
	}
	
	public virtual object? VisitCompoundAssignment(ICompoundAssignmentOperation operation)
	{
		var target = Visit(operation.Target);
		var value = Visit(operation.Value);
		
		if (target is null)
		{
			return null;
		}
		
		return ExecuteBinaryOperation(operation.OperatorKind, target, value);
	}
	
	public virtual object? VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation)
	{
		var target = Visit(operation.Target);
		var value = Visit(operation.Value);

		if (target is not object?[] array)
		{
			return null;
		}
		
		for (var i = 0; i < array.Length; i++)
		{
			array[i] = value is object?[] values
				? values[i]
				: null;
		}
		
		return array;
	}
	
	public virtual object? VisitSimpleAssignment(ISimpleAssignmentOperation operation)
	{
		return Visit(operation.Value);
	}
	
	public virtual object? VisitAttribute(IAttributeOperation operation)
	{
		return Visit(operation.Operation);
	}
	
	public virtual object? VisitAwait(IAwaitOperation operation)
	{
		return Visit(operation.Operation);
	}
	
	public virtual object? VisitBinaryOperation(IBinaryOperation operation)
	{
		var left = Visit(operation.LeftOperand);
		var right = Visit(operation.RightOperand);
		var operatorKind = operation.OperatorKind;
		var method = operation.OperatorMethod;
		
		if (method != null)
		{
			return SyntaxHelpers.ExecuteMethod(method, null, left, right);
		}
		
		return ExecuteBinaryOperation(operatorKind, left, right);
	}
	
	public virtual object? VisitBinaryPattern(IBinaryPatternOperation operation)
	{
		var left = Visit(operation.LeftPattern);
		var right = Visit(operation.RightPattern);
		var operatorKind = operation.OperatorKind;
		
		return ExecuteBinaryOperation(operatorKind, left, right);
	}
	
	public virtual object? VisitBlock(IBlockOperation operation)
	{
		return operation.Operations
			.Select(Visit)
			.ToArray();
	}
	
	public virtual object? VisitBranch(IBranchOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitDefaultCaseClause(IDefaultCaseClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitPatternCaseClause(IPatternCaseClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitRangeCaseClause(IRangeCaseClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitRelationalCaseClause(IRelationalCaseClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitSingleValueCaseClause(ISingleValueCaseClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitCatchClause(ICatchClauseOperation operation)
	{
		return null;
	}
	
	public virtual object? VisitCoalesce(ICoalesceOperation operation)
	{
		return Visit(operation.Value) ?? Visit(operation.WhenNull);
	}
	
	public virtual object? VisitCollectionExpression(ICollectionExpressionOperation operation)
	{
		return operation.Elements
			.Select(Visit);
	}
}