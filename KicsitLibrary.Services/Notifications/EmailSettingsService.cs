using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Notifications
{
    public class EmailSettingsService : IEmailSettingsService
    {
        private readonly KicsitLibraryDbContext _context;

        public EmailSettingsService(KicsitLibraryDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<EmailTransportOptions> GetOptionsAsync()
        {
            var values = await _context.SystemSettings
                .Where(setting => setting.Group == "Notifications")
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value);

            return new EmailTransportOptions
            {
                Host = Get(values, "SmtpHost"),
                Port = ParsePositiveInteger(Get(values, "SmtpPort"), 587),
                UseSsl = ParseBoolean(Get(values, "SmtpUseSsl"), true),
                User = Get(values, "SmtpUser"),
                Password = Get(values, "SmtpPassword"),
                FromEmail = Get(values, "SmtpFromEmail"),
                FromName = Get(values, "SmtpFromName", ProductBrand.Name),
                EmailNotificationEnabled = ParseBoolean(
                    Get(values, "EmailNotificationEnabled"),
                    false),
                MaxNotificationRetryCount = ParsePositiveInteger(
                    Get(values, "MaxNotificationRetryCount"),
                    3)
            };
        }

        public async Task<EmailSettingsValidationResult> ValidateAsync()
        {
            var options = await GetOptionsAsync();
            if (!options.EmailNotificationEnabled)
            {
                return Invalid(options, "Email notifications are disabled.");
            }

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(options.Host))
            {
                missing.Add("SmtpHost");
            }
            if (string.IsNullOrWhiteSpace(options.User))
            {
                missing.Add("SmtpUser");
            }
            if (string.IsNullOrWhiteSpace(options.Password))
            {
                missing.Add("SmtpPassword");
            }
            if (string.IsNullOrWhiteSpace(options.FromEmail))
            {
                missing.Add("SmtpFromEmail");
            }

            if (missing.Count > 0)
            {
                return Invalid(options, $"Missing email setting(s): {string.Join(", ", missing)}.");
            }

            return new EmailSettingsValidationResult
            {
                IsValid = true,
                IsEnabled = true,
                Message = "Email settings are configured.",
                Options = options
            };
        }

        private static EmailSettingsValidationResult Invalid(
            EmailTransportOptions options,
            string message)
        {
            return new EmailSettingsValidationResult
            {
                IsValid = false,
                IsEnabled = options.EmailNotificationEnabled,
                Message = message,
                Options = options
            };
        }

        private static string Get(
            IReadOnlyDictionary<string, string> values,
            string key,
            string defaultValue = "")
        {
            return values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private static int ParsePositiveInteger(string value, int defaultValue)
        {
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
        }

        private static bool ParseBoolean(string value, bool defaultValue)
        {
            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }
    }
}
