using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SqrtFunctionOptimizer() : BaseMathFunctionOptimizer("Sqrt", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		var arg = parameters[0];

		// Sqrt(x * x) => Abs(x) for floating point (not for negative x in general case)
		if (arg is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } mul
		    && mul.Left.IsEquivalentTo(mul.Right)
		    && IsPure(mul.Left))
		{
			var mathType = ParseTypeName(paramType.Name);
			result = InvocationExpression(
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Abs")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(mul.Left))));
			return true;
		}

		return false;
	}
}

