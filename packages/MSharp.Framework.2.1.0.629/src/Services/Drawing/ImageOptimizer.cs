namespace System.Drawing.Imaging
{
    using System;
    using System.Linq;
    using System.Drawing;
    using System.IO;

    /// <summary>
    /// A utility to resize and optimise image files.
    /// </summary>    
    public class ImageOptimizer
    {
        /// <summary>
        /// Creates a new instance of ImageOptimizer class with default settings.
        /// </summary>
        public ImageOptimizer() : this(900, 700, 80) { }

        /// <summary>
        /// Creates a new instance of ImageOptimizer class.
        /// </summary>		
        public ImageOptimizer(int maxWidth, int maxHeight, int quality)
        {
            MaximumWidth = maxWidth;
            MaximumHeight = maxHeight;
            Quality = quality;
            OutputFormat = ImageFormat.Jpeg;
        }

        public int MaximumWidth { get; set; }
        public int MaximumHeight { get; set; }
        public int Quality { get; set; }
        public ImageFormat OutputFormat { get; set; }

        /// <summary>
        /// Gets the available output image formats.
        /// </summary>
        public enum ImageFormat { Bmp = 0, Jpeg = 1, Gif = 2, Png = 4 }

        /// <summary>
        /// Applies the settings of this instance on a specified source image, and provides an output optimized/resized image.
        /// </summary>
        public Image Optimize(Image source)
        {
            // Calculate the suitable width and heigth for the output image:
            var width = source.Width;
            var height = source.Height;

            if (width > MaximumWidth)
            {
                height = (int)(height * (1.0 * MaximumWidth) / width);
                width = MaximumWidth;
            }

            if (height > MaximumHeight)
            {
                width = (int)(width * (1.0 * MaximumHeight) / height);
                height = MaximumHeight;
            }

            if (width == source.Width && height == source.Height)
                return source;

            var result = new Bitmap(width, height, source.PixelFormat);
            using (var gr = result.CreateGraphics())
            {
                gr.Clear(Color.Transparent);
                var srcRect = new Rectangle(0, 0, source.Width, source.Height);
                var desRect = new Rectangle(0, 0, width, height);
                gr.DrawImage(source, desRect, srcRect, GraphicsUnit.Pixel);
            }
            return result;
        }

        /// <summary>
        /// Optimizes the specified source image and returns the binary data of the output image.
        /// </summary>
        public byte[] Optimize(byte[] sourceData, bool toJpeg = true)
        {
            try
            {
                using (var source = BitmapHelper.FromBuffer(sourceData))
                {
                    var format = Imaging.ImageFormat.Jpeg;

                    if (!toJpeg)
                        if (new[] { Imaging.ImageFormat.Png, Imaging.ImageFormat.Gif }.Any(f => f.Equals(source.RawFormat)))
                        {
                            format = source.RawFormat;
                        }

                    return Optimize(source).ToBuffer(format);
                }
            }
            catch
            {
                return sourceData;
            }
        }

        /// <summary>
        /// Applies optimization settings on a a source image file on the disk and saves the output to another file with the specified path.
        /// </summary>
        public void Optimize(string souceImagePath, string optimizedImagePath)
        {
            if (!File.Exists(souceImagePath))
                throw new Exception("Could not find the file: " + souceImagePath);

            Image source;

            try
            {
                source = Image.FromFile(souceImagePath);
            }
            catch (Exception ex)
            {
                throw new Exception("Could not obtain bitmap data from the file: {0}.".FormatWith(souceImagePath), ex);
            }

            using (source)
            {
                using (var optimizedImage = Optimize(source))
                {
                    optimizedImage.Save(optimizedImagePath, GenerateCodecInfo(), GenerateEncoderParameters());
                }
            }
        }

        /// <summary>
        /// Applies optimization settings on a source image file.
        /// Please note that the original file data is lost (overwritten) in this overload.
        /// </summary>
        public void Optimize(string imagePath)
        {
            Optimize(imagePath, imagePath);
        }

        EncoderParameters GenerateEncoderParameters()
        {
            var result = new EncoderParameters(1);
            result.Param[0] = new EncoderParameter(Encoder.Quality, Quality);
            return result;
        }

        ImageCodecInfo GenerateCodecInfo()
        {
            return ImageCodecInfo.GetImageEncoders()[(int)OutputFormat];
        }
    }
}