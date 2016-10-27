namespace MSharp.Framework.Services
{


    public interface IPickyDocumentUrlContainer : IEntity
    {
        /// <summary>
        /// Gets the url of the specified document.
        /// </summary>
        string GetUrl(Document document);
    }
}