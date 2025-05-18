using System;
using System.Collections.Generic;
using System.Diagnostics;
using ConstExpr.SourceGenerator.Enums;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator;

[DebuggerDisplay("{Method.ToString()}")]
public class InvocationModel : IEquatable<InvocationModel>
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

	public bool Equals(InvocationModel? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return Equals(Location, other.Location) && Method.IsEquivalentTo(other.Method);
	}

	public override bool Equals(object? obj)
	{
		if (obj is null)
		{
			return false;
		}

		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj.GetType() != GetType())
		{
			return false;
		}
		
		return Equals((InvocationModel) obj);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			var hashCode = (Location != null ? Location.GetHashCode() : 0);
			hashCode = (hashCode * 397) ^ Method.GetHashCode();
			return hashCode;
		}
	}

	public static bool operator ==(InvocationModel? left, InvocationModel? right)
	{
		return Equals(left, right);
	}

	public static bool operator !=(InvocationModel? left, InvocationModel? right)
	{
		return !Equals(left, right);
	}
}