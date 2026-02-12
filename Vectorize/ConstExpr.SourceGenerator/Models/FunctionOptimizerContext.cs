using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Models;

public sealed class FunctionOptimizerContext
{
	public FunctionOptimizerContext(
		SemanticModel model,
		MetadataLoader loader,
		IMethodSymbol method,
		InvocationExpressionSyntax invocation,
		IList<ExpressionSyntax> visitedParameters,
		IList<ExpressionSyntax> originalParameters,
		Func<SyntaxNode, ExpressionSyntax?> visit,
		Func<LambdaExpressionSyntax, LambdaExpression?> getLambda,
		IDictionary<SyntaxNode, bool> additionalMethods)
	{
		Model = model;
		Loader = loader;
		Method = method;
		Invocation = invocation;
		VisitedParameters = visitedParameters;
		OriginalParameters = originalParameters;
		Visit = visit;
		GetLambda = getLambda;
		AdditionalMethods = additionalMethods;
	}

	public SemanticModel Model { get; }
	public MetadataLoader Loader { get; }
	public IMethodSymbol Method { get; }
	public InvocationExpressionSyntax Invocation { get; }
	public IList<ExpressionSyntax> VisitedParameters { get; }
	public IList<ExpressionSyntax> OriginalParameters { get; }
	public Func<SyntaxNode, ExpressionSyntax?> Visit { get; }
	public Func<LambdaExpressionSyntax, LambdaExpression?> GetLambda { get; }
	public IDictionary<SyntaxNode, bool> AdditionalMethods { get; }
}

