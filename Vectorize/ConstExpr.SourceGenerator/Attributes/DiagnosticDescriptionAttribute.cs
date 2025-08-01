using System;

namespace ConstExpr.SourceGenerator.Attributes;

public class DiagnosticDescriptionAttribute(string description) : Attribute
{
	public string Description { get; } = description;
}