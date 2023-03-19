using System;
using System.Drawing;
using System.Threading.Tasks;

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

    public Matrix(Pixel[,] pixels)
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

        fixed (Pixel* pixelMatrixPtr = pixelMatrix)
        {
            for (var j = 0; j < height; j++)
                for (var i = 0; i < width; i++, ptr += 3)
                {
                    var index = j * width + i;
                    *(pixelMatrixPtr + index) = new Pixel(*(ptr + 2), *(ptr + 1), *ptr, PixelFormat.RGB);
                }
        }
        return new Matrix(pixelMatrix);
    }

    public unsafe Bitmap ToBitmap()
    {
        var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        var data = bitmap.LockBits(new Rectangle(new Point(), new Size(Width, Height)),
                                   System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                   System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var ptr = (byte*)data.Scan0.ToPointer();

        for (var j = 0; j < Height; j++)
            for (var i = 0; i < Width; i++, ptr += 3)
            {
                var pixel = Pixels[j, i];
                *ptr = ToByte(pixel.B);
                *(ptr + 1) = ToByte(pixel.G);
                *(ptr + 2) = ToByte(pixel.R);
            }

        bitmap.UnlockBits(data);
        return bitmap;
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

    public static byte ToByte(double d)
    {
        var val = (int)d;
        if (val > byte.MaxValue)
            return byte.MaxValue;
        if (val < byte.MinValue)
            return byte.MinValue;
        return (byte)val;
    }
}