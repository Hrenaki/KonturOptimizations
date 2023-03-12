using BenchmarkDotNet.Attributes;
using JPEG.Images;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPEG.Benchmarks.Benchmarks
{
   [MemoryDiagnoser]
   [SimpleJob(warmupCount: 2, iterationCount: 3)]
   public class BitmapLoadBenchmark
   {
      private static readonly string imagePath = @"sample.bmp";

      [Benchmark]
      public void ReadBitmapWithCast()
      {
         using var fileStream = File.OpenRead(imagePath);
         using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
         var imageMatrix = (Matrix)bmp;

         FakeMatrixSave(imageMatrix);
      }

      [Benchmark]
      public void ReadRawBytes()
      {
         using var fileStream = File.OpenRead(imagePath);
         using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
         var imageMatrix = Matrix.FromBitmap(bmp);

         FakeMatrixSave(imageMatrix);
      }

      private static void FakeMatrixSave(Matrix matrix)
      {
         File.WriteAllText("123.txt", FakeMatrixToString(matrix));
      }

      private static string FakeMatrixToString(Matrix matrix)
      {
         var sb = new StringBuilder();
         for (var j = 0; j < 3; j++)
         {
            sb.Append("byte;");
         }
         return sb.ToString();
      }
   }
}