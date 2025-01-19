using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SGF;
using Vectorize.Rewriters;

namespace Vectorize;

[IncrementalGenerator]
public class VectorizeSourceGenerator() : IncrementalGenerator("Vectorize")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(x =>
		{
			var method = context.SyntaxProvider.ForAttributeWithMetadataName("VectorizeAttribute", (node, token) => !token.IsCancellationRequested, GenerateSource);

			context.RegisterSourceOutput(method, static (spc, source) =>
			{
				
			});
			
			x.AddSource("VectorizeAttribute.g", """
					using System;
					
					[AttributeUsage(AttributeTargets.Method, Inherited = false)]
					public class VectorizeAttribute : Attribute;
					""");
		});
	}

	private string? GenerateSource(GeneratorAttributeSyntaxContext context, CancellationToken token)
	{
		if (context.TargetNode is not MethodDeclarationSyntax methodDeclaration)
		{
			return null;
		}
		
		var rewriter = new VectorizeRewriter(context.SemanticModel, token);
		var result = rewriter.Visit(methodDeclaration.Body).NormalizeWhitespace("\t", true);
		
		var resultString = result.ToFullString();

		return String.Empty;
	}
}