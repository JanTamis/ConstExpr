using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	public abstract class BaseStringFunctionOptimizer(SyntaxNode? instance, string name) : BaseFunctionOptimizer
	{
		public string Name { get; } = name;

		public SyntaxNode? Instance { get; } = instance;

		protected bool IsValidMethod(IMethodSymbol method, out INamedTypeSymbol type)
		{
			type = method.ContainingType;

			return method.Name == Name;
		}

		protected bool TryGetStringInstance(out string? result)
		{
			if (Instance is LiteralExpressionSyntax les)
			{
				if (les.IsKind(SyntaxKind.StringLiteralExpression))
				{
					result = les.Token.ValueText;
					return true;
				}

				if (les.IsKind(SyntaxKind.NullLiteralExpression))
				{
					result = null;
					return true;
				}
			}

			result = null;
			return false;
		}
	}
}
