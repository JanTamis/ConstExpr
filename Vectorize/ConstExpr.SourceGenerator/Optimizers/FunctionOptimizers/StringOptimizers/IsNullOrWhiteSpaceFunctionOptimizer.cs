using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Optimizes string.IsNullOrWhiteSpace(value) into a hybrid implementation: a scalar early-exit
///   loop for short inputs (where SIMD dispatch overhead dominates) and a
///   SearchValues&lt;char&gt;-based SIMD scan for long inputs (where the scalar BCL loop is O(n) and
///   the SIMD path wins by an order of magnitude). Emits two helpers once per compilation unit —
///   IsWhiteSpaceFast(ReadOnlySpan&lt;char&gt;) with no null check, and IsNullOrWhiteSpaceFast(string?)
///   which handles null — and picks between them the same way the existing per-invocation string
///   optimizers already do: CanBeNull (driven by OptimizationFlags.UseNullableAnnotations) selects
///   the null-check-free call site when nullability is provably ruled out, and the null-checking one
///   otherwise.
/// </summary>
public class IsNullOrWhiteSpaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrWhiteSpace", true, n => n is 1)
{
	private const string FieldName = "s_isNullOrWhiteSpaceValues";
	private const string SpanHelperName = "IsWhiteSpaceFast";
	private const string StringHelperName = "IsNullOrWhiteSpaceFast";

	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// SearchValues<char> is .NET 8+; leave the call as written when the target compilation
		// doesn't have it.
		if (!context.Model.Compilation.GetTypeByMetadataName("System.Buffers.SearchValues").HasMethod("Create"))
		{
			return false;
		}

		context.Usings.Add("System.Buffers");
		context.Usings.Add("System.Numerics");

		// This text must stay as the raw (unescaped) escape-sequence source so it is emitted
		// verbatim into the generated code as a normal (non-verbatim) string literal. When that
		// generated code is compiled, each escape sequence (\t, \u0020, ...) is interpreted by the
		// C# compiler into the actual whitespace character it denotes. Passing it through
		// CreateLiteral would instead escape the backslashes themselves (producing "\\t\\n..."),
		// which compiles into literal backslash + letter characters instead of real whitespace.
		const string whitespaceEscapeText = @"\t\n\v\f\r\u0020\u0085\u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000";
		const string whitespaceValue = "\t\n\v\f\r\u0020\u0085\u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000";

		var whitespaceLiteral = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("\"" + whitespaceEscapeText + "\"", whitespaceValue));

		var field = FieldDeclaration(
				VariableDeclaration(
						GenericName(Identifier("SearchValues"))
							.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(PredefinedType(Token(SyntaxKind.CharKeyword))))))
					.WithVariables(SingletonSeparatedList(
						VariableDeclarator(Identifier(FieldName))
							.WithInitializer(EqualsValueClause(
								InvocationExpression(MemberAccessExpression(IdentifierName("SearchValues"), IdentifierName("Create")))
									.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(whitespaceLiteral)))))))))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));

		context.AdditionalSyntax.TryAdd(field, false);

		var parameter = context.VisitedParameters[0];

		if (!CanBeNull(context, parameter))
		{
			var spanHelper = ParseMethodFromString($$"""
				private static bool {{SpanHelperName}}(ReadOnlySpan<char> span)
				{
					if (!Vector.IsHardwareAccelerated || span.Length <= Vector<ushort>.Count * 4)
					{
						return span.IsWhiteSpace();
					}

					return !span.ContainsAnyExcept({{FieldName}});
				}
				""");

			if (spanHelper is not null)
			{
				context.AdditionalSyntax.TryAdd(spanHelper, false);
			}
		}
		else
		{
			var stringHelper = ParseMethodFromString($$"""
				private static bool {{StringHelperName}}(string? value)
				{
					if (value is null)
					{
						return true;
					}
					
					var span = value.AsSpan();
					
					if (!Vector.IsHardwareAccelerated || span.Length <= Vector<ushort>.Count * 4)
					{
						return span.IsWhiteSpace();
					}
					
					return !span.ContainsAnyExcept({{FieldName}});
				}
				""");

			if (stringHelper is not null)
			{
				context.AdditionalSyntax.TryAdd(stringHelper, false);
			}
		}

		result = CanBeNull(context, parameter)
			? CreateInvocation(StringHelperName, parameter)
			: CreateInvocation(SpanHelperName, InvocationExpression(MemberAccessExpression(parameter, IdentifierName("AsSpan"))));

		return true;
	}
}