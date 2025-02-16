using System;
using System.Text;
using Vectorize.Sample;

Console.WriteLine(Test.Sum([ 1f, 2f, 3f, 4f, 5f ]));
Console.WriteLine(Test.Average([ 1f, 2f, 3f, 4f, 5f ]));
Console.WriteLine(Test.StdDev([ 1f, 2f, 3f, 4f, 5f ]));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8).ToString());
Console.WriteLine(Test.Base64Encode("Hello, World!"));