using System;

namespace ConstExpr.SourceGenerator.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticCategoryAttribute(string category) : Attribute
{
	public string Category { get; } = category;
}