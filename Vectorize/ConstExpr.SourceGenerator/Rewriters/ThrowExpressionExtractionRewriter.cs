using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Extracts <c>throw new X(message)</c> expressions carrying an
///   <see cref="ExpectedTypeAnnotationKind" /> annotation (attached by
///   <see cref="Optimizers.FunctionOptimizers.LinqOptimizers.BaseLinqFunctionOptimizer.CreateThrowExpression{TException}" />
///   )
///   into a private, generic, <c>[DoesNotReturn]</c> static helper, reducing codegen at the throw site
///   without touching the exception construction itself.
///   <para>
///     Runs as a separate, final pass rather than at throw-creation time: <see cref="ExceptionGuardSimplifier" />
///     and <see cref="RedundantBitCastElisionRewriter" /> both still pattern-match on
///     <see cref="ThrowExpressionSyntax" /> directly (guard collapsing / numeric-operand proof for
///     conditional branches), so extracting any earlier would silently stop them from firing. The
///     extracted helper is generic (<c>T Throw...&lt;T&gt;(string message)</c>) rather than <c>void</c>,
///     because a throw-expression only ever occupies a value position (<c>cond ? a : throw ...</c>,
///     <c>x ?? throw ...</c>) — a <c>void</c> call cannot legally sit there.
///   </para>
///   <para>
///     The type argument comes from the annotation, not from re-deriving it here: by the time this pass
///     runs, the throw node may sit anywhere (inlined into a `return`, buried in an arithmetic chain), so
///     the only reliable source for "what type does this branch need to be" is whatever the optimizer that
///     built the throw already knew at creation time.
///   </para>
/// </summary>
public sealed class ThrowExpressionExtractionRewriter(IDictionary<SyntaxNode, bool> additionalMethods, ISet<string> usings) : CSharpSyntaxRewriter
{
	public const string ExpectedTypeAnnotationKind = "ConstExpr_ThrowExpectedType";

	public static SyntaxNode Apply(SyntaxNode node, IDictionary<SyntaxNode, bool> additionalMethods, ISet<string> usings)
	{
		return new ThrowExpressionExtractionRewriter(additionalMethods, usings).Visit(node);
	}

	public override SyntaxNode? VisitThrowExpression(ThrowExpressionSyntax node)
	{
		var expectedTypeName = node.GetAnnotations(ExpectedTypeAnnotationKind).FirstOrDefault()?.Data;

		if (expectedTypeName is null || node.Expression is not ObjectCreationExpressionSyntax { ArgumentList.Arguments: [ var messageArg ] } creationExpression)
		{
			return base.VisitThrowExpression(node);
		}

		var methodName = $"Throw{creationExpression.Type.TryGetInferredMemberName()}";

		// A throw normally sits in a value position (cond ? a : throw ...), which is why the helper is
		// generic — but the reflection-fallback callers of CreateThrowExpression (BaseLinqFunctionOptimizer,
		// TryExecuteWithConstantArguments/TryExecutePredicates) can be optimizing a void-returning LINQ
		// method, where the throw instead replaces the whole invocation and is used as a statement. `void`
		// cannot be a generic type argument (CS0673), so that case gets a plain, non-generic helper instead.
		var isVoid = expectedTypeName == "void";

		TypeSyntax returnType = isVoid ? PredefinedType(Token(SyntaxKind.VoidKeyword)) : IdentifierName("T");

		var method = MethodDeclaration(returnType, methodName)
			.WithTypeParameterList(isVoid ? null : TypeParameterList(SingletonSeparatedList(TypeParameter("T"))))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
			.WithAttributeLists(List(
			[
				AttributeList(SingletonSeparatedList(
					Attribute(ParseName("DoesNotReturn")))),
				AttributeList(SingletonSeparatedList(
					Attribute(ParseName("MethodImpl"))
						.WithArgumentList(AttributeArgumentList(SingletonSeparatedList(
							AttributeArgument(ParseName("MethodImplOptions.NoInlining")))))))
			]))
			.WithParameterList(ParameterList(SingletonSeparatedList(
				Parameter(Identifier("message")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))))
			.WithExpressionBody(ArrowExpressionClause(ThrowExpression(creationExpression.WithArgumentList(
				ArgumentList(SingletonSeparatedList(Argument(IdentifierName("message"))))))))
			.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

		additionalMethods.TryAdd(method, false);
		usings.Add("System.Diagnostics.CodeAnalysis");
		usings.Add("System.Runtime.CompilerServices");

		var target = isVoid
			? (SimpleNameSyntax) IdentifierName(methodName)
			: GenericName(Identifier(methodName)).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(ParseTypeName(expectedTypeName))));

		return InvocationExpression(target)
			.WithArgumentList(ArgumentList(SingletonSeparatedList(messageArg)));
	}
}