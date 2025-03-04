using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using ConstExpr.SourceGenerator.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConstExpr.SourceGenerator.Analyzers;

public abstract class BaseAnalyzer<TNode, TSymbol> : DiagnosticAnalyzer 
	where TNode : SyntaxNode
	where TSymbol : ISymbol
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

	protected BaseAnalyzer()
	{
		SupportedDiagnostics =
		[
			new DiagnosticDescriptor(
				GetAttribute<DiagnosticIdAttribute>().Id,
				GetAttribute<DiagnosticTitleAttribute>().Title,
				GetAttribute<DiagnosticMessageFormatAttribute>().Message,
				GetAttribute<DiagnosticCategoryAttribute>().Category,
				GetAttribute<DiagnosticSeverityAttribute>().Severity,
				isEnabledByDefault: true,
				GetAttribute<DiagnosticDescriptionAttribute>().Description),
		];
	}
	
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(ctx =>
		{
			if (ctx.Node is not TNode node || !ValidateSyntax(ctx, node, ctx.CancellationToken))
			{
				return;
			}

			if (ctx.SemanticModel.GetSymbolInfo(node, ctx.CancellationToken).Symbol is TSymbol symbol)
			{
				if (ValidateSymbol(ctx, symbol, ctx.CancellationToken))
				{
					AnalyzeSyntax(ctx, node, symbol, ctx.CancellationToken);
					return;
				}
			}

			if (ctx.SemanticModel.GetDeclaredSymbol(node, ctx.CancellationToken) is TSymbol declaredSymbol)
			{
				if (ValidateSymbol(ctx, declaredSymbol, ctx.CancellationToken))
				{
					AnalyzeSyntax(ctx, node, declaredSymbol, ctx.CancellationToken);
				}
			}
		}, GetSyntaxKind());
	}
	
	protected abstract void AnalyzeSyntax(SyntaxNodeAnalysisContext context, TNode node, TSymbol symbol, CancellationToken token);

	protected virtual bool ValidateSyntax(SyntaxNodeAnalysisContext context, TNode node, CancellationToken token) => true;

	protected virtual bool ValidateSymbol(SyntaxNodeAnalysisContext context, TSymbol symbol, CancellationToken token) => true;
	
	public void ReportDiagnostic(SyntaxNodeAnalysisContext context, CSharpSyntaxNode node, params object?[]? messageArgs)
	{
		ReportDiagnostic(context, node.GetLocation(), messageArgs);
	}

	public void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxToken node, params object?[]? messageArgs)
	{
		ReportDiagnostic(context, node.GetLocation(), messageArgs);
	}

	public void ReportDiagnostic(SyntaxNodeAnalysisContext context, Location location, params object?[]? messageArgs)
	{
		context.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics[0], location, messageArgs));
	}
	
	private T GetAttribute<T>() where T : Attribute
	{
		return GetType().GetCustomAttributes<T>().First();
	}
	
	private SyntaxKind GetSyntaxKind()
	{
		var name = typeof(TNode).Name;

		if (!Enum.TryParse(name.Substring(0, name.Length - 6), out SyntaxKind kind))
		{
			throw new InvalidOperationException($"Unable to parse SyntaxKind from {name}");
		}

		return kind;
	}
}