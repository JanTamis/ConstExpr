using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public abstract class BaseLinqFunctionOptimizer(string name, params HashSet<int> parameterCounts) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public HashSet<int> ParameterCounts { get; } = parameterCounts;

	protected bool IsValidLinqMethod(IMethodSymbol method)
	{
		return method.Name == Name
		       && ParameterCounts.Contains(method.Parameters.Length)
		       && method.ContainingType.ToString() is "System.Linq.Enumerable";
	}
	
	protected bool TryGetLambda(ArgumentSyntax parameter, [NotNullWhen(true)] out LambdaExpressionSyntax? lambda)
	{
		lambda = null;

		if (parameter.Expression is LambdaExpressionSyntax lambdaExpression)
		{
			lambda = lambdaExpression;
			return true;
		}

		return false;
	}
	
	protected bool IsIdentityLambda(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: IdentifierNameSyntax identifierName } simpleLambda 
				=> identifierName.Identifier.Text == simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { Body: IdentifierNameSyntax identifierName, ParameterList.Parameters.Count: 1 } parenthesizedLambda 
				=> identifierName.Identifier.Text == parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => false
		};
	}
}