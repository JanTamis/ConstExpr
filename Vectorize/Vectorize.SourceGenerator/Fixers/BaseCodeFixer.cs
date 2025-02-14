using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Vectorize.Fixers;

public abstract class BaseCodeFixer<TNode>(string diagnosticId, string title) : CodeFixProvider where TNode : SyntaxNode
{
	public override ImmutableArray<string> FixableDiagnosticIds => [diagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var diagnostic = context.Diagnostics.First(f => f.Id == diagnosticId);
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		// Zoek de methode-declaratie waarop de diagnostic van toepassing is.
		var node = root?.FindToken(diagnosticSpan.Start).Parent?
			.AncestorsAndSelf()
			.OfType<TNode>()
			.FirstOrDefault();

		if (node == null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
					title: title,
					createChangedDocument: async c => await ReplaceNodeAsync(context.Document, node, await ExecuteAsync(context.Document, node, c), c),
					equivalenceKey: title),
			diagnostic);
	}

	protected abstract Task<TNode> ExecuteAsync(Document document, TNode syntax, CancellationToken cancellationToken);

	protected async Task<Document> ReplaceNodeAsync(Document document, TNode oldNode, TNode newNode, CancellationToken cancellationToken)
	{
		if (SyntaxNodeComparer<TNode>.Instance.Equals(oldNode, newNode))
		{
			return document;
		}

		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

		if (root is null)
		{
			return document;
		}

		var newRoot = root.ReplaceNode(oldNode, newNode.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation));
		return document.WithSyntaxRoot(newRoot);
	}
}