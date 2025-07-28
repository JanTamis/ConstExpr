using Microsoft.CodeAnalysis;
using System;

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
}