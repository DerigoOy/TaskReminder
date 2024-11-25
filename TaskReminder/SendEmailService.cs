using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace TaskReminder
{
    public class SendEmailService
    {
        private static SMTPConfig _settings;

        public SendEmailService(SMTPConfig settings)
        {
            _settings = settings;
        }

        public SmtpStatusCode Send(MailItem mail)
        {
            SmtpStatusCode status = new SmtpStatusCode();          

            if (!String.IsNullOrEmpty(_settings.UserName) && !String.IsNullOrEmpty(_settings.Password))
            {
                _settings.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            }

            if (String.IsNullOrEmpty(mail.From))
            {
                mail.From = "noreply@pro3.fi";
            }

            MailMessage message = new MailMessage();
            message.From = new MailAddress(mail.From);

            if (!String.IsNullOrEmpty(_settings.AllEmailsTo))
            {
                message.To.Add(new MailAddress(_settings.AllEmailsTo));
            }
            else if (!String.IsNullOrEmpty(_settings.OnlySendTo)) {
                if (mail.To == _settings.OnlySendTo)
                {
                    message.To.Add(new MailAddress(_settings.OnlySendTo));
                }
                else
                {
                    Helper.Log(_settings.OnlySendTo + " <> " + mail.To);
                    return status;
                }
            }
            else
            {
                message.To.Add(new MailAddress(mail.To));
            }

            message.Subject = mail.Subject;
            message.Body = mail.Body;
            message.IsBodyHtml = true;
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.SubjectEncoding = System.Text.Encoding.UTF8;

            Helper.Log("Sending to: " + message.To);

            using (SmtpClient client = new SmtpClient(_settings.Host))
            {
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.EnableSsl = _settings.SSL;
                client.Port = _settings.Port;
                client.Credentials = _settings.Credentials;
                client.Timeout = _settings.Timeout;

                try
                {
                    client.Send(message);
                    status = SmtpStatusCode.Ok;
                }
                catch (SmtpFailedRecipientException ex)
                {
                    status = ex.StatusCode;
                    Helper.Log("Send Failed: " + ex.ToString());
                }

                client.Dispose();
            }

            message.Dispose();

            return status;
        }
    }
}
