using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class User : EntityBase
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }

        // Forgot Password
        public string? PasswordResetTokenHash { get; set; }
        public System.DateTime? PasswordResetTokenExpiresAt { get; set; }
        
        // 2FA
        public bool IsTwoFactorEnabled { get; set; }
        public string? TwoFactorMethod { get; set; }
        public string? PendingOtpHash { get; set; }
        public System.DateTime? PendingOtpExpiresAt { get; set; }
        public int PendingOtpAttempts { get; set; }

        public string? TwoFactorEmail { get; set; }
        public System.DateTime? PendingOtpCreatedAt { get; set; }
        public bool PasswordResetTokenUsed { get; set; }
        public System.DateTime? PasswordResetRequestedAt { get; set; }
        public System.DateTime? LastLoginAt { get; set; }
        public string? ExternalProvider { get; set; }
        public string? ExternalProviderUserId { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool CreatedByAdmin { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public int? LinkedStudentId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AccountStatus { get; set; }
        public string? Role { get; set; }
        public string? LinkedFacultyStaffId { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
