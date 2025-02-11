using Microsoft.CodeAnalysis.Operations;
using Vectorize.Helpers;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetPropertyReferenceValue(IPropertyReferenceOperation propertyReferenceOperation)
	{
		var instance = GetConstantValue(propertyReferenceOperation.Instance);
		var property = propertyReferenceOperation.Property;
		
		return SyntaxHelpers.GetPropertyValue(property, instance);
	}
}