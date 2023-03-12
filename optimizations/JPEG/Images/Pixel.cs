using System;
using System.Linq;

namespace JPEG.Images;

public class Pixel
{
	private readonly PixelFormat format;

	public Pixel(double firstComponent, double secondComponent, double thirdComponent, PixelFormat pixelFormat)
	{
		if(pixelFormat != PixelFormat.RGB && pixelFormat != PixelFormat.YCbCr)
         throw new FormatException("Unknown pixel format: " + pixelFormat);

		format = pixelFormat;
		this.firstComponent = firstComponent;
		this.secondComponent = secondComponent;
		this.thirdComponent = thirdComponent;
	}

	private readonly double firstComponent;
	private readonly double secondComponent;
	private readonly double thirdComponent;

	public double R => format == PixelFormat.RGB ? firstComponent : (298.082 * firstComponent + 408.583 * thirdComponent) / 256.0 - 222.921;

	public double G =>
		format == PixelFormat.RGB ? secondComponent : (298.082 * firstComponent - 100.291 * secondComponent - 208.120 * thirdComponent) / 256.0 + 135.576;

	public double B => format == PixelFormat.RGB ? thirdComponent : (298.082 * firstComponent + 516.412 * secondComponent) / 256.0 - 276.836;

	public double Y => format == PixelFormat.YCbCr ? firstComponent : 16.0 + (65.738 * firstComponent + 129.057 * secondComponent + 24.064 * thirdComponent) / 256.0;
	public double Cb => format == PixelFormat.YCbCr ? secondComponent : 128.0 + (-37.945 * firstComponent - 74.494 * secondComponent + 112.439 * thirdComponent) / 256.0;
	public double Cr => format == PixelFormat.YCbCr ? thirdComponent : 128.0 + (112.439 * firstComponent - 94.154 * secondComponent - 18.285 * thirdComponent) / 256.0;
}