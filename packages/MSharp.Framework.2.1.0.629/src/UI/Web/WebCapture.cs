using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MSharp.Framework.Services
{   
    public enum WebCaptureOutputFormat
    {
        SVG,
        PS,
        PDF,
        ITEXT,
        HTML,
        RTREE,
        PNG,
        JPEG,
        MNG,
        TIFF,
        GIF,
        BMP,
        PPM,
        XBM,
        XPM
    }

    /// <summary>
    /// This class provides utilities to capture an image from a web url
    /// </summary>
    public class WebCapture
    {
        static string ExePath = AppDomain.CurrentDomain.BaseDirectory + "Content\\cutycapt.exe";

        /// <summary>
        /// Creates a new WebCapture instance.
        /// </summary>
        public WebCapture()
        {
            OutputFormat = WebCaptureOutputFormat.PNG;
            Javascript = true;
            MaxWait = 20000;
        }

        #region OutputFormat
        /// <summary>
        /// Output format default: PNG
        /// </summary>
        public WebCaptureOutputFormat OutputFormat { get; set; }
        #endregion

        #region MinWidth
        /// <summary>
        /// Gets or sets the MinWidth of this WebCapture.
        /// </summary>
        public int MinWidth { get; set; }
        #endregion

        #region MinHeight
        /// <summary>
        /// Gets or sets the MinHeight of this WebCapture.
        /// </summary>
        public int MinHeight { get; set; }
        #endregion

        #region Delay
        /// <summary>
        /// After successful load, wait X milliseconds (default: 0)
        /// </summary>
        public int Delay { get; set; }
        #endregion

        #region Javascript
        /// <summary>
        /// JavaScript execution (default: on)
        /// </summary>
        public bool Javascript { get; set; }
        #endregion

        #region MaxWait
        /// <summary>
        /// Maximum time in millisecond that the process should take to capture the snapshot.
        /// if ths time is passed the system would throw exception. DEFAULT: 20,000
        /// </summary>
        public int MaxWait { get; set; }
        #endregion


        /// <summary>
        /// Prepares arguments for capturing executable
        /// </summary>
        string GetArguments(string url, string filename)
        {
            return "--url={0} --out={1} --out-format={2}{3}{4}{5}{6}".FormatWith(
                url,
                filename,
                OutputFormat.ToString().ToLower(),
                " --min-width={0}".FormatWith(MinWidth).OnlyWhen(MinWidth > 0),
                " --min-height={0}".FormatWith(MinHeight).OnlyWhen(MinHeight > 0),
                " --delay={0}".FormatWith(Delay).OnlyWhen(Delay > 0),
                " --javascript={0}".FormatWith(Javascript?"on":"off")
                );
        }

        /// <summary>
        /// Gets the byte array of an image captured from the given url
        /// </summary>
        public byte[] Capture(string url)
        {
            return CaptureBitmap(url).ToBuffer();
        }

        /// <summary>
        /// Gets the bitmap captured from the given url
        /// To get the data you can call .ToBuffer(ImageFormat.Png).
        /// </summary>
        public Image CaptureBitmap(string url)
        {
            using (var temp = new TemporaryFilePath(".png"))
            {
                Capture(url, temp.FilePath);
                //return Bitmap.FromFile(temp.FilePath);

                using (var img = Bitmap.FromFile(temp.FilePath))
                {
                    return new Bitmap(img);
                }
            }
        }

        /// <summary>
        /// Capturs from the given url and stores it in the gievn file path
        /// </summary>
        public void Capture(string url, string filename)
        {
            if (File.Exists(ExePath) == false)
                throw new Exception("Web capture can not locate the executable file in: " + ExePath);

            url = url.TrimStart("http:").TrimStart("/");
            url = "http://" + url;
            var args = GetArguments(url, filename);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(ExePath, args),
            };
            process.Start();
            var finished = process.WaitForExit(MaxWait);

            if (finished == false)
                throw new Exception("Can not capture the given url: " + url);
        }
    }
}