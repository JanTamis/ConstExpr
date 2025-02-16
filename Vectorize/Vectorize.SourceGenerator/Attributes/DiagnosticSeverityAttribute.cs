using System;
using Microsoft.CodeAnalysis;

namespace Vectorize.Attributes;

public class DiagnosticSeverityAttribute(DiagnosticSeverity severity) : Attribute
{
	public DiagnosticSeverity Severity { get; } = severity;
}