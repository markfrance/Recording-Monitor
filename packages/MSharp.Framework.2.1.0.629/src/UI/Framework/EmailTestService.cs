namespace MSharp.Framework.UI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using MSharp.Framework.Services;
    using System.IO;
    using System.Net.Mail;

    internal class EmailTestService
    {
        static readonly Regex LinkPattern = new Regex("(https?://[^ ]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        HttpRequest Request;
        HttpResponse Response;
        string To;
        string ReturnUrl;
        Attachment AttachmentFile;
        IEmailQueueItem Email;

        public EmailTestService(HttpRequest request, HttpResponse response)
        {
            this.Request = request;
            this.Response = response;

            To = Request["to"].ToStringOrEmpty().ToLower();
            ReturnUrl = Request.GetReturnUrl();
            if (Request.Has("attachmentInfo"))
                AttachmentFile = EmailService.ParseAttachment(Request["attachmentInfo"]);

            using (new SoftDeleteAttribute.Context(bypassSoftdelete: true))
            {
                Email = Request.GetOrDefault<IEmailQueueItem>("id");
            }
        }

        void Validate()
        {
            if (Request.Has("id") && Email == null) throw new Exception("Invalid Email id specified.");
        }

        internal void Process()
        {
            Validate();

            string response;
            if (AttachmentFile != null)
            {
                if (IsTextFile(AttachmentFile.Name))
                    response = "<a href='/?Web.Test.Command=testEmail&to=" + To + "'>&lt;&lt; Back to emails</a><pre>" + AttachmentFile.ContentStream.ReadAllText().HtmlEncode() + "</pre>";
                else
                {
                    Response.Dispatch(AttachmentFile.ContentStream.ReadAllBytes(), AttachmentFile.Name);
                    return;
                }
            }
            else if (Email == null)
            {
                response = GenerateInbox();
            }
            else
            {
                response = GenerateEmailView();
            }

            Dispatch(response);
        }

        void Dispatch(string response)
        {
            Response.Clear();
            Response.ContentType = "text/html";
            Response.Write("<html>");

            Response.Write("<head>");
            Response.Write("<style>");
            Response.Write("body {font-family:Arial; background:#fff; }");
            Response.Write("td, th {border:1px solid #999; padding:5px; font-size:9pt;}");
            Response.Write("th {background:#eee;}");
            Response.Write("a { color: blue; }");
            Response.Write(".exit { background: #444; color:#fff; padding:4px 10px; display:inline-block; float:right; margin-top:10px; border-radius: 10px; text-decoration:none;}");
            Response.Write(".body { background: #f0f0f0; }");
            Response.Write(".label { color: #888; width:100px; }");
            Response.Write("</style>");
            Response.Write("</head>");
            Response.Write("<body>");

            Response.Write(response);

            Response.Write("<a class='exit' href='{0}'>Exit Mailbox</a>".FormatWith(ReturnUrl));

            // TDD hack:
            Response.Write("<a style='display:none;' href='/?Web.Test.Command=restart'>Restart Temp Database</a>");

            Response.Write("</body></html>");
            Response.End();
        }

        List<IEmailQueueItem> GetEmails()
        {
            using (new SoftDeleteAttribute.Context(bypassSoftdelete: true))
            {
                var items = Database.GetList<IEmailQueueItem>().Where(x => To.IsEmpty() || (x.To + "," + x.Cc + ", " + x.Bcc).ToLower().Contains(To));

                return items.OrderByDescending(x => x.Date).Take(15).ToList();
            }
        }
        
        static string GetBodyHtml(string body, bool wasHtml)
        {
            if (wasHtml) return body;

            body = body.HtmlEncode().Replace("\n", "<br/>").Replace("\r", "");
            body = LinkPattern.Replace(body, "<a href=\"$1\" target=\"_parent\">$1</a>");
            return body;
        }

        string GenerateInbox()
        {
            var r = new StringBuilder();

            var emails = GetEmails();

            r.AppendLine("<h2>Emails sent to <u>" + To.Or("ALL") + "</u></h2>");
            r.AppendLine("<table cellspacing='0'>");
            r.AppendLine("<tr>");
            r.AppendLine("<th>Date</th>");
            r.AppendLine("<th>Time</th>");
            r.AppendLine("<th>From</th>");
            r.AppendLine("<th>To</th>");
            r.AppendLine("<th>Cc</th>");
            r.AppendLine("<th>Bcc</th>");
            r.AppendLine("<th>Subject</th>");
            r.AppendLine("<th>Attachments</th>");
            r.AppendLine("</tr>");

            if (emails.None())
            {
                r.AppendLine("<tr>");
                r.AppendLine("<td colspan='8'>No emails in the system</td>");
                r.AppendLine("</tr>");
            }
            else
            {
                foreach (var item in emails)
                {
                    r.AppendLine("<tr>");
                    r.AddFormattedLine("<td>{0}</td>", item.Date.ToString("yyyy-MM-dd"));
                    r.AddFormattedLine("<td>{0}</td>", item.Date.ToSmallTime());
                    r.AddFormattedLine("<td>{0}</td>", GetFrom(item));
                    r.AddFormattedLine("<td>{0}</td>", item.To);
                    r.AddFormattedLine("<td>{0}</td>", item.Cc);
                    r.AddFormattedLine("<td>{0}</td>", item.Bcc);

                    r.AddFormattedLine("<td><a href='/?Web.Test.Command=testEmail&id={0}&to={1}&ReturnUrl={2}'>{3}</a></td>",
                        item.GetId(), To, ReturnUrl.UrlEncode(), item.Subject.Or("[NO SUBJECT]").HtmlEncode());

                    r.AddFormattedLine("<td>{0}</td>", GetAttachmentLinks(item));

                    r.AppendLine("</tr>");
                }
            }

            r.AppendLine("</table>");

            return r.ToString();
        }

        string GetFrom(IEmailQueueItem email) => email.GetSender().Get(s => s.DisplayName.Or("").HtmlEncode() + s.Address.WithWrappers(" &lt;", "&gt;"));

        string GetAttachmentLinks(IEmailQueueItem email)
        {
            return email.Attachments.OrEmpty().Split('|').Trim()
                .Select(f => $"<form action='/?Web.Test.Command=testEmail&To={To}&ReturnUrl={ReturnUrl.UrlEncode()}' method='post'><input type=hidden name='attachmentInfo' value='{f.HtmlEncode()}'/><a href='#' onclick='this.parentElement.submit()'>{EmailService.ParseAttachment(f)?.Name.HtmlEncode()}</a></form>")
                .ToString("");
        }

        bool IsTextFile(string fileName) => Path.GetExtension(fileName).ToLower().IsAnyOf(".txt", ".csv", ".xml");

        string GenerateEmailView()
        {
            var r = new StringBuilder();

            r.AppendLine("<a href='/?Web.Test.Command=testEmail&to=" + To + "'>&lt;&lt; Back</a>");
            r.AppendLine("<h2>Subject: <u>" + Email.Subject.Or("[NO SUBJECT]") + "</u></h2>");
            r.AppendLine("<table cellspacing='0'>");

            var body = GetBodyHtml(Email.Body.Or("[EMPTY BODY]"), Email.Html);

            var toShow = new Dictionary<string, object> {
                { "Date", Email.Date.ToString("yyyy-MM-dd") +" at " + Email.Date.ToString("HH:mm") },
                {"From", GetFrom(Email)},
                { "To", Email.To},
                {"Bcc", Email.Bcc},
                {"Cc", Email.Cc},
                {"Subject", Email.Subject.Or("[NO SUBJECT]").HtmlEncode().WithWrappers("<b>", "</b>")},
                {"Body", body.WithWrappers("<div class='body'>" ,"</div>") },
                {"Attachments", GetAttachmentLinks(Email) }
            };

            foreach (var item in toShow.Where(x => x.Value.ToStringOrEmpty().HasValue()))
            {
                r.AppendLine("<tr>");
                r.AddFormattedLine("<td class='label'>{0}:</td>", item.Key.HtmlEncode());
                r.AddFormattedLine("<td>{0}</td>", item.Value);

                r.AppendLine("</tr>");
            }

            r.AppendLine("</table>");

            return r.ToString();
        }
    }
}
