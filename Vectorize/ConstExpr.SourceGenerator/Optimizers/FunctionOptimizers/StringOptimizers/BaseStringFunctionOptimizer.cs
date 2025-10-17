using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	public abstract class BaseStringFunctionOptimizer(string name) : BaseFunctionOptimizer
	{
		public string Name { get; } = name;

		protected bool IsValidMethod(IMethodSymbol method, out INamedTypeSymbol type)
		{
			type = method.ContainingType;

			return method.Name == Name;
		}
	}
}
