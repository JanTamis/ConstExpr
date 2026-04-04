using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts anonymous type creation expressions to value tuples.
/// Inspired by the Roslyn <c>CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider</c>.
///
/// <code>
/// new { Name = "Bob", Age = 42 }
/// </code>
/// →
/// <code>
/// (Name: "Bob", Age: 42)
/// </code>
/// </summary>
public static class ConvertAnonymousTypeToTupleRefactoring
{
	/// <summary>
	/// Converts an anonymous object creation expression to a tuple expression.
	/// </summary>
	public static bool TryConvertAnonymousTypeToTuple(
		AnonymousObjectCreationExpressionSyntax anonymousObject,
		[NotNullWhen(true)] out TupleExpressionSyntax? result)
	{
		result = null;

		if (anonymousObject.Initializers.Count < 2)
		{
			// Tuples must have at least 2 elements
			return false;
		}

		var arguments = new List<ArgumentSyntax>();

		foreach (var initializer in anonymousObject.Initializers)
		{
			var expression = initializer.Expression;

			if (initializer.NameEquals is not null)
			{
				// Named: Name = expr → Name: expr
				arguments.Add(Argument(NameColon(initializer.NameEquals.Name.WithoutTrivia()), default, expression.WithoutTrivia()));
			}
			else
			{
				// Implicit name from expression (e.g., new { x } → (x))
				arguments.Add(Argument(expression.WithoutTrivia()));
			}
		}

		result = TupleExpression(
				SeparatedList(arguments))
			.WithTriviaFrom(anonymousObject);

		return true;
	}
}

