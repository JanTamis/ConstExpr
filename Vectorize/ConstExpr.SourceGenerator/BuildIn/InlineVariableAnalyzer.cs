using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.BuildIn;

public sealed class InlineVariableAnalyzer
{
	private readonly SemanticModel _semanticModel;

	public InlineVariableAnalyzer(SemanticModel semanticModel)
	{
		_semanticModel = semanticModel;
	}

	public IReadOnlyList<InlineCandidate> FindInlineCandidates(SyntaxNode root)
	{
		var candidates = new List<InlineCandidate>();

		foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
		{
			foreach (var variable in declaration.Declaration.Variables)
			{
				if (TryGetInlineCandidate(declaration, variable, out var candidate))
					candidates.Add(candidate!);
			}
		}

		return candidates;
	}

	private bool TryGetInlineCandidate(
		LocalDeclarationStatementSyntax declaration,
		VariableDeclaratorSyntax variable,
		out InlineCandidate? candidate)
	{
		candidate = null;

		if (variable.Initializer is null)
			return false;

		if (_semanticModel.GetDeclaredSymbol(variable) is not ILocalSymbol symbol)
			return false;

		if (symbol.RefKind != RefKind.None)
			return false;

		if (declaration.Parent is not BlockSyntax containingBlock)
			return false;

		// Verzamel alle referenties (exclusief de declaratie zelf)
		var allRefs = containingBlock
			.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(id => id.Identifier.Text == symbol.Name
			             && id.SpanStart > declaration.SpanStart
			             && SymbolEqualityComparer.Default.Equals(
				             _semanticModel.GetSymbolInfo(id).Symbol, symbol))
			.ToList();

		var writeRefs = allRefs.Where(IsWriteReference).ToList();
		var readRefs = allRefs.Where(r => !IsWriteReference(r)).ToList();

		// Na declaratie mag er geen write meer zijn
		if (writeRefs.Count != 0)
			return false;

		if (readRefs.Count == 0)
			return false;

		// ── Kern fix: meerdere reads zijn OK als ze wederzijds exclusief zijn ──
		if (readRefs.Count > 1 && !AreAllMutuallyExclusive(readRefs))
			return false;

		// Side-effect check: is de initializer puur genoeg?
		var initializerOperation = _semanticModel.GetOperation(variable.Initializer.Value);
		var purity = ClassifyPurity(initializerOperation);

		// Bij impure initializers: controleer of er iets tussen declaratie en reads zit
		// dat de waarde kan beïnvloeden
		// if (purity >= InitializerPurity.ImpureRead)
		// {
		// 	if (HasMutatingStatementsBetweenDeclarationAndReads(
		// 		    containingBlock, declaration, readRefs))
		// 		return false;
		// }

		candidate = new InlineCandidate(
			Symbol: symbol,
			Declaration: declaration,
			Variable: variable,
			ReadSites: readRefs,
			InitializerPurity: purity
		);

		return true;
	}

	// ── Wederzijdse exclusiviteit ────────────────────────────────────────────

	/// <summary>
	/// Geeft true als elke combinatie van twee reads in wederzijds exclusieve
	/// branches zit (switch-cases of if/else-takken).
	/// </summary>
	private bool AreAllMutuallyExclusive(List<IdentifierNameSyntax> refs)
	{
		for (int i = 0; i < refs.Count; i++)
		for (int j = i + 1; j < refs.Count; j++)
		{
			if (!AreMutuallyExclusive(refs[i], refs[j]))
				return false;
		}
		return true;
	}

	private bool AreMutuallyExclusive(SyntaxNode a, SyntaxNode b)
	{
		// Zoek de dichtstbijzijnde gemeenschappelijke voorouder
		var ancestorsA = new HashSet<SyntaxNode>(a.AncestorsAndSelf());

		foreach (var ancestor in b.AncestorsAndSelf())
		{
			if (!ancestorsA.Contains(ancestor))
				continue;

			// Gevonden: de LCA. Kijk of a en b in verschillende exclusieve takken zitten.
			return ancestor switch
			{
				SwitchStatementSyntax sw => AreInDifferentSwitchSections(sw, a, b),
				IfStatementSyntax ifStmt => AreInDifferentIfBranches(ifStmt, a, b),
				SwitchExpressionSyntax sw2 => AreInDifferentSwitchExpressionArms(sw2, a, b),
				_ => false
			};
		}

		return false;
	}

	private static bool AreInDifferentSwitchSections(
		SwitchStatementSyntax sw, SyntaxNode a, SyntaxNode b)
	{
		SwitchSectionSyntax? SectionOf(SyntaxNode node) =>
			node.AncestorsAndSelf()
				.OfType<SwitchSectionSyntax>()
				.FirstOrDefault(s => s.Parent == sw);

		var sectionA = SectionOf(a);
		var sectionB = SectionOf(b);

		return sectionA is not null
		       && sectionB is not null
		       && !sectionA.IsEquivalentTo(sectionB);
	}

	private static bool AreInDifferentIfBranches(
		IfStatementSyntax ifStmt, SyntaxNode a, SyntaxNode b)
	{
		bool InThen(SyntaxNode n) => ifStmt.Statement.Contains(n);
		bool InElse(SyntaxNode n) => ifStmt.Else?.Statement.Contains(n) ?? false;

		return (InThen(a) && InElse(b)) || (InElse(a) && InThen(b));
	}

	private static bool AreInDifferentSwitchExpressionArms(
		SwitchExpressionSyntax sw, SyntaxNode a, SyntaxNode b)
	{
		SwitchExpressionArmSyntax? ArmOf(SyntaxNode node) =>
			node.AncestorsAndSelf()
				.OfType<SwitchExpressionArmSyntax>()
				.FirstOrDefault(arm => arm.Parent == sw);

		var armA = ArmOf(a);
		var armB = ArmOf(b);

		return armA is not null
		       && armB is not null
		       && !armA.IsEquivalentTo(armB);
	}

	// ── Side-effect check ────────────────────────────────────────────────────

	/// <summary>
	/// Controleert of er tussen de declaratie en de (eerste) read-site
	/// statements zijn die mutable state kunnen wijzigen.
	/// </summary>
	private bool HasMutatingStatementsBetweenDeclarationAndReads(
		BlockSyntax block,
		LocalDeclarationStatementSyntax declaration,
		List<IdentifierNameSyntax> readRefs)
	{
		var statements = block.Statements.ToList();
		var declIndex = statements.IndexOf(declaration);

		// Vroegste read-statement in dit blok
		var earliestReadStatement = readRefs
			.Select(r => r.AncestorsAndSelf()
				.OfType<StatementSyntax>()
				.FirstOrDefault(s => s.Parent == block))
			.Where(s => s is not null)
			.OrderBy(s => s!.SpanStart)
			.FirstOrDefault();

		if (earliestReadStatement is null)
			return true; // conservatief

		var readIndex = statements.IndexOf(earliestReadStatement);

		for (int i = declIndex + 1; i < readIndex; i++)
		{
			if (StatementMutatesState(statements[i]))
				return true;
		}

		return false;
	}

	private static bool StatementMutatesState(StatementSyntax statement) =>
		statement.DescendantNodes().Any(node => node is
			InvocationExpressionSyntax or
			AwaitExpressionSyntax or
			AssignmentExpressionSyntax or
			PostfixUnaryExpressionSyntax or
			PrefixUnaryExpressionSyntax or
			YieldStatementSyntax);

	// ── Write-referentie detectie ────────────────────────────────────────────

	private static bool IsWriteReference(IdentifierNameSyntax id) =>
		id.Parent switch
		{
			AssignmentExpressionSyntax a => a.Left == id,
			PrefixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PreIncrementExpression)
			                                 || p.IsKind(SyntaxKind.PreDecrementExpression),
			PostfixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PostIncrementExpression)
			                                  || p.IsKind(SyntaxKind.PostDecrementExpression),
			ArgumentSyntax arg => arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
			                      || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
			_ => false
		};

	// ── Purity classificatie ─────────────────────────────────────────────────

	private static InitializerPurity ClassifyPurity(IOperation? op) => op switch
	{
		null => InitializerPurity.Unknown,
		ILiteralOperation => InitializerPurity.Constant,
		IDefaultValueOperation => InitializerPurity.Constant,
		ILocalReferenceOperation => InitializerPurity.PureRead,
		IParameterReferenceOperation => InitializerPurity.PureRead,
		IFieldReferenceOperation { Field.IsReadOnly: true } => InitializerPurity.PureRead,
		IPropertyReferenceOperation => InitializerPurity.ImpureRead,
		IBinaryOperation bin when BothPure(bin.LeftOperand,
			bin.RightOperand) => InitializerPurity.PureRead,
		IUnaryOperation u when
			ClassifyPurity(u.Operand) <= InitializerPurity.PureRead => InitializerPurity.PureRead,
		IInvocationOperation => InitializerPurity.HasSideEffects,
		IObjectCreationOperation => InitializerPurity.HasSideEffects,
		_ => InitializerPurity.Unknown,
	};

	private static bool BothPure(IOperation a, IOperation b) =>
		ClassifyPurity(a) <= InitializerPurity.PureRead &&
		ClassifyPurity(b) <= InitializerPurity.PureRead;
}

// ── Data types ───────────────────────────────────────────────────────────────

public enum InitializerPurity
{
	Constant,
	PureRead,
	ImpureRead,
	HasSideEffects,
	Unknown
}

public sealed record InlineCandidate(
	ILocalSymbol Symbol,
	LocalDeclarationStatementSyntax Declaration,
	VariableDeclaratorSyntax Variable,
	IReadOnlyList<IdentifierNameSyntax> ReadSites, // was: ReadSite (enkelvoud)
	InitializerPurity InitializerPurity
)
{
	public override string ToString() =>
		$"{Symbol.Name} ({InitializerPurity}) " +
		$"@ line {Declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1} " +
		$"[{ReadSites.Count} read(s)]";
}