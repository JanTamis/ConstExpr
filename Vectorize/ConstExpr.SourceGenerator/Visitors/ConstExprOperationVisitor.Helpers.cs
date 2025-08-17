using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ConstExpr.SourceGenerator.Visitors;

public partial class ConstExprOperationVisitor
{
	private object? ExecuteBinaryOperation(BinaryOperatorKind operatorKind, object? left, object? right)
	{
		return operatorKind switch
		{
			BinaryOperatorKind.Add => Add(left, right),
			BinaryOperatorKind.Subtract => Subtract(left, right),
			BinaryOperatorKind.Multiply => Multiply(left, right),
			BinaryOperatorKind.Divide => Divide(left, right),
			BinaryOperatorKind.Remainder => Modulo(left, right),
			BinaryOperatorKind.LeftShift => LeftShift(left, right),
			BinaryOperatorKind.RightShift => RightShift(left, right),
			BinaryOperatorKind.UnsignedRightShift => UnsignedRightShift(left, right),
			BinaryOperatorKind.And => And(left, right),
			BinaryOperatorKind.Or => Or(left, right),
			BinaryOperatorKind.ExclusiveOr => ExclusiveOr(left, right),
			BinaryOperatorKind.ConditionalAnd => ConditionalAnd(left, right),
			BinaryOperatorKind.ConditionalOr => ConditionalOr(left, right),
			BinaryOperatorKind.Equals => EqualityComparer<object?>.Default.Equals(left, right),
			BinaryOperatorKind.NotEquals => !EqualityComparer<object?>.Default.Equals(left, right),
			BinaryOperatorKind.LessThan => Comparer<object?>.Default.Compare(left, right) < 0,
			BinaryOperatorKind.LessThanOrEqual => Comparer<object?>.Default.Compare(left, right) <= 0,
			BinaryOperatorKind.GreaterThan => Comparer<object?>.Default.Compare(left, right) > 0,
			BinaryOperatorKind.GreaterThanOrEqual => Comparer<object?>.Default.Compare(left, right) >= 0,
			_ => null,
		};
	}

	private string? GetVariableName(IOperation operation)
	{
		return operation switch
		{
			ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local.Name,
			IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter.Name,
			// IPropertyReferenceOperation propertyReferenceOperation => propertyReferenceOperation.Property.Name,
			// IFieldReferenceOperation fieldReferenceOperation => fieldReferenceOperation.Field.Name,
			IVariableDeclaratorOperation variableDeclaratorOperation => variableDeclaratorOperation.Symbol.Name,
			_ => null,
		};
	}

	private void VisitList(ImmutableArray<IOperation> operations, IDictionary<string, object?> argument)
	{
		foreach (var operation in operations)
		{
			Visit(operation, argument);
		}
	}

	public static object? Add(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte + rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte + rightSByte,
			short leftShort when right is short rightShort => leftShort + rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort + rightUShort,
			int leftInt when right is int rightInt => leftInt + rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt + rightUInt,
			long leftLong when right is long rightLong => leftLong + rightLong,
			ulong leftULong when right is ulong rightULong => leftULong + rightULong,
			float leftFloat when right is float rightFloat => leftFloat + rightFloat,
			double leftDouble when right is double rightDouble => leftDouble + rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal + rightDecimal,
			string leftString when right is string rightString => leftString + rightString,
			_ => null
		};
	}

	public static object? Subtract(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte - rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte - rightSByte,
			short leftShort when right is short rightShort => leftShort - rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort - rightUShort,
			int leftInt when right is int rightInt => leftInt - rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt - rightUInt,
			long leftLong when right is long rightLong => leftLong - rightLong,
			ulong leftULong when right is ulong rightULong => leftULong - rightULong,
			float leftFloat when right is float rightFloat => leftFloat - rightFloat,
			double leftDouble when right is double rightDouble => leftDouble - rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal - rightDecimal,
			_ => null
		};
	}

	private object? Multiply(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte * rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte * rightSByte,
			short leftShort when right is short rightShort => leftShort * rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort * rightUShort,
			int leftInt when right is int rightInt => leftInt * rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt * rightUInt,
			long leftLong when right is long rightLong => leftLong * rightLong,
			ulong leftULong when right is ulong rightULong => leftULong * rightULong,
			float leftFloat when right is float rightFloat => leftFloat * rightFloat,
			double leftDouble when right is double rightDouble => leftDouble * rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal * rightDecimal,
			_ => null
		};
	}

	private object? Divide(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte / rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte / rightSByte,
			short leftShort when right is short rightShort => leftShort / rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort / rightUShort,
			int leftInt when right is int rightInt => leftInt / rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt / rightUInt,
			long leftLong when right is long rightLong => leftLong / rightLong,
			ulong leftULong when right is ulong rightULong => leftULong / rightULong,
			float leftFloat when right is float rightFloat => leftFloat / rightFloat,
			double leftDouble when right is double rightDouble => leftDouble / rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal / rightDecimal,
			_ => null
		};
	}

	private object? Modulo(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte % rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte % rightSByte,
			short leftShort when right is short rightShort => leftShort % rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort % rightUShort,
			int leftInt when right is int rightInt => leftInt % rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt % rightUInt,
			long leftLong when right is long rightLong => leftLong % rightLong,
			ulong leftULong when right is ulong rightULong => leftULong % rightULong,
			float leftFloat when right is float rightFloat => leftFloat % rightFloat,
			double leftDouble when right is double rightDouble => leftDouble % rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal % rightDecimal,
			_ => null
		};
	}

	private object? LeftShift(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte << rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte << rightSByte,
			short leftShort when right is short rightShort => leftShort << rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort << rightUShort,
			int leftInt when right is int rightInt => leftInt << rightInt,
			uint leftUInt when right is int rightUInt => leftUInt << rightUInt,
			long leftLong when right is int rightLong => leftLong << rightLong,
			ulong leftULong when right is int rightULong => leftULong << rightULong,
			_ => null
		};
	}

	private object? RightShift(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte >> rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte >> rightSByte,
			short leftShort when right is short rightShort => leftShort >> rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort >> rightUShort,
			int leftInt when right is int rightInt => leftInt >> rightInt,
			uint leftUInt when right is int rightUInt => leftUInt >> rightUInt,
			long leftLong when right is int rightLong => leftLong >> rightLong,
			ulong leftULong when right is int rightULong => leftULong >> rightULong,
			_ => null
		};
	}

	private object? UnsignedRightShift(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte >>> rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte >>> rightSByte,
			short leftShort when right is short rightShort => leftShort >>> rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort >>> rightUShort,
			int leftInt when right is int rightInt => leftInt >>> rightInt,
			long leftLong when right is int rightLong => leftLong >>> rightLong,
			ulong leftULong when right is int rightULong => leftULong >>> rightULong,
			_ => null
		};
	}

	private object? And(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte & rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte & rightSByte,
			short leftShort when right is short rightShort => leftShort & rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort & rightUShort,
			int leftInt when right is int rightInt => leftInt & rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt & rightUInt,
			long leftLong when right is long rightLong => leftLong & rightLong,
			ulong leftULong when right is ulong rightULong => leftULong & rightULong,
			_ => null
		};
	}

	private object? Or(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte | rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte | rightSByte,
			short leftShort when right is short rightShort => leftShort | rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort | rightUShort,
			int leftInt when right is int rightInt => leftInt | rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt | rightUInt,
			long leftLong when right is long rightLong => leftLong | rightLong,
			ulong leftULong when right is ulong rightULong => leftULong | rightULong,
			_ => null
		};
	}

	private object? ExclusiveOr(object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte ^ rightByte,
			sbyte leftSByte when right is sbyte rightSByte => leftSByte ^ rightSByte,
			short leftShort when right is short rightShort => leftShort ^ rightShort,
			ushort leftUShort when right is ushort rightUShort => leftUShort ^ rightUShort,
			int leftInt when right is int rightInt => leftInt ^ rightInt,
			uint leftUInt when right is uint rightUInt => leftUInt ^ rightUInt,
			long leftLong when right is long rightLong => leftLong ^ rightLong,
			ulong leftULong when right is ulong rightULong => leftULong ^ rightULong,
			_ => null
		};
	}

	private object? ConditionalAnd(object? left, object? right)
	{
		return left switch
		{
			bool leftBool when right is bool rightBool => leftBool && rightBool,
			_ => null
		};
	}

	private object? ConditionalOr(object? left, object? right)
	{
		return left switch
		{
			bool leftBool when right is bool rightBool => leftBool || rightBool,
			_ => null
		};
	}

	private object? BitwiseNot(object? value)
	{
		return value switch
		{
			byte b => ~b,
			sbyte sb => ~sb,
			short s => ~s,
			ushort us => ~us,
			int i => ~i,
			uint ui => ~ui,
			long l => ~l,
			ulong ul => ~ul,
			_ => null
		};
	}

	private object? LogicalNot(object? value)
	{
		return value switch
		{
			bool b => !b,
			_ => false,
		};
	}
}