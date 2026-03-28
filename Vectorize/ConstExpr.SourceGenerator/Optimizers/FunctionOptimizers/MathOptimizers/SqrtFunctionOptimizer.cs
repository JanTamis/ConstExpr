using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SqrtFunctionOptimizer() : BaseMathFunctionOptimizer("Sqrt", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Sqrt(x * x) => Abs(x) for floating point (not for negative x in general case)
		if (arg is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } mul
		    && mul.Left.IsEquivalentTo(mul.Right)
		    && IsPure(mul.Left))
		{
			var mathType = ParseTypeName(paramType.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("Abs")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(mul.Left))));
			return true;
		}
		
		
		result = CreateInvocation(paramType, "Sqrt", arg);
		return true;
	}
}
