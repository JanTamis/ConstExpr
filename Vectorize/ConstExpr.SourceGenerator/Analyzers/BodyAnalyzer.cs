using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ConstExpr.SourceGenerator.Attributes;
using ConstExpr.SourceGenerator.Visitors;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA004")]
[DiagnosticTitle("Parameter is not constant")]
[DiagnosticMessageFormat("'{0}' cannot be used in a ConstExpr method")]
[DiagnosticDescription("ConstExpr methods must be constant expressions")]
[DiagnosticCategory("Usage")]
public class BodyAnalyzer : BaseAnalyzer<InvocationExpressionSyntax, IMethodSymbol>
{
	protected override bool ValidateSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, CancellationToken token)
	{
		return !IsInConstExprBody(node);
	}

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol
			       .GetAttributes()
			       .Concat(symbol.ContainingType.GetAttributes())
			       .Any(IsConstExprAttribute)
		       && symbol.IsStatic;
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		var loader = MetadataLoader.GetLoader(context.SemanticModel.Compilation);
		var variables = ConstExprSourceGenerator.ProcessArguments(context.Compilation, loader, node, symbol, token);

		if (variables == null)
		{
			return;
		}

		var visitor = new ConstExprOperationVisitor(context.Compilation, loader, (operation, exception) =>
		{
			// ReportDiagnostic(context, operation.Syntax.GetLocation(), operation.Syntax);
		}, token);

		if (TryGetOperation<IMethodBodyOperation>(context.Compilation, symbol, out var operation))
		{
			visitor.VisitBlock(operation.BlockBody, variables);
		}
	}
}