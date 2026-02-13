using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
	protected bool IsValidLinqMethod(SemanticModel model, IMethodSymbol method)
	{
		return method.Name == Name
		       && ParameterCounts.Contains(method.Parameters.Length);
		// && method.ContainingType.EqualsType(model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable"));
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
	protected bool TryGetLinqSource(InvocationExpressionSyntax invocation, [NotNullWhen(true)] [NotNullIfNotNull(nameof(invocation))] out ExpressionSyntax? source)
	{
		source = invocation;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			return false;
		}

		source = memberAccess.Expression;
		return true;
	}

	/// <summary>
	/// Checks if a method call is chained after another LINQ method (e.g., Where().Select()).
	/// </summary>
	protected bool IsLinqMethodChain(ExpressionSyntax? expression, string methodName, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		invocation = null;

		if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } inv
		    || memberAccess.Name.Identifier.Text != methodName)
		{
			return false;
		}

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
		{
			return false;
		}

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
	protected InvocationExpressionSyntax CreateInvocation(ExpressionSyntax source, string methodName, params IEnumerable<ExpressionSyntax> arguments)
	{
		return InvocationExpression(
			CreateMemberAccess(source, methodName),
			ArgumentList(
				SeparatedList(arguments.Select(Argument))));
	}

	/// <summary>
	/// Creates a new method invocation on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateInvocation(ExpressionSyntax source, SimpleNameSyntax method, params IEnumerable<ExpressionSyntax> arguments)
	{
		return InvocationExpression(
			CreateMemberAccess(source, method),
			ArgumentList(
				SeparatedList(arguments.Select(Argument))));
	}

	/// <summary>
	/// Creates a new method invocation on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateInvocation(ExpressionSyntax source, Delegate method, params IEnumerable<ExpressionSyntax> arguments)
	{
		return InvocationExpression(
			CreateMemberAccess(source, method.Method.Name),
			ArgumentList(
				SeparatedList(arguments.Select(Argument))));
	}

	/// <summary>
	/// Creates a new method invocation on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateInvocation(Delegate method, params IEnumerable<ExpressionSyntax> arguments)
	{
		return InvocationExpression(
			CreateMemberAccess(ParseTypeName(method.Method.DeclaringType?.ToString() ?? throw new InvalidOperationException("Method must have a declaring type")), method.Method.Name),
			ArgumentList(
				SeparatedList(arguments.Select(Argument))));
	}

	protected MemberAccessExpressionSyntax CreateMemberAccess(ExpressionSyntax source, string memberName)
	{
		return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, source, IdentifierName(memberName));
	}

	protected MemberAccessExpressionSyntax CreateMemberAccess(ExpressionSyntax source, SimpleNameSyntax name)
	{
		return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, source, name);
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
		{
			return false;
		}

		if (memberAccessExpr.Name.Identifier.Text != expectedMethodName)
		{
			return false;
		}

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
		return !String.IsNullOrEmpty(parameterName);
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
		{
			return false;
		}

		if (inv.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			return false;
		}

		if (memberAccess.Name.Identifier.Text != methodName)
		{
			return false;
		}

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

		while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
		{
			chain.Insert(0, (memberAccess.Name.Identifier.Text, invocation));
			current = memberAccess.Expression;
		}

		return chain;
	}

	/// <summary>
	/// Creates a method call with no arguments on the given source expression.
	/// </summary>
	protected InvocationExpressionSyntax CreateSimpleInvocation(ExpressionSyntax source, string methodName)
	{
		return CreateInvocation(source, methodName);
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

		if (body is LiteralExpressionSyntax { Token.Value: bool boolValue })
		{
			value = boolValue;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if a lambda expression is a simple equality check (e.g., x => x == 2 or x => 2 == x).
	/// If true, extracts the value being compared against.
	/// </summary>
	protected bool IsSimpleEqualityLambda(LambdaExpressionSyntax lambda, [NotNullWhen(true)] out ExpressionSyntax? equalityValue)
	{
		equalityValue = null;

		// Get the lambda parameter name and body
		var parameterName = lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda
				=> parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => null
		};

		if (parameterName is null)
		{
			return false;
		}

		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		// Check if body is a binary expression with equality operator
		if (body is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } binaryExpression)
		{
			return false;
		}

		// Check if left side is the parameter and right side is the value (x => x == value)
		if (binaryExpression.Left is IdentifierNameSyntax { } leftIdentifier
		    && leftIdentifier.Identifier.Text == parameterName)
		{
			equalityValue = binaryExpression.Right;
			return true;
		}

		// Check if right side is the parameter and left side is the value (x => value == x)
		if (binaryExpression.Right is IdentifierNameSyntax { } rightIdentifier
		    && rightIdentifier.Identifier.Text == parameterName)
		{
			equalityValue = binaryExpression.Left;
			return true;
		}

		return false;
	}

	protected InvocationExpressionSyntax CreateEmptyEnumerableCall(ITypeSymbol elementType)
	{
		return InvocationExpression(
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName("Enumerable"),
				GenericName(
						Identifier("Empty"))
					.WithTypeArgumentList(
						TypeArgumentList(
							SingletonSeparatedList(
								ParseTypeName(elementType.ToString()))))));
	}

	protected bool TryGetOptimizedChainExpression(ExpressionSyntax source, ISet<string> methodsToSkip, [NotNullWhen(true)] [NotNullIfNotNull(nameof(source))] out ExpressionSyntax? optimizedSource)
	{
		optimizedSource = source;

		var result = false;

		// Skip operations that don't affect behavior
		while (IsLinqMethodChain(optimizedSource, methodsToSkip, out var chainInvocation)
		       && TryGetLinqSource(chainInvocation, out var innerSource))
		{
			optimizedSource = innerSource;
			result = true;
		}

		return result;
	}

	/// <summary>
	/// Checks if the invocation is made on a List&lt;T&gt; type.
	/// </summary>
	protected bool IsInvokedOnList(SemanticModel model, ExpressionSyntax? expression)
	{
		if (!model.TryGetTypeSymbol(expression, out var type)
		    || type is not INamedTypeSymbol namedType)
		{
			return false;
		}

		// Check if the type is List<T>
		if (namedType.OriginalDefinition.SpecialType == SpecialType.None
		    && namedType.OriginalDefinition.ToString() == "System.Collections.Generic.List<T>")
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if the invocation is made on an array type.
	/// </summary>
	protected bool IsInvokedOnArray(SemanticModel model, [NotNullWhen(true)] ExpressionSyntax? expression)
	{
		if (expression is null)
		{
			return false;
		}

		return model.TryGetTypeSymbol(expression, out var type) && type is IArrayTypeSymbol;
	}

	protected bool IsSpecialType(SemanticModel model, ExpressionSyntax? expression, params HashSet<SpecialType> specialTypes)
	{
		return model.TryGetTypeSymbol(expression, out var type) && type.AllInterfaces.Any(s => specialTypes.Contains(s.SpecialType));
	}

	protected bool IsCollectionType(SemanticModel model, ExpressionSyntax? expression)
	{
		return IsSpecialType(model, expression,
			       SpecialType.System_Collections_Generic_IList_T,
			       SpecialType.System_Collections_Generic_IReadOnlyList_T,
			       SpecialType.System_Collections_Generic_ICollection_T,
			       SpecialType.System_Collections_Generic_IReadOnlyCollection_T)
		       || IsInvokedOnList(model, expression);
	}

	protected LambdaExpressionSyntax CombinePredicates(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// If parameters are the same, we can directly combine with &&
		// Otherwise, replace the outer parameter with the inner parameter
		ExpressionSyntax combinedBody;

		if (innerParam == outerParam)
		{
			// Both use the same parameter name: v => v > 1 && v < 5
			combinedBody = BinaryExpression(
				SyntaxKind.LogicalAndExpression,
				ParenthesizedExpression(innerBody),
				ParenthesizedExpression(outerBody));
		}
		else
		{
			// Different parameter names: replace outer parameter with inner parameter
			var renamedOuterBody = ReplaceIdentifier(outerBody, outerParam, IdentifierName(innerParam));
			combinedBody = BinaryExpression(
				SyntaxKind.LogicalAndExpression,
				ParenthesizedExpression(innerBody),
				ParenthesizedExpression(renamedOuterBody));
		}

		// Create a new lambda with the inner parameter and the combined body
		return SimpleLambdaExpression(
			Parameter(Identifier(innerParam)),
			combinedBody
		);
	}

	protected static string GetLambdaParameter(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: > 0 } parenthesizedLambda
				=> parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => throw new InvalidOperationException("Unsupported lambda expression type")
		};
	}

	protected static ExpressionSyntax GetLambdaBody(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax { ExpressionBody: { } body } => body,
			ParenthesizedLambdaExpressionSyntax { ExpressionBody: { } body } => body,
			_ => throw new InvalidOperationException("Only expression-bodied lambdas are supported")
		};
	}

	/// <summary>
	/// Checks if the given expression is Enumerable.Empty&lt;T&gt;() or [].
	/// </summary>
	protected bool IsEmptyEnumerable(ExpressionSyntax expression)
	{
		return expression is InvocationExpressionSyntax
		{
			Expression: MemberAccessExpressionSyntax
			{
				Name.Identifier.Text: nameof(Enumerable.Empty),
				Expression: IdentifierNameSyntax { Identifier.Text: nameof(Enumerable) }
			},
			ArgumentList.Arguments.Count: 0
		} or CollectionExpressionSyntax { Elements.Count: 0 };
	}

	/// <summary>
	/// Checks if two expressions are syntactically equivalent (simple text comparison).
	/// This is a conservative check - it won't catch all semantic equivalences, but it's safe.
	/// </summary>
	protected bool AreSyntacticallyEquivalent(ExpressionSyntax first, ExpressionSyntax second)
	{
		// Simple syntactic comparison by normalized text
		var firstText = first.ToString().Replace(" ", "").Replace("\n", "").Replace("\r", "");
		var secondText = second.ToString().Replace(" ", "").Replace("\n", "").Replace("\r", "");
		return firstText == secondText;
	}

	protected static ExpressionSyntax ReplaceIdentifier(ExpressionSyntax expression, string oldIdentifier, ExpressionSyntax replacement)
	{
		// Wrap replacement in parentheses if it's a binary expression to preserve precedence
		var wrappedReplacement = replacement is BinaryExpressionSyntax
			? ParenthesizedExpression(replacement)
			: replacement;
		
		return (ExpressionSyntax) new IdentifierReplacer(oldIdentifier, wrappedReplacement).Visit(expression);
	}

	protected bool TryGetValues(SyntaxNode node, [NotNullWhen(true)] out IList<object?>? values)
	{
		if (node is CollectionExpressionSyntax collectionExpression)
		{
			var elements = collectionExpression.Elements;
			var constantValues = new List<object?>();

			foreach (var element in elements)
			{
				if (element is not ExpressionElementSyntax expressionElement)
				{
					values = null;
					return false;
				}

				if (expressionElement.Expression is LiteralExpressionSyntax literal)
				{
					constantValues.Add(literal.Token.Value);
				}
				else
				{
					values = null;
					return false;
				}
			}

			values = constantValues;
			return true;
		}

		values = null;
		return false;
	}

	protected bool TryGetSyntaxes(SyntaxNode node, [NotNullWhen(true)] out IList<ExpressionSyntax>? values)
	{
		if (node is CollectionExpressionSyntax collectionExpression)
		{
			values = collectionExpression.Elements
				.OfType<ExpressionElementSyntax>()
				.Select(s => s.Expression)
				.ToList();

			return values.Count == collectionExpression.Elements.Count;
		}

		values = null;
		return false;
	}
	
	protected bool TryCastToType(MetadataLoader loader, IEnumerable<object?> values, ITypeSymbol type, [NotNullWhen(true)] out object? result)
	{
		if (loader.TryGetType(type, out var elementType))
		{
			var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!
				.MakeGenericMethod(elementType);

			result = castMethod.Invoke(null, [ values ]);
			return true;
		}
		
		result = null;
		return false;
	}

	protected bool TryExecutePredicates(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		try
		{
			if (context.OriginalParameters.Count == context.Method.Parameters.Length
			    && TryGetValues(context.Visit(source) ?? source, out var values)
			    && context.Loader.TryGetMethodByMethod(context.Method, out var method)
			    && TryCastToType(context.Loader, values, context.Method.TypeArguments[0], out var castedValues))
			{
				var predicates = context.OriginalParameters
					.WhereSelect<ExpressionSyntax, LambdaExpression?>((x, out result) =>
					{
						if (TryGetLambda(x, out var originalLambda))
						{
							result = context.GetLambda(originalLambda);
							return result is not null;
						}

						result = null;
						return false;
					})
					.ToArray();

				if (predicates.Length == context.Method.Parameters.Length)
				{
					if (SyntaxHelpers.TryGetLiteral(method.Invoke(null, [ castedValues, ..predicates ]), out var tempResult))
					{
						result = tempResult;
						return true;
					}
				}
			}
		}
		catch (Exception e)
		{
		}
		
		result = null;
		return false;
	}

	private class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (node.Identifier.Text == identifier)
			{
				return replacement;
			}

			return base.VisitIdentifierName(node);
		}
	}
}