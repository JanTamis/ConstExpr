using System;

namespace Vectorize.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticMessageFormatAttribute(string message) : Attribute
{
	public string Message { get; } = message;
}