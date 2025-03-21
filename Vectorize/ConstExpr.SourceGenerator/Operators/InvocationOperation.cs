using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetInvocationValue(Compilation compilation, IInvocationOperation invocationOperation)
	{
		var targetMethod = invocationOperation?.TargetMethod;
		var instance = GetConstantValue(compilation, invocationOperation.Instance);

		var arguments = invocationOperation.Arguments
			.Select(argument => GetConstantValue(compilation, argument.Value))
			.ToArray();

		return compilation.ExecuteMethod(targetMethod, instance, arguments);
	}
}