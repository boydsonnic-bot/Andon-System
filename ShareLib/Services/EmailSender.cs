using System.Net;
using System.Net.Mail;

namespace SharedLib.Services
{
    public class EmailSender
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _sender;

        public EmailSender(string host, int port, string user, string pass, string sender)
        {
            _host = host; _port = port; _user = user; _pass = pass; _sender = sender;
        }

        public void Send(string[] to, string subject, string bodyHtml)
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(_sender),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            foreach (var t in to)
                if (!string.IsNullOrWhiteSpace(t))
                    msg.To.Add(t.Trim());

            using var client = new SmtpClient(_host, _port)
            {
                Credentials = new NetworkCredential(_user, _pass),
                EnableSsl = true
            };
            client.Send(msg);
        }
    }
}