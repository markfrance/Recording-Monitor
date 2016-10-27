namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Mail;
    using System.Web.Script.Serialization;

    partial class EmailService
    {
        /// <summary>
        /// Gets the Attachment objects to be attached to this email.
        /// </summary>
        public static IEnumerable<Attachment> GetAttachments(this IEmailQueueItem mail)
        {
            foreach (var attachmentInfo in mail.Attachments.OrEmpty().Split('|').Trim())
            {
                var item = ParseAttachment(attachmentInfo);
                if (item != null) yield return item;
            }
        }

        public static Attachment ParseAttachment(string attachmentInfo)
        {
            if (attachmentInfo.StartsWith("{"))
            {
                return GetAttachmentFromJSon(attachmentInfo);
            }
            else
            {
                if (attachmentInfo.StartsWith("\\\\") || Path.IsPathRooted(attachmentInfo) /*(attachment[1] == ':' && attachment[2] == '\\')*/)
                {
                    // absolute path:
                    return new Attachment(attachmentInfo);
                }
                else
                {
                    return new Attachment(AppDomain.CurrentDomain.GetPath(attachmentInfo));
                }
            }
        }

        static Attachment GetAttachmentFromJSon(string attachmentInfo)
        {
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(attachmentInfo);

            if (data == null) return null;

            var contents = data.GetOrDefault("Contents") as string;

            if (contents.HasValue())
            {
                if (data.GetOrDefault("IsLinkedResource").ToStringOrEmpty().TryParseAs<bool>() ?? false) return null; // No attachment needed?

                var stream = new MemoryStream(Convert.FromBase64String(contents));
                var name = data["Name"] as string;
                var contentId = data["ContentId"] as string;

                return new Attachment(stream, name) { ContentId = contentId };
            }

            var reference = data.GetOrDefault("PropertyReference") as string;
            if (reference.HasValue())
            {
                var document = Document.FromReference(reference);
                return new Attachment(new MemoryStream(document.FileData), document.FileName);
            }

            return null;
        }

        /// <summary>
        /// Gets the Linked Resource objects to be attached to this email.
        /// </summary>
        public static IEnumerable<LinkedResource> GetLinkedResources(this IEmailQueueItem mail)
        {
            if (mail.Attachments.HasValue())
            {
                foreach (var resource in mail.Attachments.Trim().Split('|').Where(x => x.StartsWith("{")))
                {
                    var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(resource);

                    if (data == null) continue;

                    var contents = data.GetOrDefault("Contents") as string;

                    if (contents.HasValue())
                    {
                        var isLinkedResource = data.GetOrDefault("IsLinkedResource").ToStringOrEmpty().TryParseAs<bool>() ?? false;

                        if (!isLinkedResource) continue;

                        var stream = new MemoryStream(Convert.FromBase64String(contents));
                        var name = data["Name"] as string;
                        var contentId = data["ContentId"] as string;

                        yield return new LinkedResource(stream) { ContentId = contentId };
                    }
                }
            }
        }
    }
}