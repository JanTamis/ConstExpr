using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Vectorize.Attributes;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA002")]
[DiagnosticTitle("Method is not static")]
[DiagnosticMessageFormat("Constant method '{0}' should be static")]
[DiagnosticDescription("Constant method should be static")]
[DiagnosticCategory("Usage")]
public class MethodShouldBeStaticAnalyzer : BaseAnalyzer<MethodDeclarationSyntax, IMethodSymbol>
{
	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return !symbol.IsStatic && symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		ReportDiagnostic(context, node.Identifier, symbol.Name);
	}
}