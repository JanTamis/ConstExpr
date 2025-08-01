using ConstExpr.SourceGenerator.Attributes;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA004")]
[DiagnosticTitle("Exception during evaluation")]
[DiagnosticMessageFormat("Unable to evaluate: {0}")]
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
			// ReportDiagnostic(context, operation.Syntax.GetLocation(), operation.Syntax.ToString());
		}, token);

		if (TryGetOperation<IMethodBodyOperation>(context.Compilation, symbol, out var operation))
		{
			visitor.VisitBlock(operation.BlockBody, variables);
		}
	}
}