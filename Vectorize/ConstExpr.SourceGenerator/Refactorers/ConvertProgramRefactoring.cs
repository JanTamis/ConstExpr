using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts top-level statements to an explicit <c>Program.Main</c>
/// method, and vice versa.
/// Inspired by the Roslyn <c>ConvertProgramTransform_ProgramMain</c> and
/// <c>ConvertProgramTransform_TopLevelStatements</c>.
///
/// This is a simplified pure-syntax transformation.
/// </summary>
public static class ConvertProgramRefactoring
{
	/// <summary>
	/// Wraps top-level statements in a <c>Program</c> class with a <c>Main</c> method.
	///
	/// <code>
	/// using System;
	/// Console.WriteLine("Hi");
	/// </code>
	/// →
	/// <code>
	/// using System;
	/// class Program
	/// {
	///     static void Main(string[] args)
	///     {
	///         Console.WriteLine("Hi");
	///     }
	/// }
	/// </code>
	/// </summary>
	public static bool TryConvertTopLevelStatementsToProgramMain(
		CompilationUnitSyntax compilationUnit,
		[NotNullWhen(true)] out CompilationUnitSyntax? result)
	{
		result = null;

		// Collect top-level statements (GlobalStatementSyntax members)
		var globalStatements = compilationUnit.Members
			.OfType<GlobalStatementSyntax>()
			.ToList();

		if (globalStatements.Count == 0)
		{
			return false;
		}

		// Extract the statements
		var statements = new SyntaxList<StatementSyntax>(
			globalStatements.Select(g => g.Statement));

		// Build: static void Main(string[] args) { ... }
		var mainMethod = MethodDeclaration(
				PredefinedType(Token(SyntaxKind.VoidKeyword)),
				Identifier("Main"))
			.AddModifiers(Token(SyntaxKind.StaticKeyword))
			.AddParameterListParameters(
				Parameter(Identifier("args"))
					.WithType(
						ArrayType(
							PredefinedType(Token(SyntaxKind.StringKeyword)),
							SingletonList(ArrayRankSpecifier()))))
			.WithBody(Block(statements));

		// Build: class Program { Main() }
		var programClass = ClassDeclaration("Program")
			.AddMembers(mainMethod);

		// Keep non-global-statement members (other classes, etc.)
		var otherMembers = compilationUnit.Members
			.Where(m => m is not GlobalStatementSyntax)
			.ToList();

		var newMembers = new SyntaxList<MemberDeclarationSyntax>();
		newMembers = newMembers.Add(programClass);
		newMembers = newMembers.AddRange(otherMembers);

		result = compilationUnit
			.WithMembers(newMembers);

		return true;
	}

	/// <summary>
	/// Extracts statements from a <c>Program.Main</c> method to top-level statements.
	///
	/// Looks for a class named <c>Program</c> with a single static <c>Main</c> method.
	/// </summary>
	public static bool TryConvertProgramMainToTopLevelStatements(
		CompilationUnitSyntax compilationUnit,
		[NotNullWhen(true)] out CompilationUnitSyntax? result)
	{
		result = null;

		// Find the Program class
		var programClass = compilationUnit.Members
			.OfType<ClassDeclarationSyntax>()
			.FirstOrDefault(c => c.Identifier.ValueText == "Program");

		if (programClass is null)
		{
			return false;
		}

		// Must have only a Main method
		var mainMethod = programClass.Members
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(m => m.Identifier.ValueText == "Main" &&
			                     m.Modifiers.Any(SyntaxKind.StaticKeyword));

		if (mainMethod?.Body is null)
		{
			return false;
		}

		// Convert statements to global statements
		var globalStatements = mainMethod.Body.Statements
			.Select(s => (MemberDeclarationSyntax)GlobalStatement(s))
			.ToList();

		// Keep other members (other classes, enums, etc.)
		var otherMembers = compilationUnit.Members
			.Where(m => m != programClass)
			.ToList();

		// Also keep other members from the Program class if any
		var otherProgramMembers = programClass.Members
			.Where(m => m != mainMethod)
			.ToList();

		var newMembers = new SyntaxList<MemberDeclarationSyntax>();
		newMembers = newMembers.AddRange(globalStatements);
		newMembers = newMembers.AddRange(otherMembers);

		if (otherProgramMembers.Count > 0)
		{
			// Keep the Program class with remaining members
			var remainingClass = programClass.WithMembers(List(otherProgramMembers));
			newMembers = newMembers.Add(remainingClass);
		}

		result = compilationUnit
			.WithMembers(newMembers);

		return true;
	}
}

