using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
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

			x.AddSource("OptimizeAttribute.g", """
					using System;
					
					[AttributeUsage(AttributeTargets.Method, Inherited = false)]
					public class OptimizeAttribute : Attribute;
					""");
		});
	}

	private string? GenerateSource(GeneratorAttributeSyntaxContext context, CancellationToken token)
	{
		if (context.TargetNode is not MethodDeclarationSyntax methodDeclaration)
		{
			return null;
		}
		
		var optimizer = new OptimizeRewriter(context.SemanticModel, methodDeclaration, token);
		var rewriter = new VectorizeRewriter(context.SemanticModel, methodDeclaration, token);
		
		var optimized = optimizer.Visit(methodDeclaration.Body);
		var result = rewriter.Visit(optimized);

		var optimizedString = optimized.NormalizeWhitespace("\t", false).ToFullString();
		var resultString = result.NormalizeWhitespace("\t", false).ToFullString();

		return String.Empty;
	}
}