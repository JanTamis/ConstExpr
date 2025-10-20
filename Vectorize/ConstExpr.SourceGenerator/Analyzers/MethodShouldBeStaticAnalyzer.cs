using ConstExpr.SourceGenerator.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA002")]
[DiagnosticTitle("Method is not static")]
[DiagnosticMessageFormat("Constant method '{0}' should be static")]
[DiagnosticDescription("Constant method should be static")]
[DiagnosticCategory("Usage")]
public class MethodShouldBeStaticAnalyzer : BaseAnalyzer<MethodDeclarationSyntax, IMethodSymbol>
{
	protected override bool ValidateSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, CancellationToken token)
	{
		return !node.Modifiers.Any(c => c.IsKind(SyntaxKind.StaticKeyword));
	}

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return !symbol.IsStatic && symbol
			.GetAttributes()
			.Concat(symbol.ContainingType.GetAttributes())
			.Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		ReportDiagnostic(context, node.Identifier, symbol.Name);
	}
}