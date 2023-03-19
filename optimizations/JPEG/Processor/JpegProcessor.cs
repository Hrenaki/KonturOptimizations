using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using JPEG.Images;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcessor : IJpegProcessor
{
    public static readonly JpegProcessor Init = new();
    public const int CompressionQuality = 70;
    private const int DCTSize = 8;

    public void Compress(string imagePath, string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        var imageMatrix = Matrix.FromBitmap(bmp);
        //Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
        var compressionResult = Compress(imageMatrix, CompressionQuality);
        compressionResult.Save(compressedImagePath);
    }

    public void Uncompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);
        var uncompressedImage = Uncompress(compressedImage);
        var resultBmp = uncompressedImage.ToBitmap();
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private static CompressedImage Compress(Matrix matrix, int quality = 50)
    {
        var dctSizeCountInRow = matrix.Width / DCTSize;
        var dctSizeCountInColumn = matrix.Height / DCTSize;

        var allQuantizedBytes = new byte[dctSizeCountInRow * dctSizeCountInColumn * 3 * DCTSize * DCTSize];

        var channelShift = 128;
        var channelFunctions = new Func<Pixel, double>[] { p => p.Y - channelShift, p => p.Cb - channelShift, p => p.Cr - channelShift };

        var quantizationMatrix = GetQuantizationMatrix(quality);

        Parallel.For(0, dctSizeCountInColumn, i =>
        {
            var y = i * DCTSize;
            var subMatrix = new double[DCTSize, DCTSize];
            var doubleMatrixBuffer = new double[DCTSize, DCTSize];
            var byteMatrixBuffer = new byte[DCTSize, DCTSize];

            for (var j = 0; j < dctSizeCountInRow; j++)
            {
                var x = j * DCTSize;
                var k = 0;
                foreach (var channelFunction in channelFunctions)
                {
                    GetSubMatrix(matrix, y, DCTSize, x, DCTSize, channelFunction, subMatrix);
                    DCT.DCT2D(subMatrix, doubleMatrixBuffer);
                    Quantize(doubleMatrixBuffer, quantizationMatrix, byteMatrixBuffer);
                    ZigZagScan(byteMatrixBuffer, allQuantizedBytes.AsSpan((i * dctSizeCountInRow * 3 + j * 3 + k) * DCTSize * DCTSize, DCTSize * DCTSize));

                    k++;
                }
            }
        });

        var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

        return new CompressedImage
        {
            Quality = quality,
            CompressedBytes = compressedBytes,
            BitsCount = bitsCount,
            DecodeTable = decodeTable,
            Height = matrix.Height,
            Width = matrix.Width
        };
    }

    private static Matrix Uncompress(CompressedImage image)
    {
        var pixels = new Pixel[image.Height, image.Width];

        var allQuantizedBytes = HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount);
        var quantizationMatrix = GetQuantizationMatrix(image.Quality);

        Parallel.For(0, image.Height / DCTSize, i =>
        {
            var y = i * DCTSize;

            var doubleMatrixBuffer = new double[DCTSize, DCTSize];
            var byteMatrixBuffer = new byte[DCTSize, DCTSize];

            var _y = new double[DCTSize, DCTSize];
            var cb = new double[DCTSize, DCTSize];
            var cr = new double[DCTSize, DCTSize];
            var channels = new[] { _y, cb, cr };

            for (var j = 0; j < image.Width / DCTSize; j++)
            {
                var x = j * DCTSize;
                var k = 0;
                foreach (var channel in channels)
                {
                    var quantizedBytes = new Span<byte>(allQuantizedBytes, (i * image.Width / DCTSize * 3 + j * 3 + k) * DCTSize * DCTSize, DCTSize * DCTSize);
                    byteMatrixBuffer = ZigZagUnScan(quantizedBytes, byteMatrixBuffer);
                    DeQuantize(byteMatrixBuffer, quantizationMatrix, doubleMatrixBuffer);
                    DCT.IDCT2D(doubleMatrixBuffer, channel);
                    ShiftMatrixValues(channel, 128);

                    k++;
                }

                SetPixels(pixels, _y, cb, cr, PixelFormat.YCbCr, y, x);
            }
        });

        return new Matrix(pixels);
    }

    private static unsafe void ShiftMatrixValues(double[,] subMatrix, int shiftValue)
    {
        var height = subMatrix.GetLength(0);
        var width = subMatrix.GetLength(1);
        var length = subMatrix.GetLength(0) * subMatrix.GetLength(1);

        fixed (double* ptr = subMatrix)
        {
            for (var i = 0; i < length; i++)
                *(ptr + i) += shiftValue;
        }
    }

    private static unsafe void SetPixels(Pixel[,] pixels, double[,] a, double[,] b, double[,] c, PixelFormat format,
        int yOffset, int xOffset)
    {
        var pixelMatrixWidth = pixels.GetLength(1);

        var height = a.GetLength(0);
        var width = a.GetLength(1);

        var yMatrixIndex = 0;
        var channelItemYIndex = 0;

        fixed (Pixel* pixelPtr = pixels)
        fixed (double* firstChannelPtr = a)
        fixed (double* secondChannelPtr = b)
        fixed (double* thirdChannelPtr = c)
        {
            for (var y = 0; y < height; y++)
            {
                yMatrixIndex = (yOffset + y) * pixelMatrixWidth;
                channelItemYIndex = y * width;

                for (var x = 0; x < width; x++)
                {
                    var index = pixelPtr + yMatrixIndex + xOffset + x;
                    var channelItemIndex = channelItemYIndex + x;
                    *index = new Pixel(*(firstChannelPtr + channelItemIndex), *(secondChannelPtr + channelItemIndex), *(thirdChannelPtr + channelItemIndex), format);
                }
            }
        }
    }

    private static unsafe void GetSubMatrix(Matrix matrix, int yOffset, int yLength, int xOffset, int xLength,
        Func<Pixel, double> componentSelector,
        double[,] output)
    {
        var pixels = matrix.Pixels;
        var pixelMatrixWidth = pixels.GetLength(1);

        var yMatrixIndex = 0;
        var ySubMatrixIndex = 0;

        fixed (Pixel* pixelPtr = pixels)
        fixed(double* outputPtr  = output)
        {
            for (var j = 0; j < yLength; j++)
            {
                yMatrixIndex = (yOffset + j) * pixelMatrixWidth;
                ySubMatrixIndex = j * xLength;

                for (var i = 0; i < xLength; i++)
                {
                    *(outputPtr + ySubMatrixIndex + i) = componentSelector(*(pixelPtr + yMatrixIndex + (xOffset + i)));
                }
            }
        }
    }

    private static readonly int[] ZigZagIndices = new[]
    {
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    };

    private static unsafe void ZigZagScan(byte[,] channelFreqs, Span<byte> output)
    {
        fixed(int* zigZagIndicesPtr = ZigZagIndices)
        fixed (byte* channelFreqsPtr = channelFreqs)
        fixed(byte* outputPtr = output)
        {
            for (var i = 0; i < output.Length; i++)
            {
                *(outputPtr + i) = *(channelFreqsPtr + *(zigZagIndicesPtr + i));
            }
        }
    }

    private static unsafe byte[,] ZigZagUnScan(Span<byte> quantizedBytes, byte[,] output)
    {
        //var height = output.GetLength(0);
        //var width = output.GetLength(1);
        //
        //fixed(byte* outputPtr = output)
        //fixed(byte* quantizedBytesPtr = quantizedBytes)
        //{
        //    for(var j = 0; j < height; j++)
        //    {
        //        for (var i = 0; i < width; i++)
        //        {
        //            var offset = j * width + i;
        //            *(outputPtr + offset) = *(quantizedBytesPtr + offset);
        //        }
        //    }
        //}
        return new[,]
        {
            {
                quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14],
                quantizedBytes[15], quantizedBytes[27], quantizedBytes[28]
            },
            {
                quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16],
                quantizedBytes[26], quantizedBytes[29], quantizedBytes[42]
            },
            {
                quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25],
                quantizedBytes[30], quantizedBytes[41], quantizedBytes[43]
            },
            {
                quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31],
                quantizedBytes[40], quantizedBytes[44], quantizedBytes[53]
            },
            {
                quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39],
                quantizedBytes[45], quantizedBytes[52], quantizedBytes[54]
            },
            {
                quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46],
                quantizedBytes[51], quantizedBytes[55], quantizedBytes[60]
            },
            {
                quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50],
                quantizedBytes[56], quantizedBytes[59], quantizedBytes[61]
            },
            {
                quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57],
                quantizedBytes[58], quantizedBytes[62], quantizedBytes[63]
            }
        };
    }

    private static unsafe void Quantize(double[,] channelFreqs, int[,] quantizationMatrix, byte[,] output)
    {
        var length = channelFreqs.GetLength(0) * channelFreqs.GetLength(1);

        fixed (double* channelFreqsPtr = channelFreqs)
        fixed (int* quantizationMatrixPtr = quantizationMatrix)
        fixed (byte* outputPtr = output)
        {
            unchecked
            {
                for (var i = 0; i < length; i++)
                {
                    *(outputPtr + i) = (byte)(*(channelFreqsPtr + i) / *(quantizationMatrixPtr + i));
                }
            }
        }
    }

    private static void DeQuantize(byte[,] quantizedBytes, int[,] quantizationMatrix, double[,] output)
    {
        var height = quantizedBytes.GetLength(0);
        var width = quantizedBytes.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                output[y, x] =
                        ((sbyte)quantizedBytes[y, x]) *
                   quantizationMatrix[y, x]; //NOTE cast to sbyte not to loose negative numbers
            }
        }
    }


    private static readonly int[,] QuantizationBaseMatrix = new[,]
    {
        { 16, 11, 10, 16, 24, 40, 51, 61 },
        { 12, 12, 14, 19, 26, 58, 60, 55 },
        { 14, 13, 16, 24, 40, 57, 69, 56 },
        { 14, 17, 22, 29, 51, 87, 80, 62 },
        { 18, 22, 37, 56, 68, 109, 103, 77 },
        { 24, 35, 55, 64, 81, 104, 113, 92 },
        { 49, 64, 78, 87, 103, 121, 120, 101 },
        { 72, 92, 95, 98, 112, 100, 103, 99 }
    };

    private static unsafe int[,] GetQuantizationMatrix(int quality)
    {
        if (quality < 1 || quality > 99)
            throw new ArgumentException("quality must be in [1,99] interval");

        var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

        var result = new int[8, 8];

        fixed(int* quantizationMatrixPtr = QuantizationBaseMatrix)
        fixed(int* resultPtr = result)
        {
            for(var i = 0; i < 64; i++)
            {
                *(resultPtr + i) = (multiplier * *(quantizationMatrixPtr + i) + 50) / 100;
            }
        }

        return result;
    }

    // 0,  1,  5,  6, 14, 15, 27, 28,
    // 2,  4,  7, 13, 16, 26, 29, 42,
    // 3,  8, 12, 17, 25, 30, 41, 43,
    // 9, 11, 18, 24, 31, 40, 44, 53,
    //10, 19, 23, 32, 39, 45, 52, 54,
    //20, 22, 33, 38, 46, 51, 55, 60,
    //21, 34, 37, 47, 50, 56, 59, 61,
    //35, 36, 48, 49, 57, 58, 62, 63
}