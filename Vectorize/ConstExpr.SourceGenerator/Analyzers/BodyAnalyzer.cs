using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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
		return !IsInConstExprBody(context.Compilation, node);
	}

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol
						 .GetAttributes()
						 .Concat(symbol.ContainingType.GetAttributes())
						 .Any(IsAttribute<ConstEvalAttribute>)
					 && symbol.IsStatic;
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		// var loader = MetadataLoader.GetLoader(context.SemanticModel.Compilation);
		//
		// var visitor = new ConstExprOperationVisitor(context.SemanticModel.Compilation, loader, (operation, ex) =>
		// {
		// 	ReportDiagnostic(context, operation.Syntax.GetLocation(), operation.Syntax.ToString());
		// }, token);
		//
		// var variables = ConstExprSourceGenerator.ProcessArguments(visitor, context.SemanticModel.Compilation, node, loader, token);
		//
		// if (variables == null)
		// {
		// 	return;
		// }
		//
		// if (TryGetOperation<IMethodBodyOperation>(context.Compilation, symbol, out var operation))
		// {
		// 	visitor.VisitBlock(operation.BlockBody, variables);
		// }
	}
}