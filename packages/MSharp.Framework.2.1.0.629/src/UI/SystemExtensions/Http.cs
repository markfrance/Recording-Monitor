namespace System
{
    using System.Linq;
    using System.Reflection;
    using System.Web.UI;
    using IO;
    using IO.Compression;
    using MSharp.Framework;
    using Web;

    partial class MSharpExtensions
    {
        /// <summary>
        /// Determines whether this is an Ajax call.
        /// </summary>
        public static bool IsAjaxCall(this HttpRequest request)
        {
            return request["X-Requested-With"] == "XMLHttpRequest" || (request.Headers["X-Requested-With"] == "XMLHttpRequest");
        }

        /// <summary>
        /// Dispatches a binary data block back to the client.
        /// </summary>
        public static void Dispatch(this HttpResponse response, byte[] responseData, string fileName, string contentType = "Application/octet-stream", bool endResponse = true)
        {
            if (responseData == null)
                throw new ArgumentNullException("responseData");

            if (fileName.IsEmpty())
                throw new ArgumentNullException("fileName");

            response.Clear();
            response.ContentType = contentType;

            response.AddHeader("Content-Disposition", "attachment; filename=\"{0}\"".FormatWith(fileName.Replace("\"", "").Replace(",", "-")));

            response.BinaryWrite(responseData);
            response.Flush();

            if (endResponse)
                response.End();
        }

        /// <summary>
        /// Dispatches a string back to the client as a file.
        /// </summary>
        public static void Dispatch(this HttpResponse response, string responseText, string fileName, string contentType = "Application/octet-stream", bool endResponse = true, System.Text.Encoding encoding = null)
        {
            response.Clear();

            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("Pragma", "no-cache");

            if (fileName.HasValue())
                response.AddHeader("Content-Disposition", "attachment;filename=" + fileName.Replace(" ", "_") + "");

            response.ContentType = contentType;

            if (encoding != null)
            {
                response.BinaryWrite(responseText.ToBytesWithSignature(encoding));
            }
            else
            {
                response.Write(responseText);
            }

            response.Flush();

            if (endResponse)
                response.End();
        }

        /// <summary>
        /// Dispatches a file back to the client.
        /// </summary>
        /// <param name="fileName">If set to null, the same file name of the file will be used.</param>
        public static void Dispatch(this HttpResponse response, FileInfo responseFile, string fileName = null, string contentType = "Application/octet-stream", bool endResponse = true)
        {
            if (responseFile == null)
                throw new ArgumentNullException("responseFile");

            if (fileName.IsEmpty())
                fileName = responseFile.Name;

            response.Clear();
            response.ContentType = contentType;

            response.AddHeader("Content-Disposition", "attachment; filename=\"{0}\"".FormatWith(fileName.Replace("\"", "").Replace(",", "-")));

            response.TransmitFile(responseFile.FullName);
            response.Flush();

            if (endResponse)
                response.End();
        }

        /// <summary>
        /// Dispatches a file back to the client.
        /// </summary>
        public static void Dispatch(this HttpResponse response, Document document, string contentType = "Application/octet-stream", bool endResponse = true)
        {
            Dispatch(response, File.ReadAllBytes(document.LocalPath), document.FileName, contentType, endResponse);
        }

        /// <summary>
        /// If the request accepts encoding of GZIP then it will compress the response.
        /// </summary>
        public static void SupportGZip(this HttpContext context)
        {
            if (context.Request.Headers["Accept-Encoding"].Or("").Contains("gzip", caseSensitive: false))
            {
                try
                {
                    context.Response.Filter = context.Response.Filter; // ASP.NET bug. It should be read first!
                    context.Response.Filter = new GZipStream(context.Response.Filter, CompressionMode.Compress);
                    context.Response.Headers.Remove("Content-Encoding");
                    context.Response.AppendHeader("Content-Encoding", "gzip");
                }
                catch
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Determines if this is a GET http request.
        /// </summary>
        public static bool IsGet(this HttpRequest request)
        {
            return request.HttpMethod == "GET";
        }

        /// <summary>
        /// Determines if this is a POST http request.
        /// </summary>
        public static bool IsPost(this HttpRequest request)
        {
            return request.HttpMethod == "POST";
        }

        /// <summary>
        /// Gets the currently specified return URL.
        /// </summary>
        public static string GetReturnUrl(this HttpRequest request)
        {
            var result = request["ReturnUrl"];

            if (result.IsEmpty()) return string.Empty;

            if (result.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Invalid ReturnUrl.");

            if (result.ToCharArray().ContainsAny('\'', '\"', '>', '<'))
                throw new Exception("Invalid ReturnUrl.");

            if (result.ContainsAny(new[] { "//", ":" }, caseSensitive: false))
                throw new Exception("Invalid ReturnUrl.");

            return result;
        }

        /// <summary>
        /// Determines whether this is the specified browser and any of the specified major versions.
        /// </summary>        
        public static bool IsBrowser(this HttpBrowserCapabilities browser, string browserName, params int[] majorVersions)
        {
            return browser.IsBrowser(browserName) && majorVersions.Contains(browser.MajorVersion);
        }

        /// <summary>
        /// Adds Javascript code to safely set focus on the specified control.
        /// </summary>
        public static void Focus(this Web.UI.Control control, bool safe)
        {
            if (!safe) control.Focus();

            var script =
                @"setTimeout(function() {                     
                    var fn = function() { 
                        var control = $get('#CLIENT_ID#'); 
                        if (control && control.focus) { try { control.focus(); } catch (err) { } }
                        try{ Sys.Application.remove_load(fn); } catch (err) { }
                    };
                    Sys.Application.add_load(fn); fn();
                }, 10);".Replace("#CLIENT_ID#", control.ClientID);

            ScriptManager.RegisterStartupScript(control.Page, control.GetType(), control.ClientID + "_SetFocusOnLoad", script, addScriptTags: true);
        }

        /// <summary>
        /// Writes the specified message in the response and then ends the response.
        /// </summary>
        public static void EndWith(this HttpResponse response, string message, string mimeType = "text/html")
        {
            response.ContentType = mimeType;
            response.Write(message);
            response.End();
        }

        /// <summary>
        /// Injects a file into this http file collection.
        /// </summary>
        public static HttpPostedFile InjectFile(this HttpFileCollection files, string fileKey, byte[] data, string filename, string contentType)
        {
            if (filename == null) throw new ArgumentNullException("fileName cannot be null.");

            var systemWebAssembly = typeof(HttpPostedFileBase).Assembly;

            var typeHttpRawUploadedContent = systemWebAssembly.GetType("System.Web.HttpRawUploadedContent");
            var typeHttpInputStream = systemWebAssembly.GetType("System.Web.HttpInputStream");

            var uploadedParams = new[] { typeof(int), typeof(int) };
            var streamParams = new[] { typeHttpRawUploadedContent, typeof(int), typeof(int) };
            var parameters = new[] { typeof(string), typeof(string), typeHttpInputStream };

            var uploadedContent = typeHttpRawUploadedContent
              .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, uploadedParams, null)
              .Invoke(new object[] { data.Length, data.Length });

            typeHttpRawUploadedContent.GetMethod("AddBytes", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(uploadedContent, new object[] { data, 0, data.Length });

            var stream = (Stream)typeHttpInputStream
              .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, streamParams, null)
              .Invoke(new object[] { uploadedContent, 0, data.Length });

            uploadedContent.GetType().GetField("_completed", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(uploadedContent, true);

            var postedFile = typeof(HttpPostedFile).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First()
              .Invoke(new object[] { filename, contentType, stream }) as HttpPostedFile;

            files.RemoveFile(fileKey);

            var addMethod = typeof(HttpFileCollection).GetMethod("AddFile", BindingFlags.NonPublic | BindingFlags.Instance);

            addMethod.Invoke(files, new object[] { fileKey, postedFile });

            return postedFile;
        }

        /// <summary>
        /// Reads the full content of a posted text file.
        /// </summary>
        public static string ReadAllText(this HttpPostedFile file)
        {
            file.InputStream.Position = 0;
            using (var reader = new StreamReader(file.InputStream))
            {
                var result = reader.ReadToEnd();

                file.InputStream.Position = 0;

                return result;
            }
        }

        /// <summary>
        /// Removes a file from this http file collection with the specified key, if it exists.
        /// </summary>
        public static void RemoveFile(this HttpFileCollection files, string fileKey)
        {
            if (files.AllKeys.Contains(fileKey))
            {
                var method = typeof(HttpFileCollection).GetMethod("BaseRemove", BindingFlags.NonPublic | BindingFlags.Instance);

                method.Invoke(files, new object[] { fileKey });
            }
        }
    }
}