using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ParameterInvalidAnalyzer() : BaseAnalyzer<MethodDeclarationSyntax, IMethodSymbol>("CEA003", DiagnosticSeverity.Warning)
{
	public override SyntaxKind SyntaxKind => SyntaxKind.MethodDeclaration;

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol)
	{
		return symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax node, IMethodSymbol symbol)
	{
		for (var i = 0; i < node.ParameterList.Parameters.Count; i++)
		{
			var type = symbol.Parameters[i].Type;
		
			if (!IsNumericType(type) && !IsImmutableArrayOfNumbers(type))
			{
				ReportDiagnostic(context, node.ParameterList.Parameters[i].Type, symbol.Parameters[i].Name);
			}
		}
	}
}