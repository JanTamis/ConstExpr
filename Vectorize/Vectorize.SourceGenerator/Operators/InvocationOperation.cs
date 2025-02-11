using System;
using System.Linq;
using Microsoft.CodeAnalysis.Operations;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetInvocationValue(IInvocationOperation invocationOperation)
	{
		var targetMethod = invocationOperation?.TargetMethod;
		var instance = GetConstantValue(invocationOperation.Instance);

		var arguments = invocationOperation.Arguments
			.Select(argument => GetConstantValue(argument.Value))
			.ToArray();

		return ExecuteMethod(targetMethod, instance, arguments);
	}
}