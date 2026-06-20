using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace KicsitLibrary.Services.Notifications
{
    public class InfobipSmsTransport : ISmsTransport
    {
        private readonly HttpClient _httpClient;
        private readonly KicsitLibraryDbContext _context;

        public InfobipSmsTransport(HttpClient httpClient, KicsitLibraryDbContext context)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SmsSendResult> SendSmsAsync(string toPhone, string message, bool isWhatsApp = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toPhone))
            {
                return new SmsSendResult { Succeeded = false, FailureReason = "To phone number is missing." };
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return new SmsSendResult { Succeeded = false, FailureReason = "Message body is empty." };
            }

            // Fetch Infobip options from SystemSettings
            var settings = await _context.SystemSettings
                .Where(s => s.Group == "Notifications" || s.Group == "System")
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            var apiKey = Get(settings, "SmsInfobipApiKey");
            var baseUrl = Get(settings, "SmsInfobipBaseUrl");
            var sender = Get(settings, "SmsInfobipSender", "Ilm-o-Kutub");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return new SmsSendResult
                {
                    Succeeded = false,
                    FailureReason = "Infobip credentials or BaseUrl are missing in system settings."
                };
            }

            // Standardize URL
            baseUrl = baseUrl.Trim().TrimEnd('/');
            if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }

            var cleanTo = FormatPhoneNumber(toPhone);

            try
            {
                var requestUrl = $"{baseUrl}/sms/2/text/advanced";
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("App", apiKey);

                var payload = new
                {
                    messages = new[]
                    {
                        new
                        {
                            from = sender,
                            destinations = new[]
                            {
                                new { to = cleanTo }
                            },
                            text = message
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var messages = doc.RootElement.GetProperty("messages");
                    if (messages.GetArrayLength() > 0)
                    {
                        var status = messages[0].GetProperty("status");
                        var groupName = status.GetProperty("groupName").GetString();
                        if (groupName != null && (groupName.Equals("PENDING", StringComparison.OrdinalIgnoreCase) || groupName.Equals("ACCEPTED", StringComparison.OrdinalIgnoreCase) || groupName.Equals("DELIVERED", StringComparison.OrdinalIgnoreCase)))
                        {
                            var messageId = messages[0].GetProperty("messageId").GetString();
                            return new SmsSendResult
                            {
                                Succeeded = true,
                                MessageSid = messageId
                            };
                        }
                        else
                        {
                            var desc = status.GetProperty("description").GetString();
                            return new SmsSendResult
                            {
                                Succeeded = false,
                                FailureReason = desc ?? "Failed in Infobip status resolution."
                            };
                        }
                    }
                    return new SmsSendResult
                    {
                        Succeeded = false,
                        FailureReason = "Empty response messages list from Infobip."
                    };
                }
                else
                {
                    return new SmsSendResult
                    {
                        Succeeded = false,
                        FailureReason = $"Infobip API returned error: {response.StatusCode} - {responseContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new SmsSendResult
                {
                    Succeeded = false,
                    FailureReason = $"Infobip HTTP request failed: {ex.Message}"
                };
            }
        }

        private static string FormatPhoneNumber(string phone)
        {
            var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (!cleaned.StartsWith("+") && cleaned.Length >= 10)
            {
                if (cleaned.StartsWith("03"))
                {
                    cleaned = "+92" + cleaned.Substring(1);
                }
                else if (cleaned.StartsWith("3"))
                {
                    cleaned = "+92" + cleaned;
                }
            }
            return cleaned;
        }

        private static string Get(Dictionary<string, string> values, string key, string defaultValue = "")
        {
            return values.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
