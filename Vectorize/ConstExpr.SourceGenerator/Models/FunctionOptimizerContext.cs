using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Models;

public sealed class FunctionOptimizerContext(
	SemanticModel model,
	MetadataLoader loader,
	IMethodSymbol method,
	InvocationExpressionSyntax invocation,
	IList<ExpressionSyntax> visitedParameters,
	IList<ExpressionSyntax> originalParameters,
	Func<SyntaxNode, ExpressionSyntax?> visit,
	Func<SyntaxNode, StatementSyntax?> visitStatement,
	Func<LambdaExpressionSyntax, LambdaExpression?> getLambda,
	Func<BinaryExpressionSyntax, ITypeSymbol, ITypeSymbol, ITypeSymbol, ExpressionSyntax> optimizeBinaryExpression,
	IDictionary<SyntaxNode, bool> additionalMethods,
	IDictionary<string, VariableItem> variables,
	ISet<string> usings,
	FastMathFlags fastMathFlags)
{
	public SemanticModel Model { get; } = model;
	public MetadataLoader Loader { get; } = loader;
	public IMethodSymbol Method { get; } = method;
	public InvocationExpressionSyntax Invocation { get; } = invocation;
	public IList<ExpressionSyntax> VisitedParameters { get; set; } = visitedParameters;
	public IList<ExpressionSyntax> OriginalParameters { get; set; } = originalParameters;
	public Func<SyntaxNode, ExpressionSyntax?> Visit { get; } = visit;

	public Func<SyntaxNode, StatementSyntax?> VisitStatement { get; } = visitStatement;
	
	public Func<LambdaExpressionSyntax, LambdaExpression?> GetLambda { get; } = getLambda;
	public Func<BinaryExpressionSyntax, ITypeSymbol, ITypeSymbol, ITypeSymbol, ExpressionSyntax> OptimizeBinaryExpression { get; set; } = optimizeBinaryExpression;
	public IDictionary<SyntaxNode, bool> AdditionalMethods { get; } = additionalMethods;
	public IDictionary<string, VariableItem> Variables { get; } = variables;
	public ISet<string> Usings { get; } = usings;
	public FastMathFlags FastMathFlags { get; set; } = fastMathFlags;

	public FunctionOptimizerContext WithInvocationAndMethod(InvocationExpressionSyntax invocation, IMethodSymbol method)
	{
		return new FunctionOptimizerContext(Model, Loader, method, invocation, VisitedParameters, OriginalParameters, Visit, VisitStatement, GetLambda, OptimizeBinaryExpression, AdditionalMethods, Variables, Usings, FastMathFlags);
	}
}