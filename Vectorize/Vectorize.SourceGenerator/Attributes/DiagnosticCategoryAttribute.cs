using System;

namespace Vectorize.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticCategoryAttribute(string category) : Attribute
{
	public string Category { get; } = category;
}