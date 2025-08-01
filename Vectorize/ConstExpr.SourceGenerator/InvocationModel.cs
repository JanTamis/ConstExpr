using ConstExpr.SourceGenerator.Enums;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConstExpr.SourceGenerator;

[DebuggerDisplay("{Method.ToString()}")]
public class InvocationModel
{
#pragma warning disable RSEXPERIMENTAL002
	public InterceptableLocation? Location { get; set; }
#pragma warning restore RSEXPERIMENTAL002

	public MethodDeclarationSyntax Method { get; set; }

	public InvocationExpressionSyntax Invocation { get; set; }

	public object? Value { get; set; }

	public HashSet<string> Usings { get; set; }

	public IReadOnlyDictionary<SyntaxNode, Exception> Exceptions { get; set; }

	public GenerationLevel GenerationLevel { get; set; } = GenerationLevel.Balanced;
}