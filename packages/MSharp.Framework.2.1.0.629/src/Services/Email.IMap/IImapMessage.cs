namespace MSharp.Framework.Services
{
    using System;

    public interface IImapMessage : IEntity
    {
        DateTime Date { get; set; }
        DateTime DateDownloaded { get; set; }
        string Subject { get; set; }
        string Body { get; set; }

        /// <summary>
        /// Each attachment will be XML in the form of:<para>&#160;</para> <para> &lt;Attachment FileName=&quot;myFile.pdf&quot;&gt;</para>
        /// <para>&#160;&#160;&#160;&#160;&lt;Bytes&gt;Base64 here&lt;/Bytes&gt;</para>
        /// <para>&lt;/Attachment&gt;</para>
        /// </summary>
        string Attachments { get; set; }
        string From { get; set; }
        string MessageId { get; set; }
        
        string Bcc { get; set; }
        string Cc { get; set; }
        string To { get; set; }
    }
}
