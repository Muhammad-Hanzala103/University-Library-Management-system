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
    public class TwilioSmsTransport : ISmsTransport
    {
        private readonly HttpClient _httpClient;
        private readonly KicsitLibraryDbContext _context;

        public TwilioSmsTransport(HttpClient httpClient, KicsitLibraryDbContext context)
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

            // Fetch Twilio options from SystemSettings
            var settings = await _context.SystemSettings
                .Where(s => s.Group == "Notifications" || s.Group == "System")
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            var accountSid = Get(settings, "SmsTwilioAccountSid");
            var authToken = Get(settings, "SmsTwilioAuthToken");
            
            // For WhatsApp, try WhatsApp specific from number first, then fall back to general Sms from number
            var fromNumberKey = isWhatsApp ? "WhatsAppTwilioFromNumber" : "SmsTwilioFromNumber";
            var fromNumber = Get(settings, fromNumberKey);
            if (string.IsNullOrWhiteSpace(fromNumber) && isWhatsApp)
            {
                fromNumber = Get(settings, "SmsTwilioFromNumber");
            }

            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(fromNumber))
            {
                return new SmsSendResult
                {
                    Succeeded = false,
                    FailureReason = "Twilio credentials or from number are missing in system settings."
                };
            }

            // Clean phone numbers (remove formatting if they don't have + prefix)
            var cleanTo = FormatPhoneNumber(toPhone);
            var cleanFrom = FormatPhoneNumber(fromNumber);

            if (isWhatsApp)
            {
                cleanTo = $"whatsapp:{cleanTo}";
                cleanFrom = $"whatsapp:{cleanFrom}";
            }

            try
            {
                var requestUrl = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                var authBytes = Encoding.UTF8.GetBytes($"{accountSid}:{authToken}");
                var authHeader = Convert.ToBase64String(authBytes);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                var parameters = new List<KeyValuePair<string, string>>
                {
                    new("To", cleanTo),
                    new("From", cleanFrom),
                    new("Body", message)
                };
                request.Content = new FormUrlEncodedContent(parameters);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var sid = doc.RootElement.GetProperty("sid").GetString();
                    return new SmsSendResult
                    {
                        Succeeded = true,
                        MessageSid = sid
                    };
                }
                else
                {
                    string failureReason = $"Twilio returned status {response.StatusCode}.";
                    try
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("message", out var msgProp))
                        {
                            failureReason = msgProp.GetString() ?? failureReason;
                        }
                    }
                    catch
                    {
                        // Ignore parsing failure
                    }

                    return new SmsSendResult
                    {
                        Succeeded = false,
                        FailureReason = failureReason
                    };
                }
            }
            catch (Exception ex)
            {
                return new SmsSendResult
                {
                    Succeeded = false,
                    FailureReason = $"HTTP request failed: {ex.Message}"
                };
            }
        }

        private static string FormatPhoneNumber(string phone)
        {
            var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (!cleaned.StartsWith("+") && cleaned.Length >= 10)
            {
                // Assume Pakistan (+92) if it starts with 03 or 3 and is 10/11 digits
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
