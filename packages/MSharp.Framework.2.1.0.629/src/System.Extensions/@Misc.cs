namespace System
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Web;
    using Linq.Expressions;
    using MSharp.Framework.Data;
    using MSharp.Framework.UI;
    using Newtonsoft.Json;
    using Threading.Tasks;
    /// <summary>
    /// Provides extensions methods to Standard .NET types.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class MSharpExtensions
    {
        /// <summary>
        /// Gets the full path of a file or directory from a specified relative path.
        /// </summary>
        public static string GetPath(this AppDomain applicationDomain, params string[] relativePathSections)
        {
            var result = applicationDomain.BaseDirectory;

            foreach (var path in relativePathSections)
            {
                if (path.HasValue())
                    result = System.IO.Path.Combine(result, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
            }

            return result;
        }

        const int MAXIMUM_ATTEMPTS = 3;
        const int ATTEMPT_PAUSE = 50 /*Milisseconds*/;

        static T TryHard<T>(FileSystemInfo fileOrFolder, Func<T> action, string error)
        {
            var result = default(T);
            TryHard(fileOrFolder, delegate { result = action(); }, error);
            return result;
        }

        static void TryHard(FileSystemInfo fileOrFolder, Action action, string error)
        {
            var attempt = 0;

            Exception problem = null;

            while (attempt <= MAXIMUM_ATTEMPTS)
            {
                try
                {
                    action?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    problem = ex;

                    // Remove attributes:
                    try { fileOrFolder.Attributes = FileAttributes.Normal; }
                    catch { }

                    attempt++;

                    // Pause for a short amount of time (to allow a potential external process to leave the file/directory).
                    Thread.Sleep(ATTEMPT_PAUSE);
                }
            }

            throw new IOException(error.FormatWith(fileOrFolder.FullName), problem);
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            var result = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(result, 0, result.Length);

            return result;
        }

        public static byte[] DownloadData(this WebClient client, string address, bool handleGzip)
        {
            if (!handleGzip)
                return client.DownloadData(address);

            var result = client.DownloadData(address);
            if (result != null && result.Length > 3 && result[0] == 31 && result[1] == 139 && result[2] == 8)
            {
                // GZIP:
                using (var stream = new System.IO.Compression.GZipStream(new MemoryStream(result), System.IO.Compression.CompressionMode.Decompress))
                {
                    var buffer = new byte[4096];
                    using (var memory = new MemoryStream())
                    {
                        while (true)
                        {
                            var count = stream.Read(buffer, 0, 4096);
                            if (count > 0) memory.Write(buffer, 0, count);
                            else break;
                        }

                        return memory.ToArray();
                    }
                }
            }
            else
            {
                return result;
            }
        }

        /// <summary>
        /// Posts the specified data to a url and returns the response as string.
        /// All properties of the postData object will be sent as individual FORM parameters to the destination.
        /// </summary>
        /// <param name="postData">An anonymous object containing post data.</param>
        public static string Post(this WebClient webClient, string url, object postData)
        {
            if (postData == null)
                throw new ArgumentNullException("postData");

            var data = new Dictionary<string, string>();
            data.AddFromProperties(postData);

            return Post(webClient, url, data);
        }

        /// <summary>
        /// Gets the response data as string.
        /// </summary>
        public static string GetString(this WebResponse response)
        {
            using (var stream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Gets the response data as string.
        /// </summary>
        public static string GetResponseString(this HttpWebRequest request)
        {
            using (var response = request.GetResponse())
            {
                return response.GetString();
            }
        }

        /// <summary>
        /// Posts the specified object as JSON data to this URL.
        /// </summary>
        public static string PostJson(this Uri url, object data)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);

            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = "application/json";

            using (var stream = new StreamWriter(req.GetRequestStream()))
            {
                stream.Write(JsonConvert.SerializeObject(data));
            }

            return req.GetResponseString();
        }

        /// <summary>
        /// Posts the specified data to this url and returns the response as string.
        /// All items in the postData object will be sent as individual FORM parameters to the destination.
        /// </summary>
        public static string Post(this Uri url, object data, Action<WebClient> customiseClient = null)
        {
            using (var client = new WebClient())
            {
                customiseClient?.Invoke(client);

                return client.Post(url.ToString(), data);
            }
        }

        /// <summary>
        /// Posts the specified data to this url and returns the response as string.
        /// All items in the postData object will be sent as individual FORM parameters to the destination.
        /// </summary>
        public static string Post(this Uri url, Dictionary<string, string> postData, Action<WebClient> customiseClient = null)
        {
            using (var client = new WebClient())
            {
                customiseClient?.Invoke(client);
                return client.Post(url.ToString(), postData);
            }
        }

        /// <summary>
        /// Posts the specified data to a url and returns the response as string.
        /// All items in the postData object will be sent as individual FORM parameters to the destination.
        /// </summary>
        public static string Post(this WebClient webClient, string url, Dictionary<string, string> postData)
        {
            return Post(webClient, url, postData, Encoding.UTF8);
        }

        /// <summary>
        /// Posts the specified data to a url and returns the response as string.
        /// </summary>
        public static string Post(this WebClient webClient, string url, Dictionary<string, string> postData, Encoding responseEncoding)
        {
            if (responseEncoding == null)
                throw new ArgumentNullException("responseEncoding");

            if (postData == null)
                throw new ArgumentNullException("postData");

            if (url.IsEmpty())
                throw new ArgumentNullException("url");

            var responseBytes = webClient.UploadValues(url, postData.ToNameValueCollection());

            try
            {
                return responseEncoding.GetString(responseBytes);
            }
            catch (WebException ex)
            {
                throw new Exception(ex.GetResponseBody());
            }
        }

        /// <summary>
        /// Posts the specified data to a URL and returns the response as string asynchronously.
        /// </summary>
        /// <param name="postData">An anonymous object containing post data.</param>
        public static Task<string> PostAsync(this WebClient webClient, string url, object postData)
        {
            if (postData == null)
                throw new ArgumentNullException("postData");

            var data = new Dictionary<string, string>();
            data.AddFromProperties(postData);

            return PostAsync(webClient, url, data);
        }

        /// <summary>
        /// Posts the specified data to a URL and returns the response as string asynchronously.
        /// </summary>
        public static Task<string> PostAsync(this WebClient webClient, string url, Dictionary<string, string> postData)
        {
            return PostAsync(webClient, url, postData, Encoding.UTF8);
        }

        /// <summary>
        /// Posts the specified data to a URL and returns the response as string asynchronously.
        /// </summary>
        public static async Task<string> PostAsync(this WebClient webClient, string url, Dictionary<string, string> postData, Encoding responseEncoding)
        {
            if (responseEncoding == null)
                throw new ArgumentNullException("responseEncoding");

            if (postData == null)
                throw new ArgumentNullException("postData");

            if (url.IsEmpty())
                throw new ArgumentNullException("url");

            var responseBytes = await webClient.UploadValuesTaskAsync(url, postData.ToNameValueCollection());

            try
            {
                return responseEncoding.GetString(responseBytes);
            }
            catch (WebException ex)
            {
                throw new Exception(ex.GetResponseBody());
            }
        }

        public static int? GetResultsToFetch(this IEnumerable<QueryOption> options)
        {
            return options.OfType<ResultSetSizeQueryOption>().FirstOrDefault()?.Number;
        }

        /// <summary>
        /// Registers an external script url on this page.
        /// </summary>
        /// <param name="location">Specifies whether the script should be added to the page header. If False, it will be added to the </param>
        public static void RegisterScriptFile(this System.Web.UI.UserControl module, string scriptUrl, ScriptInsertLocation location = ScriptInsertLocation.FormBottom)
        {
            var mSharpPage = module.Page as MSharp.Framework.UI.Page;

            if (mSharpPage == null) throw new InvalidOperationException("The page of this module is not MSharp.Framework.UI.Page.");

            mSharpPage.RegisterScriptFile(scriptUrl, location);
        }

        /// <summary>
        /// It will restart the application.
        /// </summary>
        public static bool Restart(this System.Web.HttpApplication application)
        {
            // Method 1
            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); return true; }
            catch (ThreadAbortException) { return true; /* Good */  }
            catch {  /* We'll try another method */  }


            // Method 2 - needs full trust
            try { HttpRuntime.UnloadAppDomain(); return true; }
            catch (ThreadAbortException) { return true; /* Good */  }
            catch {  /* We'll try another method */  }


            // Method 3 - try web.config
            try { File.SetLastWriteTimeUtc(AppDomain.CurrentDomain.GetPath("web.config"), DateTime.UtcNow); return true; }
            catch (ThreadAbortException) { return true; /* Good */  }
            catch { return false; /*Still no hope, you have to do something else.*/            }
        }

        /// <summary>
        /// Returns a nullable value wrapper object if this value is the default for its type.
        /// </summary>
        public static T? NullIfDefault<T>(this T @value, T defaultValue = default(T)) where T : struct
        {
            if (value.Equals(defaultValue))
            {
                return null;
            }

            return @value;
        }
    }
}