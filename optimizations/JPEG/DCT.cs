using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	public static void DCT2D(double[,] input, double[,] output)
	{
		var height = input.GetLength(0);
		var width = input.GetLength(1);

      var beta = Beta(height, width);

		for(var u = 0; u < width; u++)
		{
         for (var v = 0; v < height; v++)
         {
            var sum = MathEx
               .SumByTwoVariables(
                  0, width,
                  0, height,
                  (x, y) => BasisFunction(input[x, y], u, v, x, y, height, width));
            output[u, v] = sum * beta * Alpha(u) * Alpha(v);
         }
      }
	}

	public static void IDCT2D(double[,] coeffs, double[,] output)
	{
	   var width = coeffs.GetLength(1);
		var height = coeffs.GetLength(0);
		var beta = Beta(height, width);

		for (var x = 0; x < width; x++)
		{
			for (var y = 0; y < height; y++)
			{
				var sum = MathEx
					.SumByTwoVariables(
						0, width,
						0, height,
						(u, v) =>
							BasisFunction(coeffs[u, v], u, v, x, y, height, width) *
							Alpha(u) * Alpha(v));

				output[x, y] = sum * beta;
			}
		}
	}

	public static double BasisFunction(double a, double u, double v, double x, double y, int height, int width)
	{
		var b = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * width));
		var c = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * height));

		return a * b * c;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double Alpha(int u)
	{
		if (u == 0)
			return 1 / Math.Sqrt(2);
		return 1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double Beta(int height, int width)
	{
		return 1d / width + 1d / height;
	}
}