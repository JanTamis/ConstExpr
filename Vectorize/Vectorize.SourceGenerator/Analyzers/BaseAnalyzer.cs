using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Vectorize.Resources;

namespace Vectorize.Analyzers;

public abstract class BaseAnalyzer<TNode, TSymbol> : DiagnosticAnalyzer 
	where TNode : SyntaxNode
	where TSymbol : ISymbol
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

	public abstract SyntaxKind SyntaxKind { get; }

	public BaseAnalyzer(string diagnosticId, DiagnosticSeverity severity)
	{
		var resourceManager = AnalyzerResources.ResourceManager;
		
		SupportedDiagnostics =
		[
			new DiagnosticDescriptor(
				diagnosticId,
				resourceManager.GetString($"{diagnosticId}_Title"),
				resourceManager.GetString($"{diagnosticId}_MessageFormat"),
				resourceManager.GetString($"{diagnosticId}_Category"),
				severity,
				isEnabledByDefault: true,
				resourceManager.GetString($"{diagnosticId}_Description"))
		];
	}
	
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(ctx =>
		{
			if (ctx.Node is not TNode node || !ValidateSyntax(ctx, node))
			{
				return;
			}

			if (ctx.SemanticModel.GetSymbolInfo(node, ctx.CancellationToken).Symbol is TSymbol symbol)
			{
				if (ValidateSymbol(ctx, symbol))
				{
					AnalyzeSyntax(ctx, node, symbol);
					return;
				}
			}

			if (ctx.SemanticModel.GetDeclaredSymbol(node, ctx.CancellationToken) is TSymbol declaredSymbol)
			{
				if (ValidateSymbol(ctx, declaredSymbol))
				{
					AnalyzeSyntax(ctx, node, declaredSymbol);
				}
			}
		}, SyntaxKind);
	}
	
	protected abstract void AnalyzeSyntax(SyntaxNodeAnalysisContext context, TNode node, TSymbol symbol);

	protected virtual bool ValidateSyntax(SyntaxNodeAnalysisContext context, TNode node) => true;

	protected virtual bool ValidateSymbol(SyntaxNodeAnalysisContext context, TSymbol symbol) => true;
	
	protected void ReportDiagnostic(SyntaxNodeAnalysisContext context, CSharpSyntaxNode node, params object?[]? messageArgs)
	{
		context.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics[0], node.GetLocation(), messageArgs));
	}

	protected void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxToken node, params object?[]? messageArgs)
	{
		context.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics[0], node.GetLocation(), messageArgs));
	}
}