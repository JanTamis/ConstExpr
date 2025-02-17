using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Vectorize.Helpers;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetFieldReferenceValue(Compilation compilation, IFieldReferenceOperation fieldReferenceOperation)
	{
		var instance = GetConstantValue(compilation, fieldReferenceOperation.Instance);
		var field = fieldReferenceOperation.Field;

		return SyntaxHelpers.GetFieldValue(compilation, field, instance);
	}
}