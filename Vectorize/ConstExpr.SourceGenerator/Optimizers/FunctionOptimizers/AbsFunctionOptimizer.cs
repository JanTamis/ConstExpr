using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AbsBaseFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, out SyntaxNode? result)
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