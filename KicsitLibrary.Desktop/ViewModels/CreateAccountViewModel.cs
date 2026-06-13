using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class CreateAccountViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPasswordHasher _passwordHasher;

        public CreateAccountViewModel(IServiceScopeFactory scopeFactory, IPasswordHasher passwordHasher)
        {
            _scopeFactory = scopeFactory;
            _passwordHasher = passwordHasher;
            Roles = new List<string> { "Student", "Faculty", "Staff", "Visitor Account Request", "Admin Request", "Librarian", "Assistant Librarian" };
        }

        [ObservableProperty]
        private string fullName = string.Empty;

        [ObservableProperty]
        private string username = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string phoneNumber = string.Empty;

        [ObservableProperty]
        private string selectedRole = "Student";

        [ObservableProperty]
        private string linkedId = string.Empty;

        [ObservableProperty]
        private string programOrDepartment = string.Empty;

        [ObservableProperty]
        private string remarks = string.Empty;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private string successMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public List<string> Roles { get; }

        public Action? RequestCompleted { get; set; }

        public async Task<bool> SubmitRequestAsync(string password, string confirmPassword)
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Full Name, Username, and Password are required.";
                return false;
            }

            if (password != confirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return false;
            }

            IsBusy = true;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();

                    var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Username == Username || (u.Email == Email && !string.IsNullOrWhiteSpace(Email)));
                    if (existingUser != null)
                    {
                        ErrorMessage = "Username or Email already exists.";
                        IsBusy = false;
                        return false;
                    }

                    var newUser = new User
                    {
                        FullName = FullName,
                        Username = Username,
                        Email = Email,
                        PasswordHash = _passwordHasher.HashPassword(password),
                        CreatedAt = DateTime.UtcNow,
                        IsActive = false,
                        IsDeleted = false
                    };

                    // For the dynamic schema we need to use the context cautiously, we assign some generic properties if available
                    var entry = context.Users.Add(newUser);
                    await context.SaveChangesAsync();
                    
                    // We can patch extra fields using raw SQL since they might not be in the model
                    var status = "PendingApproval";
                    var role = SelectedRole;
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE Users SET AccountStatus = {0}, Role = {1}, LinkedFacultyStaffId = {2}, PhoneNumber = {3} WHERE Id = {4}", 
                        status, role, LinkedId, PhoneNumber, newUser.Id);
                }

                SuccessMessage = "Account request submitted. Contact administrator for approval.";
                IsBusy = false;
                
                await Task.Delay(2000);
                RequestCompleted?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to submit request: " + ex.Message;
                IsBusy = false;
                return false;
            }
        }
    }
}
