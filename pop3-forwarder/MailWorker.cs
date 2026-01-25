using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

internal class MailWorker
{
        
    public static async Task ReadPop3EmailsAsync(IConfiguration configuration)
    {
        // Read POP3 settings from configuration
        string host = configuration["Pop3Settings:Host"]!;
        int port = int.Parse(configuration["Pop3Settings:Port"]!);
        bool useSsl = bool.Parse(configuration["Pop3Settings:UseSsl"]!);
        bool checkCertificateRevocation = bool.Parse(configuration["Pop3Settings:CheckCertificateRevocation"]!);
        string username = configuration["Pop3Settings:Username"]!;
        string password = configuration["Pop3Settings:Password"]!;

        using var client = new Pop3Client();
        
            client.CheckCertificateRevocation = checkCertificateRevocation;
            // Connect to the POP3 server
            await client.ConnectAsync(host, port, useSsl);
            Console.WriteLine($"POP3 Connected to {host}");

            // Authenticate
            await client.AuthenticateAsync(username, password);
            Console.WriteLine("POP3 Authenticated successfully");

            // Get the number of messages
            int messageCount = client.Count;
            Console.WriteLine($"POP3 Total messages: {messageCount}");
            try
            {
                // Loop through all messages
                for (int i = 0; i < messageCount; i++)
                {
                    var message = await client.GetMessageAsync(i);
                    
                    Console.WriteLine($"- Message {i + 1}:");
                    Console.WriteLine($"  From: {message.From}");
                    Console.WriteLine($"  Subject: {message.Subject}");
                    Console.WriteLine();

                    // Forward the email via SMTP
                    await SendEmailViaSmtpAsync(message, configuration);

                    await client.DeleteMessageAsync(i);
                }

                // Disconnect
                await client.DisconnectAsync(true);
                Console.WriteLine("POP3 Disconnected from server");
            }
            catch (Exception ex) {
                Console.WriteLine($"POP 3 - Error: {ex.Message}");
            }
    }
 
    static async Task SendEmailViaSmtpAsync(MimeMessage message, IConfiguration configuration)
    {
        // Read SMTP settings from configuration
        string smtpHost = configuration["SmtpSettings:Host"]!;
        int smtpPort = int.Parse(configuration["SmtpSettings:Port"]!);
        bool useSsl = bool.Parse(configuration["SmtpSettings:UseSsl"]!);
        bool checkCertificateRevocation = bool.Parse(configuration["SmtpSettings:CheckCertificateRevocation"]!);
        string smtpUsername = configuration["SmtpSettings:Username"]!;
        string smtpPassword = configuration["SmtpSettings:Password"]!;
        string forwardTo = configuration["SmtpSettings:ForwardTo"]!;

        using var smtpClient = new SmtpClient();
        
        try
        {
            smtpClient.CheckCertificateRevocation = checkCertificateRevocation;
            
            // Connect to the SMTP server
            await smtpClient.ConnectAsync(smtpHost, smtpPort, useSsl);
            Console.WriteLine($"  Connected to SMTP server {smtpHost}");

            // Authenticate
            await smtpClient.AuthenticateAsync(smtpUsername, smtpPassword);
            Console.WriteLine("  Authenticated with SMTP server");

            // Create a new message or forward the existing one
            // Option 1: Forward as-is (preserving original From)
            // await smtpClient.SendAsync(message);
             Console.WriteLine($"  Email came for {message.To.ToString()}");
            // Option 2: Create a new forwarded message
            var forwardedMessage = new MimeMessage();
            forwardedMessage.From.Add(new MailboxAddress(MailboxAddress.Parse(message.To.ToString()).Address, smtpUsername));
            forwardedMessage.To.Add(MailboxAddress.Parse(forwardTo));
            forwardedMessage.Subject = $"{message.Subject}";
            forwardedMessage.Body = message.Body;

            // Send the message
            await smtpClient.SendAsync(forwardedMessage);
            Console.WriteLine($"  Email forwarded to {forwardTo}");

            // Disconnect
            await smtpClient.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  SMTP Error: {ex.Message}");
            // bubble up the error to avoid deleting the email from POP3
            throw; 
        }
    }
}