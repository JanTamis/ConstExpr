namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayReverseTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // """
    // var left = 0;
    // var right = arr.Length - 1;
    // while (left < right)
    // {
    // 	var temp = arr[left];
    // 	arr[left] = arr[right];
    // 	arr[right] = temp;
    // 	left++;
    // 	right--;
    // }
    //
    // return arr;
    // """,
    "return [5, 4, 3, 2, 1];",
    "return [];",
    "return [42];"
  ];

  public override string Invocations => """
    var varArr = new[] { 1, 2, 3 };
    TestMethods.ArrayReverse(new[] { 1, 2, 3, 4, 5 });
    TestMethods.ArrayReverse(new int[] { });
    TestMethods.ArrayReverse(new[] { 42 });
    TestMethods.ArrayReverse(varArr);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int[] ArrayReverse(int[] arr)
    {
      var left = 0;
      var right = arr.Length - 1;
      
      while (left < right)
      {
        var temp = arr[left];
        arr[left] = arr[right];
        arr[right] = temp;
        left++;
        right--;
      }
      
      return arr;
    }
    """;
}

