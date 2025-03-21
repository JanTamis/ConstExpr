using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetFieldReferenceValue(Compilation compilation, IFieldReferenceOperation fieldReferenceOperation)
	{
		var instance = GetConstantValue(compilation, fieldReferenceOperation.Instance);
		var field = fieldReferenceOperation.Field;

		return compilation.GetFieldValue(loader, field, instance);
	}
}