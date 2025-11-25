namespace ConstExpr.Tests.String;

public class ReverseStringTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var chars = s.ToCharArray();
    //var left = 0;
    //var right = chars.Length - 1;
    //while (left < right)
    //{
    //	var temp = chars[left];
    //	chars[left] = chars[right];
    //	chars[right] = temp;
    //	left++;
    //	right--;
    //}
    
    //return new string(chars);
    //""",
    "return \"olleh\";",
    "return new string([]);",
    "return \"a\";"
  ];

  public override string Invocations => """
    var varStr = "test";
    TestMethods.ReverseString("hello");  // "olleh"
    TestMethods.ReverseString("");       // ""
    TestMethods.ReverseString("a");      // "a"
    TestMethods.ReverseString(varStr);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static string ReverseString(string s)
    {
      var chars = s.ToCharArray();
      var left = 0;
      var right = chars.Length - 1;
      
      while (left < right)
      {
        var temp = chars[left];
        chars[left] = chars[right];
        chars[right] = temp;
        left++;
        right--;
      }
      
      return new string(chars);
    }
    """;
}

