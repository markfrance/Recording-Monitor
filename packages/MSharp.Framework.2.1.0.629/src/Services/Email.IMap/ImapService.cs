namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using AE.Net.Mail;

    public class ImapService
    {
        string Host;
        int Port;
        string Username;
        string Password;
        bool SecureConnection;
        bool SkipSslValidation;

        public ImapService(string host, int port, string username, string password, bool secureConnection = true, bool skipSslValidation = false)
        {
            this.Host = host;
            this.Port = port;
            this.Username = username;
            this.Password = password;
            this.SecureConnection = secureConnection;
            this.SkipSslValidation = skipSslValidation;
        }

        AE.Net.Mail.ImapClient CreateClient()
        {
            return new AE.Net.Mail.ImapClient(Host, Username, Password,
                AuthMethods.Login, Port, SecureConnection, SkipSslValidation);
        }

        IImapMessage Extract<TMessage>(AE.Net.Mail.MailMessage info, bool getRawHtml) where TMessage : IImapMessage, new()
        {
            var result = new TMessage
            {
                Date = info.Date,
                From = info.From?.Address,
                MessageId = info.MessageID,
                Bcc = info.Bcc?.Select(x => x.Address).ToString(", "),
                Cc = info.Cc?.Select(x => x.Address).ToString(", "),
                DateDownloaded = LocalTime.Now,
                Subject = info.Subject,
                Body = info.Body,
                To = info.To?.Select(x => x.Address).ToString(", ")
            };

            var attachmentList = info.AlternateViews.Cast<Attachment>();
            if (attachmentList.HasMany())
            {
                if (getRawHtml)
                {
                    result.Body = attachmentList.ElementAt(1).Body;
                }
                else
                {
                    result.Body = attachmentList.First().Body;
                }
            }

            result.Attachments = (info.Attachments ?? new List<Attachment>()).Select(attachment => new XElement("Attachment", new XAttribute("FileName", attachment.Filename), new XElement("Bytes", attachment.GetData().ToBase64String()))).ToLinesString();

            return result;
        }

        public void DownloadEmails<TMessage>(DateTime since)
            where TMessage : IImapMessage, IEntity, new()
        {
            using (var imap = CreateClient())
            {
                var messages = imap.SearchMessages(SearchCondition.SentSince(since).And(SearchCondition.Undraft()));

                foreach (var message in messages)
                {
                    var mail = message.Value;

                    if (Database.Any<TMessage>(x => x.MessageId == mail.MessageID)) continue;

                    var toSave = Extract<TMessage>(mail, getRawHtml: true);

                    Database.Save(toSave);
                }
            }
        }
    }
}
