using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	public class SubstringFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Substring")
	{
		public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
		{
			result = null;

			if (!IsValidMethod(method, out var stringType))
			{
				return false;
			}

			return true;
		}
	}
}
