using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;

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

		return compilation.ExecuteMethod(loader, targetMethod, instance, variables, arguments);
	}
}