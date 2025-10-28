using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConstExpr.SourceGenerator;

[DebuggerDisplay("{Method?.ToString() ?? Invocation?.ToString()}")]
public class InvocationModel
{
#pragma warning disable RSEXPERIMENTAL002
	public InterceptableLocation? Location { get; set; }
#pragma warning restore RSEXPERIMENTAL002

	public MethodDeclarationSyntax? OriginalMethod { get; set; }
	public MethodDeclarationSyntax? Method { get; set; }
	public TypeDeclarationSyntax? ParentType { get; set; }
	
	public IMethodSymbol? MethodSymbol { get; set; }
	public ConstExprAttribute? AttributeData { get; set; }

	public InvocationExpressionSyntax? Invocation { get; set; }
	
	public IEnumerable<SyntaxNode>? AdditionalMethods { get; set; }

	// public object? Value { get; set; }

	public HashSet<string>? Usings { get; set; }

	public IReadOnlyDictionary<SyntaxNode, Exception>? Exceptions { get; set; }
	

	// public GenerationLevel GenerationLevel { get; set; } = GenerationLevel.Balanced;
}