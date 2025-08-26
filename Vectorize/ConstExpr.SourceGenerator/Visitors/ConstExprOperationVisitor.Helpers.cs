using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ConstExpr.SourceGenerator.Visitors;

public partial class ConstExprOperationVisitor
{
	

	private string? GetVariableName(IOperation operation)
	{
		return operation switch
		{
			ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local.Name,
			IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter.Name,
			// IPropertyReferenceOperation propertyReferenceOperation => propertyReferenceOperation.Property.Name,
			// IFieldReferenceOperation fieldReferenceOperation => fieldReferenceOperation.Field.Name,
			IVariableDeclaratorOperation variableDeclaratorOperation => variableDeclaratorOperation.Symbol.Name,
			_ => null,
		};
	}

	private void VisitList(ImmutableArray<IOperation> operations, IDictionary<string, object?> argument)
	{
		foreach (var operation in operations)
		{
			Visit(operation, argument);
		}
	}
}