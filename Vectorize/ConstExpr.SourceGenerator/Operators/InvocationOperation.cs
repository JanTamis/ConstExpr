using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetInvocationValue(Compilation compilation, IInvocationOperation invocationOperation, Dictionary<string, object?> variables)
	{
		var targetMethod = invocationOperation?.TargetMethod;
		var instance = GetConstantValue(compilation, invocationOperation.Instance);

		var arguments = invocationOperation.Arguments
			.Select(argument => GetConstantValue(compilation, argument.Value))
			.ToArray();
		
		loader.TryExecuteMethod(targetMethod, instance, variables, arguments, out var value);
		return value;
	}
}