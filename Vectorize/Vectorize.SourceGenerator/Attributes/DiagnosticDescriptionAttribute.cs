using System;

namespace Vectorize.Attributes;

public class DiagnosticDescriptionAttribute(string description) : Attribute
{
	public string Description { get; } = description;
}