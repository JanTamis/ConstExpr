using System.Runtime.CompilerServices;

namespace ConstExpr.Tests;

internal static class TestStartup
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		// TUnit's HTML reporter writes a 400 KB report after every test run, blocking process exit.
		// Telemetry (Microsoft.Testing.Extensions.Telemetry) also adds shutdown delay via network flush.
		// Disable both here so the fix applies to all runners (CLI, Rider Test Explorer, etc.).
		Environment.SetEnvironmentVariable("TUNIT_DISABLE_HTML_REPORTER", "true");
		Environment.SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
	}
}
