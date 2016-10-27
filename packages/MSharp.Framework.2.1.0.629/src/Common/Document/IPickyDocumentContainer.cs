using System.Collections.Generic;
namespace MSharp.Framework.Services
{
    /// <summary>
    /// This interface can be implemented on any entity which has a property of type Document.
    /// </summary>
    public interface IPickyDocumentContainer : IEntity
    {
        /// <summary>
        /// Gets the path to the physical folder containing files for the specified document property.
        /// If you don't need to implement this specific method, simply return NULL.
        /// </summary>
        string GetPhysicalFolderPath(Document document);

        /// <summary>
        /// Gets the URL to the virtual folder containing files for the specified document property.
        /// If you don't need to implement this specific method, simply return NULL.
        /// </summary>
        string GetVirtualFolderPath(Document document);

        /// <summary>
        /// Gets the name of the file used for the specified document property, without extension.
        /// If you don't need to implement this specific method, simply return NULL.
        /// </summary>
        string GetFileNameWithoutExtension(Document document);

        /// <summary>
        /// Gets the fallback paths for the specified document.
        /// </summary>
        IEnumerable<string> GetFallbackPaths(Document document);
    }
}