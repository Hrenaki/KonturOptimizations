using System;
using System.Drawing;

namespace JPEG.Images;

public class Matrix
{
   public Pixel[,] Pixels { get; private init; }
   public int Height { get; private init; }
   public int Width { get; private init; }

   public Matrix(int height, int width)
   {
      Height = height;
      Width = width;

      Pixels = new Pixel[height, width];
      for (var i = 0; i < height; ++i)
         for (var j = 0; j < width; ++j)
            Pixels[i, j] = new Pixel(0, 0, 0, PixelFormat.RGB);
   }

   private Matrix(Pixel[,] pixels)
   {
      ArgumentNullException.ThrowIfNull(pixels);

      Height = pixels.GetLength(0);
      Width = pixels.GetLength(1);
      Pixels = pixels;
   }

   public unsafe static Matrix FromBitmap(Bitmap bmp)
   {
      var height = bmp.Height - bmp.Height % 8;
      var width = bmp.Width - bmp.Width % 8;

      var data = bmp.LockBits(new Rectangle(new Point(), new Size(bmp.Width, bmp.Height)),
                              System.Drawing.Imaging.ImageLockMode.ReadOnly,
                              System.Drawing.Imaging.PixelFormat.Format24bppRgb);
      var ptr = (byte*)data.Scan0.ToPointer();

      var pixelMatrix = new Pixel[height, width];
      var offset = bmp.Width % 8 * 3;

      for (var j = 0; j < height; j++)
         for (var i = 0; i < width; i++, ptr += 3)
            pixelMatrix[j, i] = new Pixel(*(ptr + 2), *(ptr + 1), *ptr, PixelFormat.RGB);

      return new Matrix(pixelMatrix);
   }

   public static explicit operator Matrix(Bitmap bmp)
   {
      var height = bmp.Height - bmp.Height % 8;
      var width = bmp.Width - bmp.Width % 8;
      var matrix = new Matrix(height, width);

      for (var j = 0; j < height; j++)
      {
         for (var i = 0; i < width; i++)
         {
            var pixel = bmp.GetPixel(i, j);
            matrix.Pixels[j, i] = new Pixel(pixel.R, pixel.G, pixel.B, PixelFormat.RGB);
         }
      }

      return matrix;
   }

   public static explicit operator Bitmap(Matrix matrix)
   {
      var bmp = new Bitmap(matrix.Width, matrix.Height);

      for (var j = 0; j < bmp.Height; j++)
      {
         for (var i = 0; i < bmp.Width; i++)
         {
            var pixel = matrix.Pixels[j, i];
            bmp.SetPixel(i, j, Color.FromArgb(ToByte(pixel.R), ToByte(pixel.G), ToByte(pixel.B)));
         }
      }

      return bmp;
   }

   public static int ToByte(double d)
   {
      var val = (int)d;
      if (val > byte.MaxValue)
         return byte.MaxValue;
      if (val < byte.MinValue)
         return byte.MinValue;
      return val;
   }
}