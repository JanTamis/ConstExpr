using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Attributes;
using ConstExpr.SourceGenerator.Visitors;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA004")]
[DiagnosticTitle("Parameter is not constant")]
[DiagnosticMessageFormat("'{0}' cannot be used in a ConstExpr method")]
[DiagnosticDescription("Parameters marked with the ConstExpr attribute should be constant")]
[DiagnosticCategory("Usage")]
public class UsageAnalyzer : BaseAnalyzer<InvocationExpressionSyntax, IMethodSymbol>
{
	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		var variables = new Dictionary<string, Type?>();
		var visitor = new ConstExprAnalyzerVisitor<InvocationExpressionSyntax, IMethodSymbol>(this, context, variables);

		foreach (var parameter in symbol.Parameters)
		{
			variables.Add(parameter.Name, GetTypeByType(context.Compilation, parameter.Type));
		}

		if (TryGetOperation<IMethodBodyOperation>(context.Compilation, symbol, out var operation))
		{
			visitor.VisitBlock(operation.BlockBody);
		}
	}
}