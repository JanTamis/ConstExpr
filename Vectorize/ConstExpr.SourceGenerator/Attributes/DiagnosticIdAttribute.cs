using System;

namespace ConstExpr.SourceGenerator.Attributes;

public class DiagnosticIdAttribute(string id) : Attribute
{
	public string Id { get; } = id;
}