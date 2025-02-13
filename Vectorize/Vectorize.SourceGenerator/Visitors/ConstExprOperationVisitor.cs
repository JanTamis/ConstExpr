using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Vectorize.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace Vectorize.Visitors;

public partial class ConstExprOperationVisitor : OperationVisitor<Dictionary<string, object?>, object?>
{
	public override object? DefaultVisit(IOperation operation, Dictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return value;
		}

		foreach (var currentOperation in operation.ChildOperations)
		{
			Visit(currentOperation, argument);
		}

		return null;
	}

	public override object? VisitIsNull(IIsNullOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument) is null;
	}

	public override object? VisitArrayCreation(IArrayCreationOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Initializer?.ElementValues
			.Select(value => Visit(value, argument))
			.ToArray();
	}

	public override object? VisitArrayElementReference(IArrayElementReferenceOperation operation, Dictionary<string, object?> argument)
	{
		var array = Visit(operation.ArrayReference, argument);

		if (array is null)
		{
			return null;
		}

		var indexers = operation.Indices
			.Select(s => Visit(s, argument))
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

	public override object? VisitCoalesce(ICoalesceOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Value, argument) ?? Visit(operation.WhenNull, argument);
	}

	public override object? VisitCompoundAssignment(ICompoundAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		if (target is null)
		{
			return null;
		}

		return ExecuteBinaryOperation(operation.OperatorKind, target, value);
	}

	public override object? VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

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

	public override object? VisitSimpleAssignment(ISimpleAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitBinaryOperator(IBinaryOperation operation, Dictionary<string, object?> argument)
	{
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);
		var operatorKind = operation.OperatorKind;
		var method = operation.OperatorMethod;

		if (method is not null)
		{
			return SyntaxHelpers.ExecuteMethod(method, null, left, right);
		}

		return ExecuteBinaryOperation(operatorKind, left, right);
	}

	public override object? VisitBlock(IBlockOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var currentOperation in operation.Operations)
		{
			Visit(currentOperation, argument);
		}

		return null;
	}

	public override object? VisitCoalesceAssignment(ICoalesceAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);

		if (target is not null)
		{
			return target;
		}

		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitCollectionExpression(ICollectionExpressionOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Elements
			.Select(s => Visit(s, argument));
	}

	public override object? VisitConditional(IConditionalOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Condition, argument) switch
		{
			true => Visit(operation.WhenTrue, argument),
			false => Visit(operation.WhenFalse, argument),
			_ => null,
		};
	}

	public override object? VisitConditionalAccess(IConditionalAccessOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operation, argument) is not null
			? Visit(operation.WhenNotNull, argument)
			: null;
	}

	public override object? VisitConversion(IConversionOperation operation, Dictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var conversion = operation.Type;

		return conversion?.SpecialType switch
		{
			SpecialType.System_Boolean => Convert.ToBoolean(operand),
			SpecialType.System_Byte => Convert.ToByte(operand),
			SpecialType.System_Char => Convert.ToChar(operand),
			SpecialType.System_DateTime => Convert.ToDateTime(operand),
			SpecialType.System_Decimal => Convert.ToDecimal(operand),
			SpecialType.System_Double => Convert.ToDouble(operand),
			SpecialType.System_Int16 => Convert.ToInt16(operand),
			SpecialType.System_Int32 => Convert.ToInt32(operand),
			SpecialType.System_Int64 => Convert.ToInt64(operand),
			SpecialType.System_SByte => Convert.ToSByte(operand),
			SpecialType.System_Single => Convert.ToSingle(operand),
			SpecialType.System_String => Convert.ToString(operand),
			SpecialType.System_UInt16 => Convert.ToUInt16(operand),
			SpecialType.System_UInt32 => Convert.ToUInt32(operand),
			SpecialType.System_UInt64 => Convert.ToUInt64(operand),
			SpecialType.System_Object => operand,
			SpecialType.System_Collections_IEnumerable => (IEnumerable) operand,
			_ => operand,
		};
	}

	public override object? VisitInvocation(IInvocationOperation operation, Dictionary<string, object?> argument)
	{
		var targetMethod = operation.TargetMethod;
		var instance = Visit(operation.Instance, argument);

		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		return SyntaxHelpers.ExecuteMethod(targetMethod, instance, arguments);
	}

	public override object? VisitSwitch(ISwitchOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);

		foreach (var caseClause in operation.Cases)
		{
			if (caseClause.Clauses
			    .Where(w => w.CaseKind != CaseKind.Default)
			    .Select(s => Visit(s, argument))
			    .Contains(value))
			{
				VisitList(caseClause.Body, argument);

				return null;
			}
		}

		foreach (var caseClause in operation.Cases)
		{
			if (caseClause.Clauses
			    .Where(w => w.CaseKind == CaseKind.Default)
			    .Select(s => Visit(s, argument))
			    .Contains(value))
			{
				VisitList(caseClause.Body, argument);
			}
		}

		return null;
	}
	
	public override object? VisitVariableDeclaration(IVariableDeclarationOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarators)
		{
			VisitVariableDeclarator(variable, argument);
		}

		return null;
	}
	
	public override object? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarations)
		{
			VisitVariableDeclaration(variable, argument);
		}

		return null;
	}

	public override object? VisitVariableDeclarator(IVariableDeclaratorOperation operation, Dictionary<string, object?> argument)
	{
		argument[operation.Symbol.Name] = Visit(operation.Initializer?.Value, argument);

		return null;
	}
	
	public override object? VisitReturn(IReturnOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.ReturnedValue, argument);
	}
	
	public override object? VisitWhileLoop(IWhileLoopOperation operation, Dictionary<string, object?> argument)
	{
		while (Visit(operation.Condition, argument) is true)
		{
			Visit(operation.Body, argument);
		}

		return null;
	}
	
	public override object? VisitForLoop(IForLoopOperation operation, Dictionary<string, object?> argument)
	{
		for (VisitList(operation.Before, argument); Visit(operation.Condition, argument) is true; VisitList(operation.AtLoopBottom, argument))
		{
			Visit(operation.Body, argument);
		}

		return null;
	}
	
	public override object? VisitForEachLoop(IForEachLoopOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var item in Visit(operation.Collection, argument) as IEnumerable)
		{
			argument[GetVariableName(operation.LoopControlVariable)] = item;
			Visit(operation.Body, argument);
		}

		return null;
	}

	public override object? VisitInterpolation(IInterpolationOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.Expression, argument);

		if (value is IFormattable formattable)
		{
			return formattable.ToString(Visit(operation.FormatString, argument) as string, CultureInfo.InvariantCulture);
		}

		return value.ToString();
	}

	public override object? VisitInterpolatedStringText(IInterpolatedStringTextOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Text;
	}

	public override object? VisitInterpolatedString(IInterpolatedStringOperation operation, Dictionary<string, object?> argument)
	{
		return String.Concat(operation.Parts
			.Select(s => Visit(s, argument)));
	}

	public override object? VisitSizeOf(ISizeOfOperation operation, Dictionary<string, object?> argument)
	{
		var type = operation.Type;
		
		return type?.SpecialType switch
		{
			SpecialType.System_Boolean => sizeof(bool),
			SpecialType.System_Byte => sizeof(byte),
			SpecialType.System_Char => sizeof(char),
			SpecialType.System_Decimal => sizeof(decimal),
			SpecialType.System_Double => sizeof(double),
			SpecialType.System_Int16 => sizeof(short),
			SpecialType.System_Int32 => sizeof(int),
			SpecialType.System_Int64 => sizeof(long),
			SpecialType.System_SByte => sizeof(sbyte),
			SpecialType.System_Single => sizeof(float),
			SpecialType.System_UInt16 => sizeof(ushort),
			SpecialType.System_UInt32 => sizeof(uint),
			SpecialType.System_UInt64 => sizeof(ulong),
			_ => 0,
		};
	}
	
	public override object? VisitTypeOf(ITypeOfOperation operation, Dictionary<string, object?> argument)
	{
		return Type.GetType(operation.Type.ToDisplayString());
	}

	public override object? VisitArrayInitializer(IArrayInitializerOperation operation, Dictionary<string, object?> argument)
	{
		return operation.ElementValues
			.Select(s => Visit(s, argument))
			.ToArray();
	}

	public override object? VisitUnaryOperator(IUnaryOperation operation, Dictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);

		return operation.OperatorKind switch
		{
			UnaryOperatorKind.UnaryPlus => operand,
			UnaryOperatorKind.UnaryMinus => Subtract(0, operand),
			UnaryOperatorKind.BitwiseNegation => SyntaxHelpers.BitwiseNot(operand),
			UnaryOperatorKind.Not => SyntaxHelpers.LogicalNot(operand),
			_ => operand,
		};
	}

	public override object? VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var incrementValue = operation.IsDecrement ? -1 : 1;

		return Add(target, incrementValue);
	}

	public override object? VisitParenthesized(IParenthesizedOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument);
	}

	public override object? VisitObjectCreation(IObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		return Activator.CreateInstance(operation.Type.ToDisplayString(), arguments);
	}

	public override object? VisitAnonymousObjectCreation(IAnonymousObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Initializers
			.Select(s => Visit(s, argument))
			.ToArray();

		return Activator.CreateInstance(operation.Type.ToDisplayString(), arguments);
	}

	public override object? VisitTypeParameterObjectCreation(ITypeParameterObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		return Activator.CreateInstance(operation.Type.ToDisplayString(), arguments);
	}

	public override object? VisitInstanceReference(IInstanceReferenceOperation operation, Dictionary<string, object?> argument)
	{
		return argument["this"];
	}

	private string GetVariableName(IOperation operation)
	{
		return operation switch
		{
			ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local.Name,
			IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter.Name,
			IPropertyReferenceOperation propertyReferenceOperation => propertyReferenceOperation.Property.Name,
			IFieldReferenceOperation fieldReferenceOperation => fieldReferenceOperation.Field.Name,
			IVariableDeclaratorOperation variableDeclaratorOperation => variableDeclaratorOperation.Symbol.Name,
			_ => String.Empty,
		};
	}
	
	private void VisitList(ImmutableArray<IOperation> operations, Dictionary<string, object?> argument)
	{
		foreach (var operation in operations)
		{
			Visit(operation, argument);
		}
	}
}
