using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetConversionValue(Compilation compilation, IConversionOperation conversionOperation)
	{
		var operand = GetConstantValue(compilation, conversionOperation.Operand);
		var conversion = conversionOperation.Type;

		return conversion.SpecialType switch
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
			SpecialType.System_Collections_IEnumerable => (IEnumerable)operand,
			_ => operand,
		};
	}
}