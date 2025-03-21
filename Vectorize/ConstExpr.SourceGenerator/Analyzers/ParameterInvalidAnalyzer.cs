using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ConstExpr.SourceGenerator.Attributes;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA003")]
[DiagnosticTitle("Parameter is not valid")]
[DiagnosticMessageFormat("'{0}' should be a number of a IReadOnlyList of numbers")]
[DiagnosticDescription("parameter should be numeric or a IReadOnlyList of number")]
[DiagnosticCategory("Usage")]
public class ParameterInvalidAnalyzer : BaseAnalyzer<MethodDeclarationSyntax, IMethodSymbol>
{
	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		// for (var i = 0; i < node.ParameterList.Parameters.Count; i++)
		// {
		// 	var type = symbol.Parameters[i].Type;
		//
		// 	if (SyntaxHelpers.TryGetConstantValue(context.Compilation, node.pa, token, out _))
		// 	{
		// 		ReportDiagnostic(context, node.ParameterList.Parameters[i].Type, symbol.Parameters[i].Name);
		// 	}
		// }
	}
}