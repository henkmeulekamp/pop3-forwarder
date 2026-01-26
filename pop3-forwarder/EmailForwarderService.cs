using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

public class EmailForwarderService : BackgroundService
{
    private readonly ILogger<EmailForwarderService> _logger;
    private readonly IConfiguration _configuration;

    public EmailForwarderService(
        ILogger<EmailForwarderService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Forwarder Service starting...");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Checking for new emails...");
                await ForwardEmailsAsync();
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing emails");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("Email Forwarder Service stopping...");
    }

    private async Task ForwardEmailsAsync()
    {
        // Read POP3 settings from configuration
        string host = _configuration["Pop3Settings:Host"]!;
        int port = int.Parse(_configuration["Pop3Settings:Port"]!);
        bool useSsl = bool.Parse(_configuration["Pop3Settings:UseSsl"]!);
        bool checkCertificateRevocation = bool.Parse(_configuration["Pop3Settings:CheckCertificateRevocation"]!);
        string username = _configuration["Pop3Settings:Username"]!;
        string password = _configuration["Pop3Settings:Password"]!;

        using var client = new Pop3Client();
        
            client.CheckCertificateRevocation = checkCertificateRevocation;
            // Connect to the POP3 server
            await client.ConnectAsync(host, port, useSsl);
            _logger.LogDebug($"POP3 Connected to {host}");

            // Authenticate
            await client.AuthenticateAsync(username, password);
            _logger.LogDebug("POP3 Authenticated successfully");

            // Get the number of messages
            int messageCount = client.Count;
            _logger.LogInformation($"POP3 Total messages: {messageCount}");
            try
            {
                // Loop through all messages
                for (int i = 0; i < messageCount; i++)
                {
                    var message = await client.GetMessageAsync(i);
                    
                    _logger.LogInformation($"- Message {i + 1}: From: {message.From} Subject: {message.Subject}");

                    // Forward the email via SMTP
                    await SendEmailViaSmtpAsync(message, _configuration);

                    await client.DeleteMessageAsync(i);
                }

                // Disconnect
                await client.DisconnectAsync(true);
                _logger.LogDebug("POP3 Disconnected from server");
            }
            catch (Exception ex) {
                _logger.LogError($"POP 3 - Error: {ex.Message}", ex);
            }
    }
 
    async Task SendEmailViaSmtpAsync(MimeMessage message, IConfiguration configuration)
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
            _logger.LogDebug($"-- Connected to SMTP server {smtpHost}");

            // Authenticate
            await smtpClient.AuthenticateAsync(smtpUsername, smtpPassword);
            _logger.LogDebug("-- Authenticated with SMTP server");

            // Create a new message or forward the existing one
            // Option 1: Forward as-is (preserving original From)
            // await smtpClient.SendAsync(message);
             _logger.LogInformation($"-- Email came for {message.To.ToString()}");
            // Option 2: Create a new forwarded message
            var forwardedMessage = new MimeMessage();
            forwardedMessage.From.Add(new MailboxAddress(MailboxAddress.Parse(message.To.ToString()).Address, smtpUsername));
            forwardedMessage.To.Add(MailboxAddress.Parse(forwardTo));
            forwardedMessage.Subject = $"Fwd: {message.Subject}";
            forwardedMessage.Body = message.Body;

            // Send the message
            await smtpClient.SendAsync(forwardedMessage);
            _logger.LogInformation($"-- Email forwarded to {forwardTo}");

            // Disconnect
            await smtpClient.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"-- SMTP Error: {ex.Message}", ex);
            // bubble up the error to avoid deleting the email from POP3
            throw; 
        }
    }
}