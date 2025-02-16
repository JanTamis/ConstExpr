using System;

namespace Vectorize.Attributes;

public class DiagnosticIdAttribute(string id) : Attribute
{
	public string Id { get; } = id;
}