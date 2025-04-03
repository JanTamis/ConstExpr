using ConstExpr.SourceGenerator.Sample;
using System;
using System.Text;

Console.WriteLine(Test.IsOdd(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.StdDev(1f, 2f, 3f, 4f, 5f));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
Console.WriteLine(Test.StringBytes("Hello, World!", Encoding.UTF8).Length);
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(await Test.Waiting());
Console.WriteLine(String.Join(", ", Test.Range(8)));
// Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
Console.WriteLine(Test.RgbToHsl(150, 100, 50));

Console.WriteLine(Test.Range(8).CommonPrefixLength([1, 2, 3, 4, 5]));