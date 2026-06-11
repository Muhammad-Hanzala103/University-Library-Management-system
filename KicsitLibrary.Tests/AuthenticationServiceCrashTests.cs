using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Authentication;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests
{
    public class AuthenticationServiceCrashTests : IAsyncLifetime
    {
        private SqliteTestDatabase _testDb = null!;
        private Mock<IActivityLogService> _mockLogService = null!;

        public async Task InitializeAsync()
        {
            _testDb = await SqliteTestDatabase.CreateAsync(seed: false);
            _mockLogService = new Mock<IActivityLogService>();
            _mockLogService.Setup(s => s.LogActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                           .Returns(Task.CompletedTask);
        }

        public async Task DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        private IServiceScopeFactory CreateMockScopeFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_testDb.Context);
            services.AddSingleton(_mockLogService.Object);

            var serviceProvider = services.BuildServiceProvider();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(s => s.ServiceProvider).Returns(serviceProvider);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

            return mockScopeFactory.Object;
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_DoesNotThrow()
        {
            // Arrange
            var mockHasher = new Mock<IPasswordHasher>();
            mockHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var user = new Core.Entities.User
            {
                Username = "testuser",
                PasswordHash = "hashed",
                IsActive = true,
                IsDeleted = false,
                Email = "test@example.com",
                FullName = "Test User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = 1
            };
            _testDb.Context.Users.Add(user);
            await _testDb.Context.SaveChangesAsync();

            var authService = new AuthenticationService(CreateMockScopeFactory(), mockHasher.Object);

            // Act & Assert
            var result = await authService.LoginAsync("testuser", "wrongpassword");
            
            // Should not throw, should return null
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_MissingUser_DoesNotThrow()
        {
            // Arrange
            var mockHasher = new Mock<IPasswordHasher>();
            var authService = new AuthenticationService(CreateMockScopeFactory(), mockHasher.Object);

            // Act & Assert
            var result = await authService.LoginAsync("nonexistent", "password");
            
            // Should not throw, should return null
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_ActivityLogFailure_PropagatesExceptionByDesign_IfUnhandled()
        {
            // The prompt says "if design allows". Right now, if LogActivityAsync throws, 
            // the exception bubbles up, which is correct because we want to know about DB failures,
            // and the ViewModel handles it gracefully.
            _mockLogService.Setup(s => s.LogActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                   .ThrowsAsync(new Exception("DB Offline"));

            var mockHasher = new Mock<IPasswordHasher>();
            var authService = new AuthenticationService(CreateMockScopeFactory(), mockHasher.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => authService.LoginAsync("nonexistent", "password"));
        }
    }
}
