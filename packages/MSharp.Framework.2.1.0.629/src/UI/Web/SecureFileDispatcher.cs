namespace MSharp.Framework
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Web;
    using MSharp.Framework.Services;

    public class SecureFileDispatcher
    {
        string[] PathParts;

        Type Type;
        object Instance;
        string Property;
        HttpResponse Response;
        Document Document;
        IUser CurrentUser;

        /// <summary>
        /// Creates a new SecureFileDispatcher instance.
        /// </summary>
        public SecureFileDispatcher(string path, IUser currentUser)
        {
            CurrentUser = currentUser;

            Response = HttpContext.Current.Response;

            PathParts = path.Split('/');

            if (PathParts.Length < 2)
            {
                throw new Exception("Invalid path specified: '{0}'".FormatWith(path));
            }

            FindRequestedProperty();

            FindRequestedObject();
        }

        public void Dispatch()
        {
            DispatchFile(GetFile());
        }

        public FileInfo GetFile()
        {
            EnsureSecurity();

            var file = Document.LocalPath;

            // Fall-back logic
            if (!File.Exists(file))
                file = Document.FallbackPaths.FirstOrDefault(File.Exists);

            return file.AsFile();
        }

        void FindRequestedProperty()
        {
            var typeName = PathParts[0].Split('.')[0];

            Type = Database.GetRegisteredAssemblies().Select(a => a.GetExportedTypes().SingleOrDefault(t => t.Name == typeName)).ExceptNull().FirstOrDefault();
            if (Type == null) throw new Exception("Invalid type name specified: '{0}'".FormatWith(typeName));

            Property = PathParts[0].Split('.')[1];
        }

        void FindRequestedObject()
        {
            var idData = PathParts[1];

            foreach (var key in new[] { ".", "/" })
                if (idData.Contains(key)) idData = idData.Substring(0, idData.IndexOf(key));

            var id = idData.TryParseAs<Guid>();
            if (id == null) throw new Exception("Invalid object ID specified: '{0}'".FormatWith(idData));

            Instance = Database.Get(id.Value, Type);
            if (Instance == null) throw new Exception("Invalid {0} ID specified: '{1}'".FormatWith(Type.FullName, id));

            Document = EntityManager.ReadProperty(Instance, Property) as Document;
        }

        void EnsureSecurity()
        {
            try
            {
                var method = Type.GetMethod("Is" + Property + "VisibleTo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    throw new Exception(Type.FullName + ".Is" + Property + "VisibleTo() method is not defined.");
                }

                if (method.GetParameters().Count() != 1 || !method.GetParameters().Single().ParameterType.Implements<IUser>())
                    throw new Exception(Type.FullName + "." + method.Name + "() doesn't accept a single argument that implements IUser");

                if (!(bool)method.Invoke(Instance, new object[] { CurrentUser }))
                    throw new Exception("You are not authorised to view the requested file.");

            }
            catch (Exception ex)
            {
                Response.Clear();
                Response.Write("<html><body><h2>File access issue</h2></body></html>");
                Log.Error("Invalid secure file access: " + PathParts.ToString("/"), ex);

                Response.WriteLine("Invalid file request. Please contact your I.T. support.");
                Response.WriteLine(ex.Message);

                Response.End();
                return;
            }
        }

        void DispatchFile(FileInfo file)
        {
            if (!file.Exists())
            {
                Response.Clear();
                Response.Write("File does not exist: " + file);
                Response.Flush();
                Response.End();
                return;
            }

            var fileName = Document.FileName.Or(file.Name);
            var contentType = file.Extension.OrEmpty().TrimStart(".").ToLower().Or("Application/octet-stream");

            Response.Dispatch(file.ReadAllBytes(), fileName, contentType, endResponse: true);
        }

    }
}