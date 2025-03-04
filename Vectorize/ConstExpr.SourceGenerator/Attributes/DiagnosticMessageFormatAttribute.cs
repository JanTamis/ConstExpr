using System;

namespace ConstExpr.SourceGenerator.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticMessageFormatAttribute(string message) : Attribute
{
	public string Message { get; } = message;
}