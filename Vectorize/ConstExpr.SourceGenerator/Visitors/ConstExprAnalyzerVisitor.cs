using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConstExpr.SourceGenerator.Analyzers;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Visitors;

public class ConstExprAnalyzerVisitor<TNode, TSymbol>(BaseAnalyzer<TNode, TSymbol> analyzer, SyntaxNodeAnalysisContext context, Dictionary<string, Type?> variables) : OperationVisitor
	where TNode : SyntaxNode
	where TSymbol : ISymbol
{
	public override void DefaultVisit(IOperation operation)
	{
		foreach (var currentOperation in operation.ChildOperations)
		{
			Visit(currentOperation);
		}
	}

	public override void VisitBlock(IBlockOperation operation)
	{
		foreach (var statement in operation.Operations)
		{
			Visit(statement);
		}
	}
	
	public override void VisitVariableDeclaration(IVariableDeclarationOperation operation)
	{
		foreach (var variable in operation.Declarators)
		{
			variables.Add(variable.Symbol.Name, SyntaxHelpers.GetTypeByType(context.Compilation, variable.Symbol.Type));
		}
	}

	public override void VisitPropertyReference(IPropertyReferenceOperation operation)
	{
		var type = SyntaxHelpers.GetTypeByType(context.Compilation, operation.Property.ContainingType);

		var propertyInfo = type
			.GetProperties()
			.FirstOrDefault(f => f.Name == operation.Property.Name && f.GetMethod.IsStatic == operation.Property.IsStatic);

		if (propertyInfo == null)
		{
			ReportDiagnostic(operation);
		}
	}

	public override void VisitInvocation(IInvocationOperation operation)
	{
		if (operation.TargetMethod.GetAttributes().Any(SyntaxHelpers.IsConstExprAttribute) && SyntaxHelpers.TryGetOperation<IMethodBodyOperation>(context.Compilation, operation.TargetMethod, out _))
		{
			return;
		}
		
		try
		{
			var type = SyntaxHelpers.GetTypeByType(context.Compilation, operation.TargetMethod.ContainingType);

			var arguments = operation.Arguments
				.Select(s => SyntaxHelpers.GetTypeByType(context.Compilation, s.Parameter.Type))
				.ToArray();

			foreach (var methodInfo in type.GetMethods())
			{
				if (methodInfo.Name == operation.TargetMethod.Name && methodInfo.GetParameters().Select(s => s.ParameterType).SequenceEqual(arguments))
				{
					return;
				}
			}
		}
		catch (Exception ex) 
		{
			ReportDiagnostic(operation);
		}
	}

	private void ReportDiagnostic(IOperation operation)
	{
		analyzer.ReportDiagnostic(context, operation.Syntax.GetLocation(), operation.Syntax);
	}
}