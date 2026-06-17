using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Catalog;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KicsitLibrary.Tests
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFunc;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
        {
            _responseFunc = responseFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFunc(request));
        }
    }

    public class FakeSmsTransport : ISmsTransport
    {
        public List<(string Phone, string Message, bool IsWhatsApp)> SentMessages { get; } = new();
        public SmsSendResult NextResult { get; set; } = new() { Succeeded = true, MessageSid = "SM123" };

        public Task<SmsSendResult> SendSmsAsync(string toPhone, string message, bool isWhatsApp = false, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((toPhone, message, isWhatsApp));
            return Task.FromResult(NextResult);
        }
    }

    public class ThirdPartyApiTests
    {
        [Fact]
        public async Task BookMetadataService_FetchesFromGoogleBooksSuccessfully()
        {
            var googleBooksJson = @"{
                ""items"": [
                    {
                        ""volumeInfo"": {
                            ""title"": ""Test Title"",
                            ""subtitle"": ""Test Subtitle"",
                            ""authors"": [""Author One"", ""Author Two""],
                            ""publisher"": ""Publisher Co"",
                            ""publishedDate"": ""2026-05"",
                            ""description"": ""Test Description"",
                            ""imageLinks"": {
                                ""thumbnail"": ""http://books.google.com/test.jpg""
                            },
                            ""language"": ""en""
                        }
                    }
                ]
            }";

            var handler = new FakeHttpMessageHandler(req =>
            {
                Assert.Contains("googleapis.com", req.RequestUri!.Host);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(googleBooksJson, Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler);
            var service = new BookMetadataService(client);

            var result = await service.FetchByIsbnAsync("1234567890");

            Assert.NotNull(result);
            Assert.Equal("Test Title", result!.Title);
            Assert.Equal("Test Subtitle", result.SubTitle);
            Assert.Equal(2, result.Authors.Count);
            Assert.Contains("Author One", result.Authors);
            Assert.Equal("Publisher Co", result.Publisher);
            Assert.Equal(2026, result.PublicationYear);
            Assert.Equal("Test Description", result.Description);
            Assert.Equal("https://books.google.com/test.jpg", result.CoverImageUrl);
        }

        [Fact]
        public async Task BookMetadataService_FallsBackToOpenLibrary()
        {
            var openLibraryJson = @"{
                ""ISBN:9780131103627"": {
                    ""title"": ""OL Title"",
                    ""subtitle"": ""OL Subtitle"",
                    ""notes"": ""OL Description"",
                    ""publishers"": [{ ""name"": ""OL Publisher"" }],
                    ""publish_date"": ""June 2005"",
                    ""authors"": [{ ""name"": ""OL Author"" }],
                    ""cover"": { ""large"": ""https://covers.openlibrary.org/ol.jpg"" }
                }
            }";

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host.Contains("googleapis.com"))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                
                Assert.Contains("openlibrary.org", req.RequestUri!.Host);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(openLibraryJson, Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler);
            var service = new BookMetadataService(client);

            var result = await service.FetchByIsbnAsync("9780131103627");

            Assert.NotNull(result);
            Assert.Equal("OL Title", result!.Title);
            Assert.Equal("OL Subtitle", result.SubTitle);
            Assert.Single(result.Authors);
            Assert.Equal("OL Author", result.Authors[0]);
            Assert.Equal("OL Publisher", result.Publisher);
            Assert.Equal(2005, result.PublicationYear);
            Assert.Equal("OL Description", result.Description);
            Assert.Equal("https://covers.openlibrary.org/ol.jpg", result.CoverImageUrl);
        }

        [Fact]
        public async Task SendNotificationAsync_SendsSmsWhenSmsSettingsAreValid()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            
            await database.SetSystemSettingAsync("SmsNotificationEnabled", "True", "Notifications");
            await database.SetSystemSettingAsync("SmsTwilioAccountSid", "AC123", "Notifications");
            await database.SetSystemSettingAsync("SmsTwilioAuthToken", "token123", "Notifications");
            await database.SetSystemSettingAsync("SmsTwilioFromNumber", "+15005550006", "Notifications");
            
            data.Student.Phone = "+923001234567";
            await database.Context.SaveChangesAsync();

            var notification = await database.AddNotificationAsync(data, channel: "SMS");
            
            var fakeSmsTransport = new FakeSmsTransport();
            var notificationService = new NotificationService(
                database.Context,
                new ActivityLogService(new Repository<ActivityLog>(database.Context)),
                new FakeEmailTransport(),
                new EmailSettingsService(database.Context),
                fakeSmsTransport
            );

            var result = await notificationService.SendNotificationAsync(notification.Id, data.User.Id);

            Assert.True(result.Succeeded);
            Assert.True(result.Attempted);
            Assert.Single(fakeSmsTransport.SentMessages);
            Assert.Equal("+923001234567", fakeSmsTransport.SentMessages[0].Phone);
            Assert.False(fakeSmsTransport.SentMessages[0].IsWhatsApp);

            await database.Context.Entry(notification).ReloadAsync();
            Assert.Equal(NotificationStatus.Sent, notification.Status);
        }

        [Fact]
        public async Task SendNotificationAsync_SendsWhatsAppWhenWhatsAppSettingsAreValid()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            
            await database.SetSystemSettingAsync("WhatsAppNotificationEnabled", "True", "Notifications");
            await database.SetSystemSettingAsync("SmsTwilioAccountSid", "AC123", "Notifications");
            await database.SetSystemSettingAsync("SmsTwilioAuthToken", "token123", "Notifications");
            await database.SetSystemSettingAsync("WhatsAppTwilioFromNumber", "+15005550006", "Notifications");
            
            data.Student.Phone = "+923001234567";
            await database.Context.SaveChangesAsync();

            var notification = await database.AddNotificationAsync(data, channel: "WhatsApp");
            
            var fakeSmsTransport = new FakeSmsTransport();
            var notificationService = new NotificationService(
                database.Context,
                new ActivityLogService(new Repository<ActivityLog>(database.Context)),
                new FakeEmailTransport(),
                new EmailSettingsService(database.Context),
                fakeSmsTransport
            );

            var result = await notificationService.SendNotificationAsync(notification.Id, data.User.Id);

            Assert.True(result.Succeeded);
            Assert.True(result.Attempted);
            Assert.Single(fakeSmsTransport.SentMessages);
            Assert.Equal("+923001234567", fakeSmsTransport.SentMessages[0].Phone);
            Assert.True(fakeSmsTransport.SentMessages[0].IsWhatsApp);

            await database.Context.Entry(notification).ReloadAsync();
            Assert.Equal(NotificationStatus.Sent, notification.Status);
        }
    }
}
