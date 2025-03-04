using System;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Attributes;

public class DiagnosticSeverityAttribute(DiagnosticSeverity severity) : Attribute
{
	public DiagnosticSeverity Severity { get; } = severity;
}