using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodShouldBeStaticAnalyzer() : BaseAnalyzer<MethodDeclarationSyntax, IMethodSymbol>("CEA002", DiagnosticSeverity.Warning)
{
	public override SyntaxKind SyntaxKind => SyntaxKind.MethodDeclaration;

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol)
	{
		return !symbol.IsStatic && symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, IMethodSymbol symbol)
	{
		ReportDiagnostic(context, node.Identifier, symbol.Name);
	}
}