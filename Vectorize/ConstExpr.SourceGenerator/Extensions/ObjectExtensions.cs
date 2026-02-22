using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

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
		return (T) ExecuteArithmeticOperation(left, right, Expression.Add)!;
	}

	public static T? Subtract<T>(this T left, T right)
	{
		return (T?) ExecuteArithmeticOperation(left, right, Expression.Subtract);
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
    {
      return null;
    }

    var lType = left.GetType();
		var rType = right.GetType();

		try
		{
			// String concatenation special case
			if (lType == typeof(string) || rType == typeof(string))
			{
				if (operation == (Func<Expression, Expression, BinaryExpression>) Expression.Add)
				{
					return left?.ToString() + right?.ToString();
				}

        return null;
			}

			// Check if both are numeric types
			if (!IsNumericType(lType) || !IsNumericType(rType))
      {
        return null;
      }

      // Find common type according to C# numeric promotion rules
      var common = GetCommonArithmeticType(lType, rType);
			if (common is null)
      {
        return null;
      }

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

	/// <summary>
	/// Checks if the value is a numeric type.
	/// </summary>
	public static bool IsNumeric(this object? value)
	{
		return value is not null && IsNumericType(value.GetType());
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
      {
        return null;
      }

      return typeof(decimal);
		}

		// Double: if either is double, result is double
		if (tl == TypeCode.Double || tr == TypeCode.Double)
    {
      return typeof(double);
    }

    // Float: if either is float, result is float
    if (tl == TypeCode.Single || tr == TypeCode.Single)
    {
      return typeof(float);
    }

    // Integral promotions
    // Disallow long|ulong combination
    if ((tl == TypeCode.UInt64 && tr == TypeCode.Int64) || (tl == TypeCode.Int64 && tr == TypeCode.UInt64))
    {
      return null;
    }

    if (tl == TypeCode.UInt64 || tr == TypeCode.UInt64)
    {
      return typeof(ulong);
    }

    if (tl == TypeCode.Int64 || tr == TypeCode.Int64)
    {
      return typeof(long);
    }

    if (tl == TypeCode.UInt32 || tr == TypeCode.UInt32)
    {
      return typeof(uint);
    }

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
    {
      return null;
    }

    try
		{
			// Convert shift amount to int using expression tree
			var shift = ConvertToInt32(right);
			if (shift is null)
      {
        return null;
      }

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
    {
      return null;
    }

    var type = value.GetType();
		if (!IsNumericType(type) && !IsIntegralType(type))
    {
      return null;
    }

    try
		{
			var constant = Expression.Constant(value);
			var converted = Expression.Convert(constant, typeof(int));
			var boxed = Expression.Convert(converted, typeof(object));
			var lambda = Expression.Lambda<Func<object>>(boxed);
			return (int) lambda.Compile().Invoke();
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Converts a numeric object to a long value.
	/// </summary>
	public static long? ToLong(this object? value)
	{
		if (value is null)
    {
      return null;
    }

    var type = value.GetType();
		if (!IsNumericType(type) && !IsIntegralType(type))
    {
      return null;
    }

    try
		{
			var constant = Expression.Constant(value);
			var converted = Expression.Convert(constant, typeof(long));
			var boxed = Expression.Convert(converted, typeof(object));
			var lambda = Expression.Lambda<Func<object>>(boxed);
			return (long) lambda.Compile().Invoke();
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
    {
      return null;
    }

    var lType = left.GetType();

		try
		{
			if (!IsIntegralType(lType))
      {
        return null;
      }

      // Convert shift amount to int using expression tree
      var shiftAmount = ConvertToInt32(right);
			if (shiftAmount is null)
      {
        return null;
      }

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
    {
      return null;
    }

    var lType = left.GetType();
		var rType = right.GetType();

		try
		{
			// Same enum type: perform bitwise operation on underlying type and cast back
			if (lType.IsEnum && rType.IsEnum)
			{
				if (lType != rType)
        {
          return null;
        }

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
      {
        return null;
      }

      // Find common type according to C# numeric promotion rules
      var common = GetCommonBitwiseType(lType, rType);
			if (common is null)
      {
        return null;
      }

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
		if (t.IsEnum)
    {
      return false;
    }

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
    {
      return null;
    }

    if (!IsIntegralType(lt) || !IsIntegralType(rt))
    {
      return null;
    }

    var tl = Type.GetTypeCode(lt);
		var tr = Type.GetTypeCode(rt);

		// Disallow long|ulong combination (no implicit common type in C#)
		if ((tl == TypeCode.UInt64 && tr == TypeCode.Int64) || (tl == TypeCode.Int64 && tr == TypeCode.UInt64))
    {
      return null;
    }

    // C# binary numeric promotions:
    if (tl == TypeCode.UInt64 || tr == TypeCode.UInt64)
    {
      return typeof(ulong);
    }

    if (tl == TypeCode.Int64 || tr == TypeCode.Int64)
    {
      return typeof(long);
    }

    if (tl == TypeCode.UInt32 || tr == TypeCode.UInt32)
    {
      return typeof(uint);
    }

    return typeof(int);
	}

	public static object? ConditionalAnd(this object? left, object? right)
	{
		if (left is bool lb && right is bool rb)
    {
      return lb && rb;
    }

    return null;
	}

	public static object? ConditionalOr(this object? left, object? right)
	{
		if (left is bool lb && right is bool rb)
    {
      return lb || rb;
    }

    return null;
	}

	public static bool EqualsTo(this object? left, object? right)
	{
		return ExecuteComparisonOperation(left, right, Expression.Equal);
	}

	public static bool NotEqualsTo(this object? left, object? right)
	{
		return ExecuteComparisonOperation(left, right, Expression.NotEqual);
	}

	public static bool LessThan(this object? left, object? right)
	{
		return ExecuteComparisonOperation(left, right, Expression.LessThan);
	}

	private static bool ExecuteComparisonOperation(
		object? left,
		object? right,
		Func<Expression, Expression, BinaryExpression> operation)
	{
		// Handle null cases
		if (left is null || right is null)
		{
			return operation == Expression.Equal
				? left == right
				: left != right;
		}

		var lType = left.GetType();
		var rType = right.GetType();

		try
		{
			// Same type: direct comparison
			if (lType == rType)
			{
				var lc = Expression.Constant(left);
				var rc = Expression.Constant(right);
				var expr = operation(lc, rc);
				// var boxed = Expression.Convert(expr, typeof(object));
				var lambda = Expression.Lambda<Func<bool>>(expr);

				return lambda.Compile().Invoke();
			}

			// Both numeric types: use common arithmetic type
			if (IsNumericType(lType) && IsNumericType(rType))
			{
				var common = GetCommonArithmeticType(lType, rType);

				if (common is not null)
				{
					var lc = Expression.Convert(Expression.Constant(left), common);
					var rc = Expression.Convert(Expression.Constant(right), common);
					var expr = operation(lc, rc);
					var boxed = Expression.Convert(expr, typeof(object));
					var lambda = Expression.Lambda<Func<bool>>(boxed);
					return lambda.Compile().Invoke();
				}
			}

			// String comparison
			if (lType == typeof(string) || rType == typeof(string))
			{
				var leftStr = left?.ToString() ?? string.Empty;
				var rightStr = right?.ToString() ?? string.Empty;
				return operation == Expression.Equal
					? leftStr == rightStr
					: leftStr != rightStr;
			}

			// Fallback: use object's Equals method
			return operation == Expression.Equal
				? left.Equals(right)
				: !left.Equals(right);
		}
		catch
		{
			return false;
		}
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
			BinaryOperatorKind.Add => left.Add(right),
			BinaryOperatorKind.Subtract => left.Subtract(right),
			BinaryOperatorKind.Multiply => left.Multiply(right),
			BinaryOperatorKind.Divide => left.Divide(right),
			BinaryOperatorKind.Remainder => left.Modulo(right),
			BinaryOperatorKind.LeftShift => left.LeftShift(right),
			BinaryOperatorKind.RightShift => left.RightShift(right),
			BinaryOperatorKind.UnsignedRightShift => UnsignedRightShift(left, right),
			BinaryOperatorKind.And => left.And(right),
			BinaryOperatorKind.Or => left.Or(right),
			BinaryOperatorKind.ExclusiveOr => left.ExclusiveOr(right),
			BinaryOperatorKind.ConditionalAnd => left.ConditionalAnd(right),
			BinaryOperatorKind.ConditionalOr => left.ConditionalOr(right),
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
			SyntaxKind.AddExpression or SyntaxKind.PlusEqualsToken => left.Add(right),
			SyntaxKind.SubtractExpression or SyntaxKind.MinusEqualsToken => left.Subtract(right),
			SyntaxKind.MultiplyExpression or SyntaxKind.AsteriskEqualsToken => left.Multiply(right),
			SyntaxKind.DivideExpression or SyntaxKind.SlashEqualsToken => left.Divide(right),
			SyntaxKind.ModuloExpression or SyntaxKind.PercentEqualsToken => left.Modulo(right),
			SyntaxKind.LeftShiftExpression or SyntaxKind.LessThanLessThanEqualsToken => left.LeftShift(right),
			SyntaxKind.RightShiftExpression or SyntaxKind.GreaterThanGreaterThanEqualsToken => left.RightShift(right),
			SyntaxKind.UnsignedRightShiftExpression or SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken => UnsignedRightShift(left, right),
			SyntaxKind.BitwiseAndExpression or SyntaxKind.AmpersandEqualsToken => left.And(right),
			SyntaxKind.BitwiseOrExpression or SyntaxKind.BarEqualsToken => left.Or(right),
			SyntaxKind.ExclusiveOrExpression or SyntaxKind.CaretEqualsToken => left.ExclusiveOr(right),
			SyntaxKind.LogicalAndExpression => left.ConditionalAnd(right),
			SyntaxKind.LogicalOrExpression => left.ConditionalOr(right),
			SyntaxKind.EqualsExpression => EqualityComparer<object?>.Default.Equals(left, right),
			SyntaxKind.NotEqualsExpression => !EqualityComparer<object?>.Default.Equals(left, right),
			SyntaxKind.LessThanExpression => Comparer<object?>.Default.Compare(left, right) < 0,
			SyntaxKind.LessThanOrEqualExpression => Comparer<object?>.Default.Compare(left, right) <= 0,
			SyntaxKind.GreaterThanExpression => Comparer<object?>.Default.Compare(left, right) > 0,
			SyntaxKind.GreaterThanOrEqualExpression => Comparer<object?>.Default.Compare(left, right) >= 0,
			SyntaxKind.CoalesceExpression => left ?? right,
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
		LiteralExpressionSyntax literal => literal.Token.Value.IsNumericZero(),
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
		LiteralExpressionSyntax literal => literal.Token.Value.IsNumericOne(),
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
		LiteralExpressionSyntax literal => literal.Token.Value.IsNumericTwo(),
		_ => false
	};

	public static bool IsNumericTwo(this LiteralExpressionSyntax literal)
	{
		return literal.Token.Value.IsNumericTwo();
	}

	public static bool IsNumericNegativeOne(this object? value) => value switch
	{
		sbyte sb => sb == -1,
		short s => s == -1,
		int i => i == -1,
		long l => l == -1,
		float f => Math.Abs(f - -1f) < Single.Epsilon,
		double d => Math.Abs(d - -1d) < Double.Epsilon,
		decimal m => m == -1m,
		PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.UnaryMinusExpression) &&
		                                        prefix.Operand is LiteralExpressionSyntax lit =>
			lit.Token.Value.IsNumericOne(),
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
		ulong ul => ul == (ulong) target,
		float f => Math.Abs(f - target) < Single.Epsilon,
		double d => Math.Abs(d - target) < Double.Epsilon,
		decimal m => m == target,
		_ => false
	};

	public static bool IsNumericValue(this LiteralExpressionSyntax literal, int target)
	{
		return literal.Token.Value.IsNumericValue(target);
	}

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
      {
        return false;
      }

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
			sbyte sb and > 0 when (sb & (sb - 1)) == 0 => (power = Log2((byte) sb)) >= 0,
			short s and > 0 when (s & (s - 1)) == 0 => (power = Log2((ushort) s)) >= 0,
			ushort us when us != 0 && (us & (us - 1)) == 0 => (power = Log2(us)) >= 0,
			int i and > 0 when (i & (i - 1)) == 0 => (power = Log2((uint) i)) >= 0,
			uint ui when ui != 0 && (ui & (ui - 1)) == 0 => (power = Log2(ui)) >= 0,
			long l and > 0 when (l & (l - 1)) == 0 => (power = Log2((ulong) l)) >= 0,
			ulong ul when ul != 0 && (ul & (ul - 1)) == 0 => (power = Log2(ul)) >= 0,

			// Floating-point: alleen positieve, gehele waarden binnen ulong-bereik
			float f when !float.IsNaN(f) && !float.IsInfinity(f) && f > 0f && f == MathF.Truncate(f) && f <= ulong.MaxValue &&
			             (((ulong) f & ((ulong) f - 1)) == 0) => (power = Log2((ulong) f)) >= 0,
			double d when !double.IsNaN(d) && !double.IsInfinity(d) && d > 0d && d == Math.Truncate(d) && d <= ulong.MaxValue &&
			              (((ulong) d & ((ulong) d - 1)) == 0) => (power = Log2((ulong) d)) >= 0,

			// Decimal: positieve, gehele waarden (geen fracties)
			decimal m when IsDecimalIntegerPowerOfTwo(m, out var p) => (power = p) >= 0,

			_ => false
		};
	}

	public static bool IsNumericPowerOfTwo(this LiteralExpressionSyntax literal, out int power)
	{
		return literal.Token.Value.IsNumericPowerOfTwo(out power);
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

	/// <summary>
	/// Represents the type of cluster pattern detected.
	/// </summary>
	public enum ClusterType
	{
		None,
		/// <summary>Consecutive values (step = 1), e.g., {1, 2, 3, 4}.</summary>
		Consecutive,
		/// <summary>Arithmetic sequence with constant step, e.g., {2, 4, 6, 8}.</summary>
		Step,
		/// <summary>Power of two values, e.g., {1, 2, 4, 8, 16}.</summary>
		PowerOfTwo,
		/// <summary>All even numbers.</summary>
		Even,
		/// <summary>All odd numbers.</summary>
		Odd,
		/// <summary>Values can be checked with a bitmask.</summary>
		Bitmask,
	}

	/// <summary>
	/// Represents a detected cluster of values with pattern information.
	/// </summary>
	public class Cluster
	{
		public ClusterType Type { get; init; }
		public object Start { get; init; }
		public object End { get; init; }
		public object Diff { get; init; }
		public object? Step { get; init; }
		public IReadOnlyList<object> Values { get; init; }
		public int StartIndex { get; init; }
		public int EndIndex { get; init; }

		public ExpressionSyntax StartExpression { get; set; }
		public ExpressionSyntax EndExpression { get; set; }
		public ExpressionSyntax StepExpression { get; set; }
		public ExpressionSyntax DiffExpression { get; set; }
		public ExpressionSyntax Expression { get; set; }

		public int Count => Values.Count;
	}

	public static IEnumerable<Cluster> GetClusterPatterns(this IList<object?> items)
	{
		if (items.Count == 0)
    {
      yield break;
    }

    // Convert to non-nullable list for helper methods
    var values = items
			.Where(x => x != null)
			.Distinct()
			.Cast<object>()
			.ToList();

		if (values.Count == 0)
    {
      yield break;
    }

    var i = 0;

		while (i < values.Count)
		{
			var cluster = DetectBestCluster(values, i);
			yield return cluster;
			i = cluster.EndIndex + 1;
		}
	}

	private static Cluster DetectBestCluster(IList<object> values, int startIndex)
	{
		var bestEndIndex = startIndex;
		var bestType = ClusterType.None;
		object? bestStep = null;

		// 1. Try consecutive sequence (step = 1) - highest priority for range optimization
		if (values.TryGetConsecutiveSet(startIndex, out var consecutiveEnd)
		    && consecutiveEnd > bestEndIndex)
		{
			bestEndIndex = consecutiveEnd;
			bestType = ClusterType.Consecutive;
			bestStep = 1;
		}

		// 2. Try arithmetic sequence with any step
		if (values.TryGetStepSet(startIndex, out var stepEnd, out var step)
		    && stepEnd > bestEndIndex)
		{
			bestEndIndex = stepEnd;
			bestType = ClusterType.Step;
			bestStep = step;
		}

		// 3. Try power of two sequence
		if (values.TryGetPowerOfTwoSet(startIndex, out var powerEnd)
		    && powerEnd >= bestEndIndex && powerEnd > startIndex && bestType != ClusterType.Consecutive)
		{
			// Power of two is preferred if it covers more or equal elements
			bestEndIndex = powerEnd;
			bestType = ClusterType.PowerOfTwo;
			bestStep = null;
		}

		// 4. Try even number sequence
		if (values.TryGetEvenNumberSet(startIndex, out var evenEnd)
		    && evenEnd - startIndex > 1
		    && evenEnd > bestEndIndex)
		{
			// Only use if all values are even and it covers more elements
			bestEndIndex = evenEnd;
			bestType = ClusterType.Even;
			bestStep = 2;
		}

		// 5. Try odd number sequence
		if (values.TryGetOddNumberSet(startIndex, out var oddEnd)
		    && oddEnd - startIndex > 1
		    && oddEnd > bestEndIndex)
		{
			bestEndIndex = oddEnd;
			bestType = ClusterType.Odd;
			bestStep = 2;
		}

		// 6. Check if bitmask is applicable for the detected range
		var clusterValues = values
			.Skip(startIndex)
			.ToList();

		if (clusterValues.Count >= 2
		    && clusterValues.TryGetBitmaskCandidate(out _, out _, out var bitCount)
		    && bitCount <= 64
		    && bestType is not ClusterType.Consecutive and not ClusterType.PowerOfTwo)
		{
			// Bitmask is efficient for sparse sets within 64 bits
			bestType = ClusterType.Bitmask;
			bestEndIndex = startIndex + clusterValues.Count - 1;
			bestStep = bitCount;
		}

		// Build result
		var resultValues = new List<object>(bestEndIndex - startIndex + 1);

		for (var j = startIndex; j <= bestEndIndex; j++)
		{
			resultValues.Add(values[j]);
		}

		return new Cluster
		{
			Type = bestType,
			Start = values[startIndex],
			End = values[bestEndIndex],
			Diff = values[bestEndIndex].Subtract(values[startIndex])!,
			Step = bestStep,
			Values = resultValues,
			StartIndex = startIndex,
			EndIndex = bestEndIndex,
		};
	}

	public static bool IsEvenNumber(this object? value)
	{
		return value.And(1).IsNumericZero();
	}

	public static bool IsPowerOfTwo(this object value)
	{
		return value.And(value.Subtract(1)).IsNumericZero();
	}

	/// <summary>
	/// Tries to get an arithmetic sequence (constant step) starting at the given index.
	/// Returns true if a valid sequence with at least 2 elements is found.
	/// </summary>
	public static bool TryGetStepSet(this IList<object> values, int index, out int endIndex, out object? step)
	{
		endIndex = index;
		step = null;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index + 1;

		if (j >= values.Count)
    {
      return false;
    }

    step = values[j].Subtract(values[index]);
		if (step == null)
    {
      return false;
    }

    var previous = values[index];

		while (j < values.Count)
		{
			var diff = values[j].Subtract(previous);

			if (diff == null || !diff.EqualsTo(step))
      {
        break;
      }

      previous = values[j];
			j++;
		}

		endIndex = j - 1;
		return endIndex > index; // At least 2 elements
	}

	/// <summary>
	/// Tries to get a power-of-two sequence starting at the given index.
	/// Returns true if at least one power-of-two value is found.
	/// </summary>
	public static bool TryGetPowerOfTwoSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index;

		while (j < values.Count && values[j].IsPowerOfTwo())
		{
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index && values[index].IsPowerOfTwo();
	}

	/// <summary>
	/// Tries to get a consecutive sequence (step = 1) starting at the given index.
	/// Returns true if at least 2 consecutive values are found.
	/// </summary>
	public static bool TryGetConsecutiveSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var current = values[index];
		var j = index + 1;

		while (j < values.Count)
		{
			var next = values[j];
			var diff = next.Subtract(current);

			if (diff == null || !diff.IsNumericOne())
      {
        break;
      }

      current = next;
			j++;
		}

		endIndex = j - 1;
		return endIndex > index; // At least 2 elements
	}

	/// <summary>
	/// Tries to get an even numbers sequence starting at the given index.
	/// Returns true if at least one even number is found.
	/// The sequence must be consecutive with no gaps (e.g., 2, 4, 6, 8 - not 2, 4, 8).
	/// </summary>
	public static bool TryGetEvenNumberSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    if (!values[index].IsEvenNumber())
    {
      return false;
    }

    var j = index;
		var current = values[j].ToLong();
		if (current is null)
    {
      return false;
    }

    j++;

		while (j < values.Count && values[j].IsEvenNumber())
		{
			var next = values[j].ToLong();
			if (next is null)
      {
        break;
      }

      // Check if consecutive even number (difference must be exactly 2)
      if (next.Value - current.Value != 2)
      {
        break;
      }

      current = next;
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index;
	}

	/// <summary>
	/// Tries to get an odd numbers sequence starting at the given index.
	/// Returns true if at least one odd number is found.
	/// The sequence must be consecutive with no gaps (e.g., 1, 3, 5, 7 - not 1, 3, 7).
	/// </summary>
	public static bool TryGetOddNumberSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    if (values[index].IsEvenNumber())
    {
      return false;
    }

    var j = index;
		var current = values[j].ToLong();
		if (current is null)
    {
      return false;
    }

    j++;

		while (j < values.Count && !values[j].IsEvenNumber())
		{
			var next = values[j].ToLong();
			if (next is null)
      {
        break;
      }

      // Check if consecutive odd number (difference must be exactly 2)
      if (next.Value - current.Value != 2)
      {
        break;
      }

      current = next;
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index;
	}

	/// <summary>
	/// Tries to get a sequence where all values are positive starting at the given index.
	/// Returns true if at least one positive value is found.
	/// </summary>
	public static bool TryGetPositiveSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index;

		while (j < values.Count && values[j].IsPositive())
		{
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index && values[index].IsPositive();
	}

	/// <summary>
	/// Tries to get a sequence where all values are negative starting at the given index.
	/// Returns true if at least one negative value is found.
	/// </summary>
	public static bool TryGetNegativeSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index;

		while (j < values.Count && values[j].IsNegative())
		{
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index && values[index].IsNegative();
	}

	/// <summary>
	/// Tries to get a range bounded sequence starting at the given index.
	/// Returns true if at least one value within the range is found.
	/// </summary>
	public static bool TryGetRangeSet(this IList<object> values, int index, object min, object max, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index;

		while (j < values.Count)
		{
			var value = values[j];
			var tooSmall = ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, min);
			var tooLarge = ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, max);

			if (tooSmall is true || tooLarge is true)
      {
        break;
      }

      j++;
		}

		endIndex = j - 1;

		if (endIndex < index)
    {
      return false;
    }

    var firstValue = values[index];
		var firstTooSmall = ExecuteBinaryOperation(BinaryOperatorKind.LessThan, firstValue, min);
		var firstTooLarge = ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, firstValue, max);

		return firstTooSmall is not true && firstTooLarge is not true;
	}

	/// <summary>
	/// Tries to get a constant value sequence starting at the given index.
	/// Returns true if at least 2 identical values are found.
	/// </summary>
	public static bool TryGetConstantSet(this IList<object> values, int index, out int endIndex, out object constant)
	{
		endIndex = index;
		constant = null!;

		if (index >= values.Count)
    {
      return false;
    }

    constant = values[index];
		var j = index + 1;

		while (j < values.Count && values[j].EqualsTo(constant))
		{
			j++;
		}

		endIndex = j - 1;
		return endIndex > index; // At least 2 identical elements
	}

	/// <summary>
	/// Tries to get a geometric sequence (each value is previous * ratio) starting at the given index.
	/// Returns true if at least 2 elements form a geometric sequence.
	/// </summary>
	public static bool TryGetGeometricSet(this IList<object> values, int index, out int endIndex, out object? ratio)
	{
		endIndex = index;
		ratio = null;

		if (index >= values.Count || index + 1 >= values.Count || values[index].IsNumericZero())
		{
			return false;
		}

		ratio = values[index + 1].Divide(values[index]);

		if (ratio?.IsNumericZero() == true)
		{
			return false;
		}

		var j = index + 1;

		while (j < values.Count && !values[j - 1].IsNumericZero())
		{
			var expectedRatio = values[j].Divide(values[j - 1]);

			if (expectedRatio == null || !expectedRatio.EqualsTo(ratio))
			{
				break;
			}

			j++;
		}

		endIndex = j - 1;
		return endIndex > index; // At least 2 elements
	}

	/// <summary>
	/// Tries to get a bit flag pattern sequence starting at the given index.
	/// Each value should be non-zero.
	/// Returns true if at least one non-zero value is found.
	/// </summary>
	public static bool TryGetBitFlagSet(this IList<object> values, int index, out int endIndex)
	{
		endIndex = index;

		if (index >= values.Count)
    {
      return false;
    }

    var j = index;

		while (j < values.Count && !values[j].IsNumericZero())
		{
			j++;
		}

		endIndex = j - 1;
		return endIndex >= index && !values[index].IsNumericZero();
	}

	/// <summary>
	/// Checks if all values in a range can be represented efficiently as a bitmask.
	/// Returns true if (max - min) fits within a reasonable bit count (e.g., 64 bits).
	/// </summary>
	public static bool TryGetBitmaskCandidate(this IList<object> values, out object min, out object max, out int bitCount)
	{
		min = null!;
		max = null!;
		bitCount = 0;

		if (values.Count == 0)
		{
			return false;
		}

		min = values[0];
		max = values[0];

		foreach (var value in values)
		{
			if (value == null)
			{
				return false;
			}

			var lessThanMin = ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, min);

			if (lessThanMin is true)
			{
				min = value;
			}

			var greaterThanMax = ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, max);

			if (greaterThanMax is true)
			{
				max = value;
			}
		}

		var range = max.Subtract(min);

		if (range == null)
		{
			return false;
		}

		// Convert to int to check bit count
		try
		{
			var rangeInt = Convert.ToInt32(range);
			bitCount = rangeInt + 1;

			// Bitmask is efficient if range fits in 64 bits or less
			return bitCount <= 64 && bitCount >= values.Count;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Checks if the values form a dense range (most values in range are present).
	/// Returns true if density is above the threshold.
	/// </summary>
	public static bool TryGetDenseRange(this IList<object> values, out double density, double densityThreshold = 0.5)
	{
		density = 0.0;

		if (values.Count <= 1)
		{
			return false;
		}

		var min = values[0];
		var max = values[0];

		foreach (var value in values)
		{
			if (value == null)
			{
				return false;
			}

			var lessThanMin = ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, min);

			if (lessThanMin is true)
			{
				min = value;
			}

			var greaterThanMax = ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, max);

			if (greaterThanMax is true)
			{
				max = value;
			}
		}

		var range = max.Subtract(min);

		if (range == null)
		{
			return false;
		}

		try
		{
			var rangeInt = Convert.ToInt32(range);
			var rangeSize = rangeInt + 1;
			density = (double) values.Count / rangeSize;

			return density >= densityThreshold;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Gets the density of the sparse set - useful for determining if jump table or linear search is better.
	/// Returns true if density could be calculated.
	/// </summary>
	public static bool TryGetSetDensity(this IList<object> values, out double density)
	{
		density = 0.0;

		if (values.Count <= 1)
		{
			density = 1.0;
			return true;
		}

		var min = values[0];
		var max = values[0];

		foreach (var value in values)
		{
			if (value == null)
      {
        return false;
      }

      var lessThanMin = ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, min);
			if (lessThanMin is true)
      {
        min = value;
      }

      var greaterThanMax = ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, max);
			if (greaterThanMax is true)
      {
        max = value;
      }
    }

		var range = max.Subtract(min);
		if (range == null)
    {
      return false;
    }

    try
		{
			var rangeInt = Convert.ToInt32(range);
			var rangeSize = rangeInt + 1;
			density = (double) values.Count / rangeSize;
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Finds common differences/gaps in the sequence. Useful for detecting patterns like {0, 3, 6, 9} (step=3).
	/// Returns true if all consecutive differences are equal.
	/// </summary>
	public static bool TryGetCommonDifference(this IList<object> values, out object? difference)
	{
		difference = null;

		if (values.Count < 2)
    {
      return false;
    }

    difference = values[1].Subtract(values[0]);
		if (difference == null)
    {
      return false;
    }

    for (var i = 2; i < values.Count; i++)
		{
			var diff = values[i].Subtract(values[i - 1]);

			if (diff == null || !diff.EqualsTo(difference))
			{
				difference = null;
				return false;
			}
		}

		return true;
	}
}