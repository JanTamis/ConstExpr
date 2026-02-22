using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
	protected bool TryGetLambda([NotNullWhen(true)] ExpressionSyntax? parameter, [NotNullWhen(true)] out LambdaExpressionSyntax? lambda)
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

	protected bool IsLinqMethodChain(ExpressionSyntax? expression, [NotNullWhen(true)] out string? methodName, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		invocation = null;
		methodName = null;

		if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } inv)
		{
			return false;
		}

		invocation = inv;
		methodName = memberAccess.Name.Identifier.Text;
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
	protected SeparatedSyntaxList<ArgumentSyntax> GetMethodArguments(InvocationExpressionSyntax invocation)
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
	
	protected ElementAccessExpressionSyntax CreateElementAccess(ExpressionSyntax source, params IEnumerable<ExpressionSyntax> arguments)
	{
		return ElementAccessExpression(
			source,
			BracketedArgumentList(
				SeparatedList( arguments.Select(Argument))));
	}

	protected InvocationExpressionSyntax UpdateInvocation(FunctionOptimizerContext context, ExpressionSyntax source)
	{
		return UpdateInvocation(context, source, context.VisitedParameters);
	}

	protected InvocationExpressionSyntax UpdateInvocation(FunctionOptimizerContext context, ExpressionSyntax source, params IEnumerable<ExpressionSyntax> arguments)
	{
		if (context.Invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return context.Invocation.
				Update(memberAccess.WithExpression(context.Visit(source) ?? source), ArgumentList(SeparatedList(arguments.Select(Argument))));
		}
		
		throw new InvalidOperationException("Invocation expression must be a member access");
	}

	/// <summary>
	/// Creates a throw expression for a specific exception type with a message.
	/// </summary>
	/// <param name="message">The message to pass to the exception constructor</param>
	/// <returns>A ThrowExpressionSyntax that throws the specified exception with the message</returns>
	protected ThrowExpressionSyntax CreateThrowExpression<TException>(string message) where TException : Exception	
	{
		return ThrowExpression(
			ObjectCreationExpression(
					IdentifierName(typeof(TException).Name))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(
								LiteralExpression(
									SyntaxKind.StringLiteralExpression,
									Literal(message)))))));
	}

	protected ThrowExpressionSyntax CreateThrowExpression(Exception ex)
	{
		return ThrowExpression(
			ObjectCreationExpression(
					IdentifierName(ex.GetType().Name))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(
								LiteralExpression(
									SyntaxKind.StringLiteralExpression,
									Literal(ex.Message)))))));
	}
	
	protected ImplicitArrayCreationExpressionSyntax CreateImplicitArray(params IEnumerable<ExpressionSyntax> elements)
	{
		return ImplicitArrayCreationExpression(
			InitializerExpression(SyntaxKind.ArrayInitializerExpression,
				SeparatedList(elements)));
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
		return InvocationExpression(
			CreateMemberAccess(source, methodName),
			ArgumentList());
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
	protected bool IsInvokedOnList(FunctionOptimizerContext context, ExpressionSyntax? expression)
	{
		if (!context.Model.TryGetTypeSymbol(expression, out var type)
		    || type is not INamedTypeSymbol namedType)
		{
			return false;
		}

		if (expression is IdentifierNameSyntax identifier
		    && context.Variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			namedType = variable.Type as INamedTypeSymbol;
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
	protected bool IsInvokedOnArray(FunctionOptimizerContext context, [NotNullWhen(true)] ExpressionSyntax? expression)
	{
		if (expression is null)
		{
			return false;
		}

		if (expression is IdentifierNameSyntax identifier
		    && context.Variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			return variable.Type is IArrayTypeSymbol;
		}

		return context.Model.TryGetTypeSymbol(expression, out var type) && type is IArrayTypeSymbol;
	}

	protected bool IsSpecialType(FunctionOptimizerContext context, ExpressionSyntax? expression, params HashSet<SpecialType> specialTypes)
	{
		if (expression is IdentifierNameSyntax identifier
		    && context.Variables.TryGetValue(identifier.Identifier.Text, out var variable))
		{
			return variable.Type.AllInterfaces.Any(s => specialTypes.Contains(s.SpecialType));
		}
		
		return context.Model.TryGetTypeSymbol(expression, out var type) && type.AllInterfaces.Any(s => specialTypes.Contains(s.SpecialType));
	}

	protected bool IsCollectionType(FunctionOptimizerContext context, ExpressionSyntax? expression)
	{
		return IsSpecialType(context, expression,
			       SpecialType.System_Collections_Generic_IList_T,
			       SpecialType.System_Collections_Generic_IReadOnlyList_T,
			       SpecialType.System_Collections_Generic_ICollection_T,
			       SpecialType.System_Collections_Generic_IReadOnlyCollection_T)
		       || IsInvokedOnList(context, expression);
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
		
		if (node is ArrayCreationExpressionSyntax arrayCreation)
		{
			if (arrayCreation.Initializer is null)
			{
				values = null;
				return false;
			}

			var constantValues = new List<object?>();

			foreach (var expression in arrayCreation.Initializer.Expressions)
			{
				if (expression is LiteralExpressionSyntax literal)
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
		
		if (node is ImplicitArrayCreationExpressionSyntax implicitArrayCreation)
		{
			var constantValues = new List<object?>();

			foreach (var expression in implicitArrayCreation.Initializer.Expressions)
			{
				if (expression is LiteralExpressionSyntax literal)
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

		if (node is ArrayCreationExpressionSyntax arrayCreation)
		{
			if (arrayCreation.Initializer is null)
			{
				values = null;
				return false;
			}

			values = arrayCreation.Initializer.Expressions.ToList();
			return true;
		}

		if (node is ImplicitArrayCreationExpressionSyntax implicitArrayCreation)
		{
			values = implicitArrayCreation.Initializer.Expressions.ToList();
			return true;
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
			    && context.Method.ReceiverType is INamedTypeSymbol receiverType)
			{
				var items = (object) values;

				if (receiverType.TypeArguments.Length > 0
				    && TryCastToType(context.Loader, values, receiverType.TypeArguments[0], out var castedValues))
				{
					items = castedValues;
				}

				var parameters = new List<object?>();

				for (int i = 0; i < context.OriginalParameters.Count; i++)
				{
					if (TryGetLambda(context.OriginalParameters[i], out var originalLambda))
					{
						parameters.Add(context.GetLambda(originalLambda).Compile());
					}

					if (context.VisitedParameters[i] is LiteralExpressionSyntax literal)
					{
						parameters.Add(literal.Token.Value);
					}
				}

				if (parameters.Count == context.Method.Parameters.Length)
				{
					if (SyntaxHelpers.TryGetLiteral(method.Invoke(null, [ items, ..parameters ]), out var tempResult))
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

	protected bool TryExecutePredicates(FunctionOptimizerContext context, ExpressionSyntax source, IList<ExpressionSyntax> parameters, [NotNullWhen(true)] out SyntaxNode? result)
	{
		try
		{
			if (parameters.Count == context.Method.Parameters.Length
			    && TryGetValues(context.Visit(source) ?? source, out var values)
			    && context.Loader.TryGetMethodByMethod(context.Method, out var method)
			    && context.Method.ReceiverType is INamedTypeSymbol receiverType)
			{
				var items = (object) values;

				if (receiverType.TypeArguments.Length > 0
				    && TryCastToType(context.Loader, values, receiverType.TypeArguments[0], out var castedValues))
				{
					items = castedValues;
				}

				var resultParameters = new List<object?>();

				for (int i = 0; i < parameters.Count; i++)
				{
					if (TryGetLambda(parameters[i], out var originalLambda))
					{
						resultParameters.Add(context.GetLambda(originalLambda).Compile());
					}

					if (parameters[i] is LiteralExpressionSyntax literal)
					{
						resultParameters.Add(literal.Token.Value);
					}
				}

				if (resultParameters.Count == context.Method.Parameters.Length)
				{
					if (SyntaxHelpers.TryGetLiteral(method.Invoke(null, [ items, ..resultParameters ]), out var tempResult))
					{
						result = tempResult;
						return true;
					}
				}
			}
		}
		catch (TargetInvocationException e) when (e.InnerException != null)
		{
			result = CreateThrowExpression(e.InnerException);
			return true;
		}

		result = null;
		return false;
	}

	protected SyntaxNode TryOptimizeByOptimizer<TOptimizer>(FunctionOptimizerContext context, InvocationExpressionSyntax invocation) where TOptimizer : BaseLinqFunctionOptimizer, new()
	{
		var methodName = typeof(TOptimizer).Name.Substring(0, typeof(TOptimizer).Name.Length - "FunctionOptimizer".Length);
		
		var methodSymbol = context.Model.Compilation
			.GetTypeByMetadataName(typeof(Enumerable).FullName)
			.GetMembers(methodName)
			.OfType<IMethodSymbol>()
			.Select(s => s.Construct(context.Method.TypeArguments.ToArray()))
			.First(f => f.Parameters.Length == invocation.ArgumentList.Arguments.Count + 1); // +1 for the source parameter
		
		var parameters = invocation.ArgumentList.Arguments.Select(a => a.Expression).ToArray();
		var visitedParameters = parameters.Select(p => context.Visit(p) ?? p).ToArray();

		context = context.WithInvocationAndMethod(invocation, methodSymbol);
		context.OriginalParameters = parameters;
		context.VisitedParameters = visitedParameters;

		var firstOptimizer = new TOptimizer();

		if (!firstOptimizer.TryOptimize(context, out var result))
		{
			result = invocation;
		}
		
		return result;
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