using System;

namespace Vectorize.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticTitleAttribute(string title) : Attribute
{
	public string Title { get; } = title;
}