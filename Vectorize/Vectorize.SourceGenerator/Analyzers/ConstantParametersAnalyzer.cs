using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConstantParametersAnalyzer() : BaseAnalyzer<InvocationExpressionSyntax, IMethodSymbol>("CEA001", DiagnosticSeverity.Warning)
{
	public override SyntaxKind SyntaxKind => SyntaxKind.InvocationExpression;

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol)
	{
		return symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol)
	{
		for (var i = 0; i < node.ArgumentList.Arguments.Count; i++)
		{
			var parameter = node.ArgumentList.Arguments[i];

			if (!IsConstantValue(context.SemanticModel, parameter.Expression, context.CancellationToken))
			{
				ReportDiagnostic(context, parameter, symbol.Parameters[i].Name);
			}
		}
	}
}