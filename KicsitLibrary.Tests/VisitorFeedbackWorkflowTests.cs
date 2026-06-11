using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Services.Consumer;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests
{
    public class VisitorFeedbackWorkflowTests
    {
        [Fact]
        public async Task VisitorFeedback_CanBeSavedAndStatusUpdated()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new ConsumerService(database.Context);

            // 1. Save new feedback
            var feedback = new VisitorFeedback
            {
                VisitorName = "John Doe",
                CNIC = "12345-1234567-1",
                Phone = "0300-1234567",
                Email = "john@example.com",
                VisitPurpose = "Audit",
                FeedbackType = "Suggestion",
                FeedbackText = "Great library!",
                Status = "New"
            };

            await service.AddVisitorFeedbackAsync(feedback);

            var savedFeedbacks = (await service.GetAllVisitorFeedbacksAsync()).ToList();
            Assert.Single(savedFeedbacks);
            var saved = savedFeedbacks.First();
            Assert.Equal("John Doe", saved.VisitorName);
            Assert.Equal("New", saved.Status);
            Assert.Equal("Great library!", saved.FeedbackText);

            // 2. Update status and remarks
            saved.Status = "Reviewed";
            saved.ReviewedRemarks = "Approved by head librarian";
            
            await service.UpdateVisitorFeedbackAsync(saved);

            var updatedFeedbacks = (await service.GetAllVisitorFeedbacksAsync()).ToList();
            Assert.Single(updatedFeedbacks);
            var updated = updatedFeedbacks.First();
            Assert.Equal("Reviewed", updated.Status);
            Assert.Equal("Approved by head librarian", updated.ReviewedRemarks);
        }
    }
}
