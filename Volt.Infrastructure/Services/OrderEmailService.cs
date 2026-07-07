using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volt.Application.Dtos.Order;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services
{
    public sealed class OrderEmailService : IOrderEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderEmailService> _logger;

        public OrderEmailService(IConfiguration configuration, ILogger<OrderEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendOrderConfirmationAsync(OrderDto order, CancellationToken ct = default)
        {
            var host = _configuration["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation("SMTP is not configured. Skipping confirmation email for order {OrderNumber}.", order.OrderNumber);
                return;
            }

            try
            {
                var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
                var from = _configuration["Smtp:From"] ?? _configuration["Smtp:Username"];
                if (string.IsNullOrWhiteSpace(from))
                {
                    _logger.LogWarning("SMTP From/Username is not configured. Skipping confirmation email for order {OrderNumber}.", order.OrderNumber);
                    return;
                }

                using var message = new MailMessage(from, order.Email)
                {
                    Subject = $"VOLT.AZ order confirmation {order.OrderNumber}",
                    Body = BuildBody(order),
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var enableSsl) ? enableSsl : true
                };

                var username = _configuration["Smtp:Username"];
                var password = _configuration["Smtp:Password"];
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }

                await client.SendMailAsync(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email for order {OrderNumber}.", order.OrderNumber);
            }
        }

        private static string BuildBody(OrderDto order)
        {
            var lines = order.Items.Select(x => $"- {x.ProductName} ({x.SelectedPower}) x{x.Quantity}: {x.LineTotal:0.##} AZN");
            var deliveryNote = order.DeliveryMethod switch
            {
                1 => "Unvana chatdirilma qiymeti sifarisden sonra hesablanib size bildirilir.",
                2 => "Tehvil menteqesi melumati tehlukesizlik meqsedi ile email vasitesi ile paylasilir.",
                _ => "Chatdirilma ve tehvil detallari telefon ve ya email ile tesdiqlenir."
            };

            return $"""
            Salam {order.FullName},

            Sifarisiniz qeyde alindi.

            Sifaris nomresi: {order.OrderNumber}
            Status: {order.Status}
            Cem: {order.FinalTotal:0.##} AZN

            Mehsullar:
            {string.Join(Environment.NewLine, lines)}

            Chatdirilma:
            {deliveryNote}

            VOLT.AZ komandasi sifarisinizi yoxlayacaq ve novbeti addim ucun telefon nomreniz ve ya email unvaniniz uzerinden sizinle elaqe saxlayacaq.
            """;
        }
    }
}
