using System;
using System.Collections.Immutable;
using System.Text;
using Vectorize.Sample;

Console.WriteLine(Test.Sum(new[] { 1f, 2f, 3f, 4f, 5f }));
Console.WriteLine(Test.Average(ImmutableArray.Create(1f, 2f, 3f, 4f, 5f)));
Console.WriteLine(Test.StdDev(ImmutableArray.Create(1f, 2f, 3f, 4f, 5f)));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8).ToString());
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(Test.Base64Encode("Hello, World!"));