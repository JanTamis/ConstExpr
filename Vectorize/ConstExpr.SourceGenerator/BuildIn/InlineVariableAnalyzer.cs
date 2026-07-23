using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.BuildIn;

public sealed class InlineVariableAnalyzer(SemanticModel semanticModel, ConcurrentDictionary<ulong, ISymbol> symbolStore)
{
	public IReadOnlyList<InlineCandidate> FindInlineCandidates(SyntaxNode root)
	{
		var candidates = new List<InlineCandidate>();

		foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
		foreach (var variable in declaration.Declaration.Variables)
		{
			if (TryGetInlineCandidate(declaration, variable, out var candidate))
			{
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
		{
			return false;
		}

		if (!semanticModel.TryGetDeclaredSymbol(variable, symbolStore, out ILocalSymbol? symbol))
		{
			return false;
		}

		if (symbol.RefKind != RefKind.None)
		{
			return false;
		}

		if (declaration.Parent is not BlockSyntax containingBlock)
		{
			return false;
		}

		var allRefs = containingBlock
			.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(id => id.Identifier.Text == symbol.Name
			             && id.SpanStart > declaration.SpanStart
			             && SymbolEqualityComparer.Default.Equals(
				             semanticModel.GetSymbolInfo(id).Symbol, symbol))
			.ToList();

		var writeRefs = allRefs.Where(IsWriteReference).ToList();
		var readRefs = allRefs.Where(r => !IsWriteReference(r)).ToList();

		if (writeRefs.Count != 0 || readRefs.Count == 0)
		{
			return false;
		}

		// Const variables can always be inlined regardless of read count
		if (!symbol.IsConst && readRefs.Count > 1 && !AreAllMutuallyExclusive(readRefs))
		{
			return false;
		}

		// ── Kern fix: controleer per read-site of een dependency gewijzigd is ──
		// Dit vangt swap-patronen af, ongeacht de purity van de initializer.
		var initSymbols = GetReferencedSymbols(variable.Initializer.Value);

		if (initSymbols.Count > 0)
		{
			foreach (var readSite in readRefs)
			{
				if (IsDependencyMutatedOnPathToRead(containingBlock, declaration, readSite, initSymbols))
				{
					return false;
				}
			}
		}

		// A read inside a loop that does not also contain the declaration would turn a
		// value computed once before the loop into one recomputed every iteration.
		if (readRefs.Any(readSite => IsReadInsideUncontainingLoop(declaration, readSite)))
		{
			return false;
		}

		var purity = ClassifyPurity(semanticModel.GetOperation(variable.Initializer.Value));

		candidate = new InlineCandidate(
			symbol,
			declaration,
			variable,
			readRefs,
			purity
		);

		return true;
	}

	// ── Dependency mutation check ─────────────────────────────────────────────

	/// <summary>
	///   Geeft true als een van de symbols waarvan de initializer afhankelijk is
	///   wordt geschreven op het uitvoeringspad van de declaratie naar de read-site.
	///   Voorbeeld — swap:
	///   var temp = a;   ← initSymbols = { a }
	///   a = b;          ← schrijft naar a  →  true  →  niet inlineable
	///   b = temp;
	/// </summary>
	private bool IsDependencyMutatedOnPathToRead(
		BlockSyntax block,
		LocalDeclarationStatementSyntax declaration,
		IdentifierNameSyntax readSite,
		ISet<ISymbol> initSymbols)
	{
		var statements = block.Statements.ToList();
		var declIndex = statements.IndexOf(declaration);

		// Het top-level statement in dit blok dat de read-site bevat
		var readTopLevel = readSite
			.AncestorsAndSelf()
			.OfType<StatementSyntax>()
			.FirstOrDefault(s => s.Parent == block);

		if (readTopLevel is null)
		{
			return true; // conservatief
		}

		var readIndex = statements.IndexOf(readTopLevel);

		// 1. Statements strikt tussen declaratie en het top-level read-statement
		for (var i = declIndex + 1; i < readIndex; i++)
		{
			if (WritesAnySymbol(statements[i], initSymbols))
			{
				return true;
			}
		}

		// 2. Binnen het top-level read-statement (bv. switch/if), vóór de read-positie
		//    Voorbeeld: case 1: a = b; x = temp;  →  a wordt vóór temp gelezen
		return WritesAnySymbolBeforePosition(readTopLevel, readSite.SpanStart, initSymbols);
	}

	/// <summary>
	///   Geeft alle symbols terug die in de expressie worden gelezen,
	///   geselecteerd op types die gemuteerd kunnen worden (locals, parameters, fields, properties).
	/// </summary>
	private ISet<ISymbol> GetReferencedSymbols(ExpressionSyntax expr)
	{
		var result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

		foreach (var node in expr.DescendantNodesAndSelf())
		{
			switch (node)
			{
				// Normale identifier: a, left, right, ...
				case IdentifierNameSyntax id:
				{
					var sym = semanticModel.GetSymbolInfo(id).Symbol;

					if (sym is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
					{
						result.Add(sym);
					}
					break;
				}
				// ── Fix: arr[i] → voeg arr toe als dependency ──
				// Zonder dit wordt arr gemist als mutable dependency.
				case ElementAccessExpressionSyntax elementAccess:
				{
					var baseSym = semanticModel.GetSymbolInfo(elementAccess.Expression).Symbol;

					if (baseSym is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
					{
						result.Add(baseSym);
					}
					break;
				}
			}
		}

		return result;
	}

	/// <summary>Schrijft <paramref name="node" /> (of een descendant) naar een van de symbols?</summary>
	private bool WritesAnySymbol(SyntaxNode node, ISet<ISymbol> symbols)
	{
		return node.DescendantNodesAndSelf().Any(n => IsWriteToAny(n, symbols));
	}

	/// <summary>
	///   Schrijft iets binnen <paramref name="container" /> vóór <paramref name="position" />
	///   naar een van de symbols?
	/// </summary>
	private bool WritesAnySymbolBeforePosition(
		SyntaxNode container, int position, ISet<ISymbol> symbols)
	{
		return container
			.DescendantNodes()
			.Where(n => n.SpanStart < position && n.Span.End <= position)
			.Any(n => IsWriteToAny(n, symbols));
	}

	/// <summary>
	///   Herkent schrijf-patronen naar een specifiek symbol:
	///   a = …  |  ++a / a++  |  ref a / out a
	/// </summary>
	private bool IsWriteToAny(SyntaxNode node, ISet<ISymbol> symbols)
	{
		var target = node switch
		{
			AssignmentExpressionSyntax assign => assign.Left,
			PrefixUnaryExpressionSyntax
				{
					RawKind: (int) SyntaxKind.PreIncrementExpression
					or (int) SyntaxKind.PreDecrementExpression
				} p
				=> p.Operand,
			PostfixUnaryExpressionSyntax p => p.Operand,
			ArgumentSyntax { RefKindKeyword: var kw } arg
				when kw.IsKind(SyntaxKind.RefKeyword)
				     || kw.IsKind(SyntaxKind.OutKeyword) => arg.Expression,
			_ => null
		};

		if (target is null)
		{
			return false;
		}

		// ── Fix: indexer-write (arr[i] = ...) muteert arr ──
		// ElementAccessExpressionSyntax heeft geen eigen symbol; pak de basis.
		var resolvedTarget = target is ElementAccessExpressionSyntax elementAccess
			? elementAccess.Expression
			: target;

		var written = semanticModel.GetSymbolInfo(resolvedTarget).Symbol;
		return written is not null && symbols.Contains(written);
	}

	// ── Loop-boundary check ───────────────────────────────────────────────────

	/// <summary>
	///   True if <paramref name="readSite" /> sits inside a loop that does not also contain
	///   <paramref name="declaration" /> — inlining there would turn a value computed once
	///   before the loop into one recomputed on every iteration.
	/// </summary>
	private static bool IsReadInsideUncontainingLoop(LocalDeclarationStatementSyntax declaration, IdentifierNameSyntax readSite)
	{
		return readSite
			.Ancestors()
			.Any(a => a is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CommonForEachStatementSyntax
			          && !a.Span.Contains(declaration.Span));
	}

	// ── Wederzijdse exclusiviteit ─────────────────────────────────────────────

	private bool AreAllMutuallyExclusive(List<IdentifierNameSyntax> refs)
	{
		for (var i = 0; i < refs.Count; i++)
		for (var j = i + 1; j < refs.Count; j++)
		{
			if (!AreMutuallyExclusive(refs[i], refs[j]))
			{
				return false;
			}
		}
		return true;
	}

	private bool AreMutuallyExclusive(SyntaxNode a, SyntaxNode b)
	{
		var ancestorsA = new HashSet<SyntaxNode>(a.AncestorsAndSelf());

		foreach (var ancestor in b.AncestorsAndSelf())
		{
			if (!ancestorsA.Contains(ancestor))
			{
				continue;
			}

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

	private static bool AreInDifferentSwitchSections(SwitchStatementSyntax sw, SyntaxNode a, SyntaxNode b)
	{

		var sA = SectionOf(a);
		var sB = SectionOf(b);
		return sA is not null && sB is not null && !sA.IsEquivalentTo(sB);

		SwitchSectionSyntax? SectionOf(SyntaxNode n) =>
			n.AncestorsAndSelf().OfType<SwitchSectionSyntax>().FirstOrDefault(s => s.Parent == sw);
	}

	private static bool AreInDifferentIfBranches(IfStatementSyntax ifStmt, SyntaxNode a, SyntaxNode b)
	{
		return InThen(a) && InElse(b) || InElse(a) && InThen(b);
		bool InThen(SyntaxNode n) => ifStmt.Statement.Contains(n);
		bool InElse(SyntaxNode n) => ifStmt.Else?.Statement.Contains(n) ?? false;
	}

	private static bool AreInDifferentSwitchExpressionArms(SwitchExpressionSyntax sw, SyntaxNode a, SyntaxNode b)
	{

		var aA = ArmOf(a);
		var aB = ArmOf(b);
		return aA is not null && aB is not null && !aA.IsEquivalentTo(aB);

		SwitchExpressionArmSyntax? ArmOf(SyntaxNode n) =>
			n.AncestorsAndSelf().OfType<SwitchExpressionArmSyntax>().FirstOrDefault(arm => arm.Parent == sw);
	}

	// ── Write-referentie detectie ─────────────────────────────────────────────

	private static bool IsWriteReference(IdentifierNameSyntax id)
	{
		return id.Parent switch
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
	}

	// ── Purity classificatie ──────────────────────────────────────────────────

	private static InitializerPurity ClassifyPurity(IOperation? op)
	{
		return op switch
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
			_ => InitializerPurity.Unknown
		};
	}

	private static bool BothPure(IOperation a, IOperation b)
	{
		return ClassifyPurity(a) <= InitializerPurity.PureRead &&
		       ClassifyPurity(b) <= InitializerPurity.PureRead;
	}
}

// ── Data types ────────────────────────────────────────────────────────────────

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
	IReadOnlyList<IdentifierNameSyntax> ReadSites,
	InitializerPurity InitializerPurity
)
{
	public override string ToString()
	{
		return $"{Symbol.Name} ({InitializerPurity}) " +
		       $"@ line {Declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1} " +
		       $"[{ReadSites.Count} read(s)]";
	}
}