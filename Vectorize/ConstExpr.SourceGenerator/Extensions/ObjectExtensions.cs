using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

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
			SpecialType.System_Char => Convert.ToChar(value),
			SpecialType.System_String => Convert.ToString(value),
			_ => null,
		};
	}

	public static T Add<T>(this T left, T right)
	{
		return (T)ExecuteArithmeticOperation(left, right, Expression.Add)!;
	}

	public static T? Subtract<T>(this T left, T right)
	{
		return (T?)ExecuteArithmeticOperation(left, right, Expression.Subtract);
	}

	public static object? Multiply(this object? left, object? right)
	{
		return ExecuteArithmeticOperation(left, right, Expression.Multiply);
	}

	public static object? Divide(this object? left, object? right)
	{
		return ExecuteArithmeticOperation(left, right, Expression.Divide);
	}

	public static object? Modulo(this object? left, object? right)
	{
		return ExecuteArithmeticOperation(left, right, Expression.Modulo);
	}

	private static object? ExecuteArithmeticOperation(
		object? left,
		object? right,
		Func<Expression, Expression, BinaryExpression> operation)
	{
		if (left is null || right is null)
			return null;

		var lType = left.GetType();
		var rType = right.GetType();

		try
		{
			// String concatenation special case
			if (lType == typeof(string) || rType == typeof(string))
			{
				if (operation == (Func<Expression, Expression, BinaryExpression>)Expression.Add)
					return left?.ToString() + right?.ToString();
				return null;
			}

			// Check if both are numeric types
			if (!IsNumericType(lType) || !IsNumericType(rType))
				return null;

			// Find common type according to C# numeric promotion rules
			var common = GetCommonArithmeticType(lType, rType);
			if (common is null)
				return null;

			var lc = Expression.Convert(Expression.Constant(left), common);
			var rc = Expression.Convert(Expression.Constant(right), common);
			var expr = operation(lc, rc);

			// Box result
			var boxed = Expression.Convert(expr, typeof(object));
			var lambda = Expression.Lambda<Func<object>>(boxed);
			return lambda.Compile().Invoke();
		}
		catch
		{
			return null;
		}
	}

	private static bool IsNumericType(Type t)
	{
		return Type.GetTypeCode(t) switch
		{
			TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or
			TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
			TypeCode.Single or TypeCode.Double or TypeCode.Decimal or TypeCode.Char => true,
			_ => false
		};
	}

	private static Type? GetCommonArithmeticType(Type lt, Type rt)
	{
		var tl = Type.GetTypeCode(lt);
		var tr = Type.GetTypeCode(rt);

		// Decimal: if either is decimal, result is decimal (but not with float/double)
		if (tl == TypeCode.Decimal || tr == TypeCode.Decimal)
		{
			if (tl == TypeCode.Single || tl == TypeCode.Double || tr == TypeCode.Single || tr == TypeCode.Double)
				return null;
			return typeof(decimal);
		}

		// Double: if either is double, result is double
		if (tl == TypeCode.Double || tr == TypeCode.Double)
			return typeof(double);

		// Float: if either is float, result is float
		if (tl == TypeCode.Single || tr == TypeCode.Single)
			return typeof(float);

		// Integral promotions
		// Disallow long|ulong combination
		if ((tl == TypeCode.UInt64 && tr == TypeCode.Int64) || (tl == TypeCode.Int64 && tr == TypeCode.UInt64))
			return null;

		if (tl == TypeCode.UInt64 || tr == TypeCode.UInt64)
			return typeof(ulong);
		if (tl == TypeCode.Int64 || tr == TypeCode.Int64)
			return typeof(long);
		if (tl == TypeCode.UInt32 || tr == TypeCode.UInt32)
			return typeof(uint);
		return typeof(int);
	}

	public static object? LeftShift(this object? left, object? right)
	{
		return ExecuteShiftOperation(left, right, Expression.LeftShift);
	}

	public static object? RightShift(this object? left, object? right)
	{
		return ExecuteShiftOperation(left, right, Expression.RightShift);
	}

	public static object? UnsignedRightShift(object? left, object? right)
	{
		// Expression trees don't support >>> directly, so we handle it manually
		if (left is null || right is null)
			return null;

		try
		{
			// Convert shift amount to int using expression tree
			var shift = ConvertToInt32(right);
			if (shift is null)
				return null;

			return left switch
			{
				byte b => b >>> shift.Value,
				sbyte sb => sb >>> shift.Value,
				short s => s >>> shift.Value,
				ushort us => us >>> shift.Value,
				int i => i >>> shift.Value,
				uint ui => ui >>> shift.Value,
				long l => l >>> shift.Value,
				ulong ul => ul >>> shift.Value,
				char c => c >>> shift.Value,
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	private static int? ConvertToInt32(object? value)
	{
		if (value is null)
			return null;

		var type = value.GetType();
		if (!IsNumericType(type) && !IsIntegralType(type))
			return null;

		try
		{
			var constant = Expression.Constant(value);
			var converted = Expression.Convert(constant, typeof(int));
			var boxed = Expression.Convert(converted, typeof(object));
			var lambda = Expression.Lambda<Func<object>>(boxed);
			return (int)lambda.Compile().Invoke();
		}
		catch
		{
			return null;
		}
	}

	private static object? ExecuteShiftOperation(
		object? left,
		object? right,
		Func<Expression, Expression, BinaryExpression> operation)
	{
		if (left is null || right is null)
			return null;

		var lType = left.GetType();

		try
		{
			if (!IsIntegralType(lType))
				return null;

			// Convert shift amount to int using expression tree
			var shiftAmount = ConvertToInt32(right);
			if (shiftAmount is null)
				return null;

			// C# promotes smaller types to int for shift operations
			var promoted = GetPromotedType(lType);

			var lc = Expression.Convert(Expression.Constant(left), promoted);
			var rc = Expression.Constant(shiftAmount.Value);
			var expr = operation(lc, rc);

			// Box result
			var boxed = Expression.Convert(expr, typeof(object));
			var lambda = Expression.Lambda<Func<object>>(boxed);
			return lambda.Compile().Invoke();
		}
		catch
		{
			return null;
		}
	}

	public static object? And(this object? left, object? right)
	{
		return ExecuteBitwiseOperation(left, right, Expression.And);
	}

	public static object? Or(this object? left, object? right)
	{
		return ExecuteBitwiseOperation(left, right, Expression.Or);
	}

	public static object? ExclusiveOr(this object? left, object? right)
	{
		return ExecuteBitwiseOperation(left, right, Expression.ExclusiveOr);
	}

	private static object? ExecuteBitwiseOperation(
		object? left,
		object? right,
		Func<Expression, Expression, BinaryExpression> operation)
	{
		if (left is null || right is null)
			return null;

		var lType = left.GetType();
		var rType = right.GetType();

		try
		{
			// Same enum type: perform bitwise operation on underlying type and cast back
			if (lType.IsEnum && rType.IsEnum)
			{
				if (lType != rType)
					return null;

				var underlying = Enum.GetUnderlyingType(lType);
				var lConst = Expression.Constant(left);
				var rConst = Expression.Constant(right);
				var lToUnder = Expression.Convert(lConst, underlying);
				var rToUnder = Expression.Convert(rConst, underlying);

				// Promote to int if smaller than int (C# rules)
				var promoted = GetPromotedType(underlying);
				var lProm = Expression.Convert(lToUnder, promoted);
				var rProm = Expression.Convert(rToUnder, promoted);

				var opExpr = operation(lProm, rProm);
				var backToUnder = Expression.Convert(opExpr, underlying);
				var backToEnum = Expression.Convert(backToUnder, lType);
				var boxed = Expression.Convert(backToEnum, typeof(object));
				var lambda = Expression.Lambda<Func<object>>(boxed);
				return lambda.Compile().Invoke();
			}

			// Check if both are integral types
			if (!IsIntegralType(lType) || !IsIntegralType(rType))
				return null;

			// Find common type according to C# numeric promotion rules
			var common = GetCommonBitwiseType(lType, rType);
			if (common is null)
				return null;

			var lc = Expression.Convert(Expression.Constant(left), common);
			var rc = Expression.Convert(Expression.Constant(right), common);
			var be = operation(lc, rc);

			// Box result
			var boxed2 = Expression.Convert(be, typeof(object));
			var lambda2 = Expression.Lambda<Func<object>>(boxed2);
			return lambda2.Compile().Invoke();
		}
		catch
		{
			return null;
		}
	}

	private static bool IsIntegralType(Type t)
	{
		if (t.IsEnum) return false;
		return Type.GetTypeCode(t) switch
		{
			TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or
			TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
			TypeCode.Char => true,
			_ => false
		};
	}

	private static Type GetPromotedType(Type t)
	{
		// C# promotes smaller types to int for bitwise operations
		return Type.GetTypeCode(t) switch
		{
			TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char => typeof(int),
			TypeCode.Int32 => typeof(int),
			TypeCode.UInt32 => typeof(uint),
			TypeCode.Int64 => typeof(long),
			TypeCode.UInt64 => typeof(ulong),
			_ => typeof(int)
		};
	}

	private static Type? GetCommonBitwiseType(Type lt, Type rt)
	{
		if (lt.IsEnum || rt.IsEnum)
			return null;

		if (!IsIntegralType(lt) || !IsIntegralType(rt))
			return null;

		var tl = Type.GetTypeCode(lt);
		var tr = Type.GetTypeCode(rt);

		// Disallow long|ulong combination (no implicit common type in C#)
		if ((tl == TypeCode.UInt64 && tr == TypeCode.Int64) || (tl == TypeCode.Int64 && tr == TypeCode.UInt64))
			return null;

		// C# binary numeric promotions:
		if (tl == TypeCode.UInt64 || tr == TypeCode.UInt64)
			return typeof(ulong);
		if (tl == TypeCode.Int64 || tr == TypeCode.Int64)
			return typeof(long);
		if (tl == TypeCode.UInt32 || tr == TypeCode.UInt32)
			return typeof(uint);
		return typeof(int);
	}

	public static object? ConditionalAnd(this object? left, object? right)
	{
		if (left is bool lb && right is bool rb)
			return lb && rb;
		return null;
	}

	public static object? ConditionalOr(this object? left, object? right)
	{
		if (left is bool lb && right is bool rb)
			return lb || rb;
		return null;
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
		float f => f is 0f or -0f,
		double d => d is 0d or -0d,
		decimal m => m is 0m or -0m,
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
		float f => Math.Abs(f - 1f) < Single.Epsilon,
		double d => Math.Abs(d - 1d) < Double.Epsilon,
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
		float f => Math.Abs(f - 2f) < Single.Epsilon,
		double d => Math.Abs(d - 2d) < Double.Epsilon,
		decimal m => m == 2m,
		_ => false
	};

	public static bool IsNumericNegativeOne(this object? value) => value switch
	{
		sbyte sb => sb == -1,
		short s => s == -1,
		int i => i == -1,
		long l => l == -1,
		float f => Math.Abs(f - -1f) < Single.Epsilon,
		double d => Math.Abs(d - -1d) < Double.Epsilon,
		decimal m => m == -1m,
		_ => false
	};

	public static bool IsNumericValue(this object? value, int target) => value switch
	{
		byte b => b == target,
		sbyte sb => sb == target,
		short s => s == target,
		ushort us => us == target,
		int i => i == target,
		uint ui => ui == target,
		long l => l == target,
		ulong ul => ul == (ulong)target,
		float f => Math.Abs(f - target) < Single.Epsilon,
		double d => Math.Abs(d - target) < Double.Epsilon,
		decimal m => m == target,
		_ => false
	};

	public static bool IsPositive(this object? value) => value switch
	{
		byte => true,
		sbyte sb => sb >= 0,
		short s => s >= 0,
		ushort => true,
		int i => i >= 0,
		uint => true,
		long l => l >= 0,
		ulong => true,
		float f => !float.IsNaN(f) && f > 0f,
		double d => !double.IsNaN(d) && d > 0d,
		decimal m => m > 0m,
		char => true,
		_ => false,
	};

	public static bool IsNegative(this object? value) => value switch
	{
		sbyte sb => sb < 0,
		short s => s < 0,
		int i => i < 0,
		long l => l < 0,
		float f => !float.IsNaN(f) && f < 0f,
		double d => !double.IsNaN(d) && d < 0d,
		decimal m => m < 0m,
		char => false,
		_ => false,
	};

	public static int GetBitSize(this object? value) => value switch
	{
		byte or sbyte => 8,
		short or ushort or char => 16,
		int or uint => 32,
		long or ulong => 64,
		float => 32,
		double => 64,
		decimal => 128,
		_ => 0,
	};

	public static bool IsNumericPowerOfTwo(this object? value, out int power)
	{
		power = 0;

		static int Log2(ulong x)
		{
			var p = 0;
			while (x > 1)
			{
				x >>= 1;
				p++;
			}
			return p;
		}

		static bool IsDecimalIntegerPowerOfTwo(decimal m, out int p)
		{
			p = 0;
			if (m <= 0m || decimal.Truncate(m) != m)
				return false;

			while (m % 2m == 0m)
			{
				m /= 2m;
				p++;
			}
			return m == 1m;
		}

		return value switch
		{
			byte b when b != 0 && (b & (b - 1)) == 0 => (power = Log2(b)) >= 0,
			sbyte sb and > 0 when (sb & (sb - 1)) == 0 => (power = Log2((byte)sb)) >= 0,
			short s and > 0 when (s & (s - 1)) == 0 => (power = Log2((ushort)s)) >= 0,
			ushort us when us != 0 && (us & (us - 1)) == 0 => (power = Log2(us)) >= 0,
			int i and > 0 when (i & (i - 1)) == 0 => (power = Log2((uint)i)) >= 0,
			uint ui when ui != 0 && (ui & (ui - 1)) == 0 => (power = Log2(ui)) >= 0,
			long l and > 0 when (l & (l - 1)) == 0 => (power = Log2((ulong)l)) >= 0,
			ulong ul when ul != 0 && (ul & (ul - 1)) == 0 => (power = Log2(ul)) >= 0,

			// Floating-point: alleen positieve, gehele waarden binnen ulong-bereik
			float f when !float.IsNaN(f) && !float.IsInfinity(f) && f > 0f && f == MathF.Truncate(f) && f <= ulong.MaxValue &&
									 (((ulong)f & ((ulong)f - 1)) == 0) => (power = Log2((ulong)f)) >= 0,
			double d when !double.IsNaN(d) && !double.IsInfinity(d) && d > 0d && d == Math.Truncate(d) && d <= ulong.MaxValue &&
										(((ulong)d & ((ulong)d - 1)) == 0) => (power = Log2((ulong)d)) >= 0,

			// Decimal: positieve, gehele waarden (geen fracties)
			decimal m when IsDecimalIntegerPowerOfTwo(m, out var p) => (power = p) >= 0,

			_ => false
		};
	}

	public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
	{
		if (!dictionary.ContainsKey(key))
		{
			dictionary.Add(key, value);
			return true;
		}

		return false;
	}

	public static IDictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
	{
		return new Dictionary<TKey, TValue>(dictionary);
	}

	public static bool TryPop<T>(this Stack<T> stack, out T? value)
	{
		if (stack.Count > 0)
		{
			value = stack.Pop();
			return true;
		}

		value = default;
		return false;
	}

	public static IEnumerable<(object start, object step, object end, IList<object?> values)> GetClusters(this IList<object?> items)
	{
		var i = 0;
		var result = new List<object>(items.Count);

		var previous = items[0];

		while (i < items.Count)
		{
			var start = items[i];
			result.Add(start);
			
			var j = i + 1;
			object step = null!;

			if (j < items.Count)
			{
				step = items[j].Subtract(items[i]);
			}

			while (j < items.Count && items[j].Subtract(previous).Subtract(step).IsNumericZero())
			{
				result.Add(items[j]);
				previous = items[j];
				j++;
			}

			var minValue = items[i];
			var maxValue = items[j - 1];

			// if ((i != 0 && j - 1 != items.Count - 1) && Convert.ToInt32(maxValue) <= maxValue.GetBitSize())
			// {
			// 	// maxValue = maxValue.Subtract(minValue);
			// 	// minValue = minValue.Subtract(minValue);
			//
			// 	// for (var k = 0; k < items.Count; k++)
			// 	// {
			// 	// 	items[k] = items[k].Subtract(minValue);
			// 	// }
			//
			// 	yield return (minValue, -1, maxValue, items);
			// 	yield break;
			// }

			yield return (start, step, items[j - 1], result);
			
			result.Clear();
			i = j;
		}
	}
	
	public static bool IsEvenNumber(this object? value)
	{
		return value switch
		{
			byte b => (b & 1) == 0,
			sbyte sb => (sb & 1) == 0,
			short s => (s & 1) == 0,
			ushort us => (us & 1) == 0,
			int i => (i & 1) == 0,
			uint ui => (ui & 1) == 0,
			long l => (l & 1) == 0,
			ulong ul => (ul & 1) == 0,
			char c => (c & 1) == 0,
			_ => false
		};
	}
}