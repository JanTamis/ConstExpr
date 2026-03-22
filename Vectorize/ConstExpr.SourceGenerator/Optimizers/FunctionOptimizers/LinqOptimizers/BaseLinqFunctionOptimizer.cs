using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
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

	protected static readonly HashSet<string> MaterializingMethods =
	[
		nameof(Enumerable.ToArray),
		nameof(Enumerable.ToList),
		nameof(Enumerable.AsEnumerable),
		"ToHashSet",
	];

	protected static readonly HashSet<string> OrderingOperations =
	[
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
		"Shuffle",
	];

	protected static readonly HashSet<string> SetBasedOperations =
	[
		nameof(Enumerable.Count),
		nameof(Enumerable.Any),
		nameof(Enumerable.Contains),
		nameof(Enumerable.LongCount),
		nameof(Enumerable.First),
		nameof(Enumerable.FirstOrDefault),
	];

	/// <summary>
	/// Validates if the given method is a valid LINQ Enumerable method matching this optimizer's criteria.
	/// </summary>
	protected bool IsValidLinqMethod(FunctionOptimizerContext context)
	{
		return context.Method.Name == Name
		       && ParameterCounts.Contains(context.OriginalParameters.Count);
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
	/// Creates a new method invocation on the given source expression, annotated with the
	/// resolved LINQ method symbol from the compilation so that the LinqUnroller can process it.
	/// </summary>
	protected InvocationExpressionSyntax CreateAnnotatedInvocation(FunctionOptimizerContext context, ExpressionSyntax source, string methodName, params IEnumerable<ExpressionSyntax> arguments)
	{
		var argList = arguments as ICollection<ExpressionSyntax> ?? arguments.ToArray();
		var invocation = CreateInvocation(source, methodName, argList);
		return AnnotateLinqInvocation(context, invocation, methodName, argList.Count);
	}

	/// <summary>
	/// Creates a method call with no arguments on the given source expression, annotated with the
	/// resolved LINQ method symbol from the compilation so that the LinqUnroller can process it.
	/// </summary>
	protected InvocationExpressionSyntax CreateAnnotatedSimpleInvocation(FunctionOptimizerContext context, ExpressionSyntax source, string methodName)
	{
		var invocation = CreateSimpleInvocation(source, methodName);
		return AnnotateLinqInvocation(context, invocation, methodName, 0);
	}

	/// <summary>
	/// Resolves the LINQ extension method symbol from the compilation and annotates the invocation node.
	/// </summary>
	private static InvocationExpressionSyntax AnnotateLinqInvocation(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, string methodName, int lambdaArgCount)
	{
		var enumerable = context.Model.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");

		if (enumerable is not null)
		{
			var method = enumerable.GetMembers(methodName)
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.Length == lambdaArgCount + 1);

			if (method is not null)
			{
				return invocation.WithMethodSymbolAnnotation(method);
			}
		}

		return invocation;
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
				SeparatedList(arguments.Select(Argument))));
	}

	protected InvocationExpressionSyntax UpdateInvocation(FunctionOptimizerContext context, ExpressionSyntax source)
	{
		return UpdateInvocation(context, source, context.VisitedParameters);
	}

	protected InvocationExpressionSyntax UpdateInvocation(FunctionOptimizerContext context, ExpressionSyntax source, params IEnumerable<ExpressionSyntax> arguments)
	{
		if (context.Invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return context.Invocation
				.Update(memberAccess.WithExpression(context.Visit(source) ?? source), ArgumentList(SeparatedList(arguments.Select(Argument))))
				.WithMethodSymbolAnnotation(context.Method);
		}

		throw new InvalidOperationException("Invocation expression must be a member access");
	}

	/// <summary>
	/// Creates a throw expression for a specific exception type with a message.
	/// </summary>
	/// <param name="message">The message to pass to the exception constructor</param>
	/// <returns>A ThrowExpressionSyntax that throws the specified exception with the message</returns>
	protected ThrowExpressionSyntax CreateThrowExpression<TException>(string message = "") where TException : Exception
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

	protected CollectionExpressionSyntax CreateCollection(params IEnumerable<ExpressionSyntax> elements)
	{
		return CollectionExpression(SeparatedList<CollectionElementSyntax>(elements
			.Select(ExpressionElement)));
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
	
	protected bool TryGetSimpleLambdaParameter(LambdaExpressionSyntax lambda, [NotNullWhen(true)] out ParameterSyntax? parameterName)
	{
		parameterName = null;

		if (lambda is SimpleLambdaExpressionSyntax { Parameter: var paramName })
		{
			parameterName = paramName;
			return true;
		}

		return false;
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
	/// Checks if a lambda expression is a type-check pattern (e.g., x => x is SomeType).
	/// If true, extracts the type being checked against, enabling replacement with OfType&lt;T&gt;().
	/// </summary>
	protected bool IsTypeCheckLambda(LambdaExpressionSyntax lambda, [NotNullWhen(true)] out TypeSyntax? typeCheckType)
	{
		typeCheckType = null;

		var (paramName, body) = lambda switch
		{
			SimpleLambdaExpressionSyntax { Parameter.Identifier.Text: var p, Body: ExpressionSyntax b } => (p, b),
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1, Body: ExpressionSyntax b } pl
				=> (pl.ParameterList.Parameters[0].Identifier.Text, b),
			_ => (null, (ExpressionSyntax?)null)
		};

		if (paramName is null || body is null)
		{
			return false;
		}

		if (body is not BinaryExpressionSyntax
		    {
			    RawKind: (int)SyntaxKind.IsExpression,
			    Left: IdentifierNameSyntax { Identifier.Text: var identName },
			    Right: TypeSyntax type
		    } || identName != paramName)
		{
			return false;
		}

		typeCheckType = type;
		return true;
	}

	/// <summary>
	/// Checks whether replacing <c>Where(x => x is T)</c> with <c>OfType&lt;T&gt;()</c> is semantically safe.
	/// The optimization is only valid when the conversion from the element type to <paramref name="typeCheckType"/>
	/// is a reference, identity, or boxing conversion — never a numeric or user-defined implicit conversion
	/// (e.g. <c>int</c> to <c>double</c> would not be a valid replacement).
	/// </summary>
	protected bool IsTypeCompatibleForOfType(FunctionOptimizerContext context, TypeSyntax typeCheckType, ITypeSymbol elementType)
	{
		// Resolve the TypeSyntax to an ITypeSymbol via the semantic model
		if (context.Model.GetSymbolInfo(typeCheckType).Symbol is not ITypeSymbol targetTypeSymbol)
		{
			return false;
		}

		// Classify the conversion from elementType -> targetType
		var conversion = context.Model.Compilation.ClassifyConversion(elementType, targetTypeSymbol);

		// Only allow identity, reference, or boxing conversions.
		// Numeric/implicit user-defined conversions (e.g. int -> double) must be rejected because
		// OfType<T>() uses 'is' semantics (runtime type check), not implicit cast semantics.
		return conversion.IsIdentity 
		       || conversion.IsReference 
		       || conversion.IsBoxing;
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
			return variable.Type.AllInterfaces.Any(s => specialTypes.Contains(s.SpecialType) || specialTypes.Contains(s.OriginalDefinition.SpecialType));
		}

		return context.Model.TryGetTypeSymbol(expression, out var type)
		       && type.AllInterfaces.Any(s => specialTypes.Contains(s.SpecialType) || specialTypes.Contains(s.OriginalDefinition.SpecialType));
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

	protected bool IsEnumerableType(INamedTypeSymbol? type, ITypeSymbol elementType)
	{
		return type?.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
		       && SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], elementType);
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

	/// <summary>
	/// Combines two lambdas by composing them: outer(inner(x)) => x => outer(inner(x)).
	/// The outer lambda's parameter is replaced by the inner lambda's body.
	/// </summary>
	protected LambdaExpressionSyntax CombineLambdas(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// Replace the outer lambda's parameter with the inner lambda's body
		var combinedBody = ReplaceIdentifier(outerBody, outerParam, innerBody);

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
		// Wrap replacement in parentheses if it's a binary or conditional expression to preserve precedence.
		// ConditionalExpressionSyntax (?:) has very low precedence and must be parenthesized when used
		// as an operand in a binary expression (e.g., (x.Length > 0 ? x[0] : 0) << 1).
		var wrappedReplacement = replacement is BinaryExpressionSyntax or ConditionalExpressionSyntax
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

	protected bool TryGetSyntaxes([NotNullWhen(true)] SyntaxNode? node, [NotNullWhen(true)] out IList<ExpressionSyntax>? values)
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

	protected bool TryCastToType(MetadataLoader loader, object values, ITypeSymbol type, [NotNullWhen(true)] out object? result)
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

	protected bool TryChangeToArray(MetadataLoader loader, object values, ITypeSymbol type, [NotNullWhen(true)] out object? result)
	{
		if (loader.TryGetType(type, out var elementType))
		{
			var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!
				.MakeGenericMethod(elementType);

			result = castMethod.Invoke(null, [ values ]);
			return true;
		}

		result = null;
		return false;
	}

	protected bool TryChangeToList(MetadataLoader loader, object values, ITypeSymbol type, [NotNullWhen(true)] out object? result)
	{
		if (loader.TryGetType(type, out var elementType))
		{
			var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!
				.MakeGenericMethod(elementType);

			result = castMethod.Invoke(null, [ values ]);
			return true;
		}

		result = values;
		return false;
	}

	protected bool TryExecutePredicates(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result, out ExpressionSyntax visitedSource)
	{
		visitedSource = context.Visit(source) ?? source;

		try
		{
			if (context.OriginalParameters.Count <= context.Method.Parameters.Length
			    && context.Method.ReceiverType is INamedTypeSymbol receiverType
			    && TryGetLiteralValue(visitedSource, context, receiverType, out var values)
			    && context.Loader.TryGetMethodByMethod(context.Method, out var method))
			{
				var parameters = new List<object?>();

				for (var i = 0; i < context.OriginalParameters.Count; i++)
				{
					if (TryGetLiteralValue(context.OriginalParameters[i], context, context.Method.Parameters[i].Type, out var value)
					    || TryGetLiteralValue(context.VisitedParameters[i], context, context.Method.Parameters[i].Type, out value))
					{
						parameters.Add(value);
					}
				}

				// Fill in default values for parameters not explicitly provided in the call.
				for (var i = context.OriginalParameters.Count; i < context.Method.Parameters.Length; i++)
				{
					var param = context.Method.Parameters[i];
					
					if (param.HasExplicitDefaultValue)
					{
						parameters.Add(param.ExplicitDefaultValue);
					}
				}

				if (parameters.Count == context.Method.Parameters.Length)
				{
					if (method.IsStatic
					    && TryGetLiteral(method.Invoke(null, [ values, ..parameters ]), out var tempResult)
					    || TryGetLiteral(method.Invoke(values, [ ..parameters ]), out tempResult))
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

	protected bool TryExecutePredicates(FunctionOptimizerContext context, ExpressionSyntax source, IList<ExpressionSyntax> parameters, [NotNullWhen(true)] out SyntaxNode? result)
	{
		try
		{
			if (context.OriginalParameters.Count <= context.Method.Parameters.Length
			    && context.Method.ReceiverType is INamedTypeSymbol receiverType
			    && TryGetLiteralValue(context.Visit(source) ?? source, context, receiverType, out var values)
			    && context.Loader.TryGetMethodByMethod(context.Method, out var method))
			{
				var newParameters = parameters
					.WhereSelect<ExpressionSyntax, object?>((s, out result) => TryGetLiteralValue(s, context, null, out result))
					.ToList();

				// Fill in default values for parameters not explicitly provided in the call.
				for (var i = parameters.Count; i < context.Method.Parameters.Length; i++)
				{
					var param = context.Method.Parameters[i];
					
					if (param.HasExplicitDefaultValue)
					{
						newParameters.Add(param.ExplicitDefaultValue);
					}
				}

				if (newParameters.Count == context.Method.Parameters.Length)
				{
					if (context.Method.ReceiverType is not null
					    && TryGetLiteral(method.Invoke(null, [ values, ..newParameters ]), out var tempResult)
					    || TryGetLiteral(method.Invoke(values, [ ..newParameters ]), out tempResult))
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
		return TryOptimizeByOptimizer<TOptimizer>(context, invocation, _ => true, context.Method.TypeArguments.ToArray());
	}

	protected SyntaxNode TryOptimizeByOptimizer<TOptimizer>(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, params ITypeSymbol[] typeArguments) where TOptimizer : BaseLinqFunctionOptimizer, new()
	{
		return TryOptimizeByOptimizer<TOptimizer>(context, invocation, _ => true, typeArguments);
	}

	protected SyntaxNode TryOptimizeByOptimizer<TOptimizer>(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, Func<IMethodSymbol, bool> selector, params ITypeSymbol[] typeArguments) where TOptimizer : BaseLinqFunctionOptimizer, new()
	{
		try
		{
			var methodName = typeof(TOptimizer).Name.Substring(0, typeof(TOptimizer).Name.Length - "FunctionOptimizer".Length);

			var parameters = invocation.ArgumentList.Arguments
				.Select(a => a.Expression)
				.ToArray();

			var visitedParameters = parameters
				.Select(p => context.Visit(p) ?? p)
				.ToArray();

			var methodSymbol = context.Model.Compilation
				.GetTypeByMetadataName(typeof(Enumerable).FullName)
				.GetMembers(methodName)
				.OfType<IMethodSymbol>()
				.Where(f => f.Parameters.Length == invocation.ArgumentList.Arguments.Count + 1) // +1 for the source parameter
				.Select(s => s.TypeArguments.Length == 0 ? s : s.Construct(typeArguments))
				.First(selector);

			context = context.WithInvocationAndMethod(invocation, methodSymbol);
			context.OriginalParameters = parameters;
			context.VisitedParameters = visitedParameters;

			var optimizer = new TOptimizer();

			if (!optimizer.TryOptimize(context, out var result))
			{
				result = invocation.WithMethodSymbolAnnotation(methodSymbol);
			}

			return result;
		}
		catch (Exception e)
		{
			return invocation;
		}
	}
	
	/// <summary>
	/// Optimizes a pairwise Min/Max scalar comparison by delegating to the corresponding Math optimizer.
	/// Used when a LINQ Min/Max over a Concat source is reduced to a two-argument scalar call so that
	/// Math-level optimizations (idempotency, clamp pattern, constant folding) are automatically applied.
	/// </summary>
	protected ExpressionSyntax OptimizeAsMathPairwise<TOptimizer>(
		FunctionOptimizerContext context,
		ExpressionSyntax left,
		ExpressionSyntax right)
		where TOptimizer : BaseMathFunctionOptimizer, new()
	{
		var optimizer = new TOptimizer();
		var returnType = context.Method.ReturnType;

		// Synthetic fallback invocation: ReturnType.Max/Min(left, right)
		var syntheticInvocation = CreateInvocation(returnType, optimizer.Name, left, right);

		try
		{
			// Prefer a method from System.Math so IsValidMathMethod passes
			var mathType = context.Model.Compilation.GetTypeByMetadataName("System.Math");
			IMethodSymbol? mathMethod = mathType
				?.GetMembers(optimizer.Name)
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.Length == 2
				                     && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, returnType));

			// Fall back to a static method on the numeric type itself (e.g., int.Max in .NET 7+)
			mathMethod ??= returnType
				?.GetMembers(optimizer.Name)
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.Length == 2);

			if (mathMethod is null)
			{
				return syntheticInvocation;
			}

			var mathContext = context.WithInvocationAndMethod(syntheticInvocation, mathMethod);
			mathContext.VisitedParameters = [left, right];
			mathContext.OriginalParameters = [left, right];

			return optimizer.TryOptimize(mathContext, out var result) && result is ExpressionSyntax expr
				? expr
				: syntheticInvocation;
		}
		catch
		{
			return syntheticInvocation;
		}
	}

	protected bool TryGetElementType(FunctionOptimizerContext context, [NotNullWhen(true)] out ITypeSymbol? elementType)
	{
		if (context.Method.ReceiverType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T, TypeArguments.Length: 1 } receiverType)
		{
			elementType = receiverType.TypeArguments[0];
			return true;
		}
		
		elementType = null;
		return false;
	}
	
	protected bool TryGetEnumerableMethod(FunctionOptimizerContext context, string methodName, int parameterCount, [NotNullWhen(true)] out IMethodSymbol? methodSymbol)
	{
		methodSymbol = context.Model.Compilation
			.GetTypeByMetadataName(typeof(Enumerable).FullName)
			.GetMembers(methodName)
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == parameterCount + 1); // +1 for the source parameter

		return methodSymbol is not null;
	}

	protected ExpressionSyntax InvertSyntax(ExpressionSyntax node)
	{
		// invert binary expressions with logical operators
		if (node is BinaryExpressionSyntax binary)
		{
			return binary.Kind() switch
			{
				SyntaxKind.LogicalAndExpression => BinaryExpression(SyntaxKind.LogicalOrExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.LogicalOrExpression => BinaryExpression(SyntaxKind.LogicalAndExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
				SyntaxKind.EqualsExpression => BinaryExpression(SyntaxKind.NotEqualsExpression, binary.Left, binary.Right),
				SyntaxKind.NotEqualsExpression => BinaryExpression(SyntaxKind.EqualsExpression, binary.Left, binary.Right),
				SyntaxKind.GreaterThanExpression => BinaryExpression(SyntaxKind.LessThanOrEqualExpression, binary.Left, binary.Right),
				SyntaxKind.GreaterThanOrEqualExpression => BinaryExpression(SyntaxKind.LessThanExpression, binary.Left, binary.Right),
				SyntaxKind.LessThanExpression => BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, binary.Left, binary.Right),
				SyntaxKind.LessThanOrEqualExpression => BinaryExpression(SyntaxKind.GreaterThanExpression, binary.Left, binary.Right),
				_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(node))
			};
		}

		// handle 'x is T' (pattern form) and 'x is not T'
		if (node is IsPatternExpressionSyntax isPattern)
		{
			// x is not T  →  x is T  (strip the negation)
			if (isPattern.Pattern.Kind() == SyntaxKind.NotPattern && isPattern.Pattern is UnaryPatternSyntax negated)
				return IsPatternExpression(isPattern.Expression, negated.Pattern);

			// x is T  →  x is not T  (add negation)
			return IsPatternExpression(isPattern.Expression, UnaryPattern(Token(SyntaxKind.NotKeyword), isPattern.Pattern));
		}

		return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(node));
	}

	/// <summary>
	/// Parses a struct declaration from a source code string.
	/// </summary>
	protected static TType ParseTypeFromString<TType>(string structString) where TType : TypeDeclarationSyntax
	{
		var wrappedCode = $$"""
			using System;
			using System.Collections;
			using System.Collections.Generic;
			using System.Linq;

			public class TempClass
			{
			{{structString}}
			}
			""";

		var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

		return syntaxTree.GetRoot()
			.DescendantNodes()
			.Select(s => s.NormalizeWhitespace("\t"))
			.OfType<TType>()
			.First();
	}

	protected ExpressionSyntax OptimizeComparison(FunctionOptimizerContext context, SyntaxKind kind, ExpressionSyntax left, ExpressionSyntax right, ITypeSymbol type)
	{
		var boolType = context.Model.Compilation.CreateBoolean();
		
		return context.OptimizeBinaryExpression(BinaryExpression(kind, left, right), type, type, boolType);
	}

	protected ExpressionSyntax OptimizeArithmetic(FunctionOptimizerContext context, SyntaxKind kind, ExpressionSyntax left, ExpressionSyntax right, ITypeSymbol type)
	{
		return context.OptimizeBinaryExpression(BinaryExpression(kind, left, right), type, type, type);
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