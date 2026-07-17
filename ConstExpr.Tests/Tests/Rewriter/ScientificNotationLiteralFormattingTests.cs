extern alias sourcegen;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sourcegen::ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for the BlockFormattingRewriter's small/large-magnitude scientific-notation formatting.</summary>
public class ScientificNotationLiteralFormattingTests
{
	private static string Render(string literalText)
	{
		var method = (MethodDeclarationSyntax) SyntaxFactory.ParseMemberDeclaration($"double M() {{ return {literalText}; }}")!;
		return FormattingHelper.Render(method.Body!)!;
	}

	[Test, Arguments("-0.00019841269836761127", "-1.9841269836761127E-4;"), Arguments("0.009618129107628477", "9.618129107628477E-3;"), Arguments("123456789012.0", "1.23456789012E+11;")]
	public async Task AwkwardMagnitude_UsesScientificNotation(string literalText, string expectedSuffix)
	{
		await Assert.That(Render(literalText)).Contains(expectedSuffix);
	}

	[Test, Arguments("0.5", "0.5;"), Arguments("3.14159", "3.14159;"), Arguments("100.0", "100D;"), Arguments("0.041666666666666664", "0.041666666666666664;")]
	public async Task NormalMagnitude_StaysFixedNotation(string literalText, string expectedSuffix)
	{
		await Assert.That(Render(literalText)).Contains(expectedSuffix);
	}

	[Test]
	public async Task Idempotent_WhenFormattedTwice()
	{
		var once = Render("-0.00019841269836761127");
		var twice = FormattingHelper.Render(SyntaxFactory.ParseStatement(once));

		await Assert.That(twice).IsEqualTo(once);
	}
}