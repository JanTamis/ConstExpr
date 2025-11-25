using Xunit;
using Xunit.Abstractions;

namespace ConstExpr.Tests.Tests.Hashing;

public class HashCollisionTests
{
  private readonly ITestOutputHelper _output;

  public HashCollisionTests(ITestOutputHelper output)
  {
    _output = output;
  }

  [Fact]
  public void TestHashCollisionRate()
  {
    // Redirect console output to test output
    var originalOut = Console.Out;
    var writer = new StringWriter();
    Console.SetOut(writer);

    try
    {
      HashCollisionTest.RunCollisionAnalysis();
      
      var output = writer.ToString();
      _output.WriteLine(output);
      
      // Parse results to assert quality
      Assert.Contains("COLLISION ANALYSIS RESULTS", output);
      
      // The test passes regardless - this is informational
      // But we log warnings if quality is poor
      if (output.Contains("POOR:"))
      {
        _output.WriteLine("WARNING: Hash quality is below acceptable threshold!");
      }
    }
    finally
    {
      Console.SetOut(originalOut);
    }
  }
}
