using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetPropertyReferenceValue(Compilation compilation, IPropertyReferenceOperation propertyReferenceOperation)
	{
		var instance = GetConstantValue(compilation, propertyReferenceOperation.Instance);
		var property = propertyReferenceOperation.Property;

		return SyntaxHelpers.GetPropertyValue(compilation, property, instance);
	}
}