using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Threading;
using ConstExpr.SourceGenerator.Attributes;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[DiagnosticSeverity(DiagnosticSeverity.Warning)]
[DiagnosticId("CEA001")]
[DiagnosticTitle("Parameter is not constant")]
[DiagnosticMessageFormat("Parameter '{0}' must be a constant expression in ConstExpr method calls")]
[DiagnosticDescription("Parameters marked with the ConstExpr attribute should be constant")]
[DiagnosticCategory("Usage")]
public class ConstantParametersAnalyzer : BaseAnalyzer<InvocationExpressionSyntax, IMethodSymbol>
{
	protected override bool ValidateSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, CancellationToken token)
	{
		SyntaxNode? item = node;
		
		while (item != null)
		{
			if (item is MethodDeclarationSyntax methodDeclaration && IsInConstExprBody(methodDeclaration))
			{
				return false;
			}

			item = item.Parent;
		}
		
		return true;
	}

	protected override bool ValidateSymbol(SyntaxNodeAnalysisContext context, IMethodSymbol symbol, CancellationToken token)
	{
		return symbol
			.GetAttributes()
			.Concat(symbol.ContainingType.GetAttributes())
			.Any(IsConstExprAttribute);
	}

	protected override void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax node, IMethodSymbol symbol, CancellationToken token)
	{
		var loader = MetadataLoader.GetLoader(context.Compilation);

		for (var i = 0; i < node.ArgumentList.Arguments.Count; i++)
		{
			var parameter = node.ArgumentList.Arguments[i];

			if (!TryGetConstantValue(context.SemanticModel.Compilation, loader, parameter.Expression, null, context.CancellationToken, out _))
			{
				ReportDiagnostic(context, parameter, parameter.ToString());
			}
		}
	}
}