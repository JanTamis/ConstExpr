using System;
using Vectorize.Sample;

const float Item = 5f;

var sum = Test.Sum([ 1f, 2f, 3f, 4f, 5f ], Item);
// var average = Test.Average([ 1f, 2f, 3f, 4f, 5f ]);

Console.WriteLine(sum);
// Console.WriteLine(average);