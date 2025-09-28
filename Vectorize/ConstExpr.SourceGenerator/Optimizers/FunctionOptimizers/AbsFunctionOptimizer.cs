using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AbsBaseFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, IList<ExpressionSyntax> parameters, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Abs" || method.ContainingType?.ToString() is not "System.Math" and not "System.MathF")
		{
			return false;
		}

		var paramType = method.Parameters[0].Type;

		if (paramType.IsUnsignedInteger())
		{
			// Abs(x) where x is unsigned => x
			result = parameters[0];
			return true;
		}
		
		result = CreateInvocation(paramType, "Abs", parameters[0]);
		return true;
	}
}