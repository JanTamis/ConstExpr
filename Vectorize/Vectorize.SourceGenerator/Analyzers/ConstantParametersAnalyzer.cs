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
[DiagnosticId("CEA001")]
[DiagnosticTitle("Parameter is not constant")]
[DiagnosticMessageFormat("Parameter '{0}' should be a constant")]
[DiagnosticDescription("Parameters marked with the ConstExpr attribute should be constant")]
[DiagnosticCategory("Usage")]
public class ConstantParametersAnalyzer : BaseAnalyzer<InvocationExpressionSyntax, IMethodSymbol>
{
	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol.GetAttributes().Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		for (var i = 0; i < node.ArgumentList.Arguments.Count; i++)
		{
			var parameter = node.ArgumentList.Arguments[i];

			if (!TryGetConstantValue(context.SemanticModel.Compilation, parameter.Expression, context.CancellationToken, out _))
			{
				ReportDiagnostic(context, parameter, symbol.Parameters[i].Name);
			}
		}
	}
}