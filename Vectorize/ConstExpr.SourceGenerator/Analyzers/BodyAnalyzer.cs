using System;
using System.Linq;
using System.Threading;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp), DiagnosticSeverity(DiagnosticSeverity.Warning), DiagnosticId("CEA004"), DiagnosticTitle("Exception during evaluation"), DiagnosticMessageFormat("Unable to evaluate: {0}"), DiagnosticDescription("ConstExpr methods must be constant expressions"), DiagnosticCategory("Usage")]
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
		var loader = MetadataLoader.GetLoader(context.SemanticModel.Compilation);
		var cache = new RoslynApiCache();

		var attributes = symbol.GetAttributes()
			.Concat(symbol.ContainingType?.GetAttributes() ?? Enumerable.Empty<AttributeData>())
			.Concat(symbol.ContainingAssembly.GetAttributes());

		var attribute = attributes.FirstOrDefault(IsAttribute<ConstEvalAttribute>)
		                ?? attributes.FirstOrDefault(IsAttribute<ConstExprAttribute>);

		try
		{
			// The result is discarded - this call exists to surface exceptions as diagnostics. The
			// real formatting options are passed anyway so that a fault in a rendering pass is
			// reported here too, instead of only failing at emit time.
			var formatting = FormattingOptions.Read(context.Options.AnalyzerConfigOptionsProvider, context.SemanticModel.SyntaxTree);

			ConstExprSourceGenerator.GenerateExpression(context.SemanticModel, loader, node, symbol, attribute!.ToAttribute<ConstExprAttribute>(), cache, formatting, token);
		}
		catch (Exception e)
		{
			ReportDiagnostic(context, node, e.Message);
		}
	}
}