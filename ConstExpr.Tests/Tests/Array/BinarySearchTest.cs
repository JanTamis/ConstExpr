namespace ConstExpr.Tests.Array;

public class BinarySearchTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 2;",
    "return 4;",
    "return -1;"
  ];

  public override string Invocations => """
    var local = 9;
    TestMethods.BinarySearch(new[]{1,3,5,7,9}, 5); // index 2
    TestMethods.BinarySearch(new[]{2,4,6,8}, 5); // not found
    TestMethods.BinarySearch(new[]{0,2,4,6,8,10}, 8); // index 4
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int BinarySearch(int[] arr, int target)
    {
      var left = 0;
      var right = arr.Length - 1;
      while (left <= right)
      {
        var mid = left + ((right - left) >> 1);
        var current = arr[mid];
        if (current == target)
        {
          return mid;
        }
        if (current < target)
        {
          left = mid + 1;
        }
        else
        {
          right = mid - 1;
        }
      }
      return -1;
    }
    """;
}
