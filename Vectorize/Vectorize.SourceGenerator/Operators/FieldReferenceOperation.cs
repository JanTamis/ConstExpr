using Microsoft.CodeAnalysis.Operations;
using Vectorize.Helpers;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetFieldReferenceValue(IFieldReferenceOperation fieldReferenceOperation)
	{
		var instance = GetConstantValue(fieldReferenceOperation.Instance);
		var field = fieldReferenceOperation.Field;
		
		return SyntaxHelpers.GetFieldValue(field, instance);
	}
}