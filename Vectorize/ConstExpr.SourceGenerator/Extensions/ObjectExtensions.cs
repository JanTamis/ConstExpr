using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Extensions;

public static class ObjectExtensions
{
	public static object? ToSpecialType<T>(this T value, SpecialType specialType)
	{
		return specialType switch
		{
			SpecialType.System_Byte => Convert.ToByte(value),
			SpecialType.System_SByte => Convert.ToSByte(value),
			SpecialType.System_Int16 => Convert.ToInt16(value),
			SpecialType.System_UInt16 => Convert.ToUInt16(value),
			SpecialType.System_Int32 => Convert.ToInt32(value),
			SpecialType.System_UInt32 => Convert.ToUInt32(value),
			SpecialType.System_Int64 => Convert.ToInt64(value),
			SpecialType.System_UInt64 => Convert.ToUInt64(value),
			SpecialType.System_Single => Convert.ToSingle(value),
			SpecialType.System_Double => Convert.ToDouble(value),
			SpecialType.System_Decimal => Convert.ToDecimal(value),
			_ => null,
		};
	}

	public static T Add<T>(this T left, T right)
	{
		return (T)(object)(left switch
		{
			byte leftByte when right is byte rightByte => leftByte + rightByte,
			short leftShort when right is short rightShort => leftShort + rightShort,
			int leftInt when right is int rightInt => leftInt + rightInt,
			long leftLong when right is long rightLong => leftLong + rightLong,
			float leftFloat when right is float rightFloat => leftFloat + rightFloat,
			double leftDouble when right is double rightDouble => leftDouble + rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal + rightDecimal,
			string leftString when right is string rightString => leftString + rightString,
			_ => null
		});
	}

	public static T? Subtract<T>(this T left, T right)
	{
		return (T?)(object)(left switch
		{
			byte leftByte when right is byte rightByte => leftByte - rightByte,
			short leftShort when right is short rightShort => leftShort - rightShort,
			int leftInt when right is int rightInt => leftInt - rightInt,
			long leftLong when right is long rightLong => leftLong - rightLong,
			float leftFloat when right is float rightFloat => leftFloat - rightFloat,
			double leftDouble when right is double rightDouble => leftDouble - rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal - rightDecimal,
			_ => null
		});
	}

	public static object? Multiply(this object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte * rightByte,
			short leftShort when right is short rightShort => leftShort * rightShort,
			int leftInt when right is int rightInt => leftInt * rightInt,
			long leftLong when right is long rightLong => leftLong * rightLong,
			float leftFloat when right is float rightFloat => leftFloat * rightFloat,
			double leftDouble when right is double rightDouble => leftDouble * rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal * rightDecimal,
			_ => null
		};
	}

	public static object? Divide(this object? left, object? right)
	{
		return left switch
		{
			byte leftByte when right is byte rightByte => leftByte / rightByte,
			short leftShort when right is short rightShort => leftShort / rightShort,
			int leftInt when right is int rightInt => leftInt / rightInt,
			long leftLong when right is long rightLong => leftLong / rightLong,
			float leftFloat when right is float rightFloat => leftFloat / rightFloat,
			double leftDouble when right is double rightDouble => leftDouble / rightDouble,
			decimal leftDecimal when right is decimal rightDecimal => leftDecimal / rightDecimal,
			_ => null
		};
	}

	public static object? Modulo(this object? left, object? right)
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

	public static object? LeftShift(this object? left, object? right)
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

	public static object? RightShift(this object? left, object? right)
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

	public static object? UnsignedRightShift(object? left, object? right)
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

	public static object? And(this object? left, object? right)
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

	public static object? Or(this object? left, object? right)
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

	public static object? ExclusiveOr(this object? left, object? right)
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

	public static object? ConditionalAnd(this object? left, object? right)
	{
		return left switch
		{
			bool leftBool when right is bool rightBool => leftBool && rightBool,
			_ => null
		};
	}

	public static object? ConditionalOr(this object? left, object? right)
	{
		return left switch
		{
			bool leftBool when right is bool rightBool => leftBool || rightBool,
			_ => null
		};
	}

	public static object? BitwiseNot(this object? value)
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

	public static object? LogicalNot(this object? value)
	{
		return value switch
		{
			bool b => !b,
			_ => false,
		};
	}

	public static object? ExecuteBinaryOperation(BinaryOperatorKind operatorKind, object? left, object? right)
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

	public static object? ExecuteBinaryOperation(SyntaxKind operatorKind, object? left, object? right)
	{
		return operatorKind switch
		{
			SyntaxKind.AddExpression or SyntaxKind.PlusEqualsToken => Add(left, right),
			SyntaxKind.SubtractExpression or SyntaxKind.MinusEqualsToken => Subtract(left, right),
			SyntaxKind.MultiplyExpression or SyntaxKind.AsteriskEqualsToken => Multiply(left, right),
			SyntaxKind.DivideExpression or SyntaxKind.SlashEqualsToken => Divide(left, right),
			SyntaxKind.ModuloExpression or SyntaxKind.PercentEqualsToken => Modulo(left, right),
			SyntaxKind.LeftShiftExpression or SyntaxKind.LessThanLessThanEqualsToken => LeftShift(left, right),
			SyntaxKind.RightShiftExpression or SyntaxKind.GreaterThanGreaterThanEqualsToken => RightShift(left, right),
			SyntaxKind.UnsignedRightShiftExpression or SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken => UnsignedRightShift(left, right),
			SyntaxKind.BitwiseAndExpression or SyntaxKind.AmpersandEqualsToken => And(left, right),
			SyntaxKind.BitwiseOrExpression or SyntaxKind.BarEqualsToken => Or(left, right),
			SyntaxKind.ExclusiveOrExpression or SyntaxKind.CaretEqualsToken => ExclusiveOr(left, right),
			SyntaxKind.LogicalAndExpression => ConditionalAnd(left, right),
			SyntaxKind.LogicalOrExpression => ConditionalOr(left, right),
			SyntaxKind.EqualsExpression => EqualityComparer<object?>.Default.Equals(left, right),
			SyntaxKind.NotEqualsExpression => !EqualityComparer<object?>.Default.Equals(left, right),
			SyntaxKind.LessThanExpression => Comparer<object?>.Default.Compare(left, right) < 0,
			SyntaxKind.LessThanOrEqualExpression => Comparer<object?>.Default.Compare(left, right) <= 0,
			SyntaxKind.GreaterThanExpression => Comparer<object?>.Default.Compare(left, right) > 0,
			SyntaxKind.GreaterThanOrEqualExpression => Comparer<object?>.Default.Compare(left, right) >= 0,
			_ => null,
		};
	}

	public static object? Abs(this object? value, SpecialType specialType)
	{
		var zero = 0.ToSpecialType(specialType);
		return ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, zero) is true
				? ExecuteBinaryOperation(BinaryOperatorKind.Subtract, zero, value)
				: value;
	}

	public static bool IsNumericZero(this object? value) => value switch
	{
		byte b => b == 0,
		sbyte sb => sb == 0,
		short s => s == 0,
		ushort us => us == 0,
		int i => i == 0,
		uint ui => ui == 0,
		long l => l == 0,
		ulong ul => ul == 0,
		float f => f == 0f,
		double d => d == 0d,
		decimal m => m == 0m,
		_ => false
	};

	public static bool IsNumericOne(this object? value) => value switch
	{
		byte b => b == 1,
		sbyte sb => sb == 1,
		short s => s == 1,
		ushort us => us == 1,
		int i => i == 1,
		uint ui => ui == 1,
		long l => l == 1,
		ulong ul => ul == 1,
		float f => f == 1f,
		double d => d == 1d,
		decimal m => m == 1m,
		_ => false
	};

	public static bool IsNumericTwo(this object? value) => value switch
	{
		byte b => b == 2,
		sbyte sb => sb == 2,
		short s => s == 2,
		ushort us => us == 2,
		int i => i == 2,
		uint ui => ui == 2,
		long l => l == 2,
		ulong ul => ul == 2,
		float f => f == 2f,
		double d => d == 2d,
		decimal m => m == 2m,
		_ => false
	};
}