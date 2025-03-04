using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

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

		return ExecuteMethod(compilation, targetMethod, instance, arguments);
	}
}