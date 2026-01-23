using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Base class for LINQ function optimizers that handle Enumerable method optimizations.
/// Provides common helper methods for analyzing and transforming LINQ expressions.
/// </summary>
public abstract class BaseLinqFunctionOptimizer(string name, params HashSet<int> parameterCounts) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public HashSet<int> ParameterCounts { get; } = parameterCounts;

	/// <summary>
	/// Validates if the given method is a valid LINQ Enumerable method matching this optimizer's criteria.
	/// </summary>
	protected bool IsValidLinqMethod(IMethodSymbol method)
	{
		return method.Name == Name
		       && ParameterCounts.Contains(method.Parameters.Length)
		       && method.ContainingType.ToString() is "System.Linq.Enumerable";
	}
	
	/// <summary>
	/// Attempts to extract a lambda expression from the given parameter.
	/// </summary>
	protected bool TryGetLambda(ExpressionSyntax parameter, [NotNullWhen(true)] out LambdaExpressionSyntax? lambda)
	{
		lambda = null;

		if (parameter is LambdaExpressionSyntax lambdaExpression)
		{
			lambda = lambdaExpression;
			return true;
		}

		return false;
	}
	
	/// <summary>
	/// Checks if the given lambda is an identity lambda (e.g., x => x).
	/// </summary>
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

	/// <summary>
	/// Attempts to extract the source expression from a LINQ method chain.
	/// </summary>
	protected bool TryGetLinqSource(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out ExpressionSyntax? source)
	{
		source = null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		source = memberAccess.Expression;
		return true;
	}

	/// <summary>
	/// Checks if a method call is chained after another LINQ method (e.g., Where().Select()).
	/// </summary>
	protected bool IsLinqMethodChain(ExpressionSyntax expression, string methodName, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		invocation = null;

		if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } inv
		    || memberAccess.Name.Identifier.Text != methodName)
			return false;

		invocation = inv;
		return true;
	}

	/// <summary>
	/// Checks if a method call is chained after another LINQ method (e.g., Where().Select()).
	/// </summary>
	protected bool IsLinqMethodChain(ExpressionSyntax expression, ISet<string> methodNames, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		invocation = null;

		if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } inv
		    || !methodNames.Contains(memberAccess.Name.Identifier.Text))
			return false;

		invocation = inv;
		return true;
	}

	/// <summary>
	/// Extracts all arguments from a method invocation.
	/// </summary>
	protected IEnumerable<ArgumentSyntax> GetMethodArguments(InvocationExpressionSyntax invocation)
	{
		return invocation.ArgumentList.Arguments;
	}

	/// <summary>
	/// Creates a new method invocation on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateLinqMethodCall(ExpressionSyntax source, string methodName, params ArgumentSyntax[] arguments)
	{
		return SyntaxFactory.InvocationExpression(
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				source,
				SyntaxFactory.IdentifierName(methodName)),
			SyntaxFactory.ArgumentList(
				SyntaxFactory.SeparatedList(arguments)));
	}

	/// <summary>
	/// Attempts to match a LINQ method chain pattern and extract specific information.
	/// </summary>
	protected bool TryMatchLinqPattern(InvocationExpressionSyntax invocation, string expectedMethodName, 
		[NotNullWhen(true)] out MemberAccessExpressionSyntax? memberAccess, 
		[NotNullWhen(true)] out ArgumentListSyntax? argumentList)
	{
		memberAccess = null;
		argumentList = null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
			return false;

		if (memberAccessExpr.Name.Identifier.Text != expectedMethodName)
			return false;

		memberAccess = memberAccessExpr;
		argumentList = invocation.ArgumentList;
		return true;
	}

	/// <summary>
	/// Gets the parameter name from a simple lambda expression.
	/// </summary>
	protected bool TryGetLambdaParameterName(SimpleLambdaExpressionSyntax lambda, out string? parameterName)
	{
		parameterName = lambda.Parameter.Identifier.Text;
		return !string.IsNullOrEmpty(parameterName);
	}

	/// <summary>
	/// Gets parameter names from a parenthesized lambda expression.
	/// </summary>
	protected bool TryGetLambdaParameterNames(ParenthesizedLambdaExpressionSyntax lambda, out IList<string> parameterNames)
	{
		parameterNames = lambda.ParameterList.Parameters
			.Select(p => p.Identifier.Text)
			.ToList();
		return parameterNames.Count > 0;
	}

	/// <summary>
	/// Checks if a lambda body is a constant expression (e.g., x => 42).
	/// </summary>
	protected bool IsConstantLambda(LambdaExpressionSyntax lambda, out ExpressionSyntax? constantValue)
	{
		constantValue = null;
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		if (body is LiteralExpressionSyntax or IdentifierNameSyntax)
		{
			constantValue = body;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Extracts the lambda body as an expression syntax.
	/// </summary>
	protected bool TryGetLambdaBody(LambdaExpressionSyntax lambda, [NotNullWhen(true)] out ExpressionSyntax? body)
	{
		body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		return body is not null;
	}

	/// <summary>
	/// Checks if the expression is a direct method invocation on a collection.
	/// </summary>
	protected bool IsDirectLinqCall(ExpressionSyntax expression, string methodName, out InvocationExpressionSyntax? invocation)
	{
		invocation = null;

		if (expression is not InvocationExpressionSyntax inv)
			return false;

		if (inv.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		if (memberAccess.Name.Identifier.Text != methodName)
			return false;

		invocation = inv;
		return true;
	}

	/// <summary>
	/// Gets all chained LINQ method calls from an expression.
	/// </summary>
	protected IList<(string MethodName, InvocationExpressionSyntax Invocation)> GetLinqChain(ExpressionSyntax expression)
	{
		var chain = new List<(string, InvocationExpressionSyntax)>();
		var current = expression;

		while (current is InvocationExpressionSyntax invocation
		       && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			chain.Insert(0, (memberAccess.Name.Identifier.Text, invocation));
			current = memberAccess.Expression;
		}

		return chain;
	}

	/// <summary>
	/// Creates a method call with no arguments on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateSimpleLinqMethodCall(ExpressionSyntax source, string methodName)
	{
		return CreateLinqMethodCall(source, methodName);
	}

	/// <summary>
	/// Checks if two lambda expressions have compatible signatures for composition.
	/// </summary>
	protected bool AreCompatibleForComposition(LambdaExpressionSyntax first, LambdaExpressionSyntax second)
	{
		var firstParamCount = first switch
		{
			SimpleLambdaExpressionSyntax => 1,
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count,
			_ => 0
		};

		var secondParamCount = second switch
		{
			SimpleLambdaExpressionSyntax => 1,
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count,
			_ => 0
		};

		return firstParamCount == 1 && secondParamCount == 1;
	}

	/// <summary>
	/// Checks if a lambda expression always returns a boolean literal value.
	/// </summary>
	protected bool IsLiteralBooleanLambda(LambdaExpressionSyntax lambda, out bool? value)
	{
		value = null;
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		if (body is LiteralExpressionSyntax literal && literal.Token.Value is bool boolValue)
		{
			value = boolValue;
			return true;
		}

		return false;
	}
}