using Microsoft.Extensions.Configuration;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("POP3 Email Forwarder");
Console.WriteLine("Press any ctrl-c to stop...\n");

while (true)
{
    await MailWorker.ReadPop3EmailsAsync(configuration);
    
    Console.WriteLine("\nWaiting 60 seconds before next check...");
    
    await Task.Delay(60000);
    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] Starting next email check...");
}



