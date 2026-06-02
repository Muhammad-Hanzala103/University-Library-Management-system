using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;

namespace KicsitLibrary.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly KicsitLibraryDbContext _context;

        public DashboardService(KicsitLibraryDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var currentYear = today.Year;

            var stats = new DashboardStats();

            try
            {
                // Book Copy Statuses
                var copyGroups = await _context.BookCopies
                    .Where(bc => !bc.IsDeleted)
                    .GroupBy(bc => bc.AvailabilityStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                stats.TotalBooks = copyGroups.Sum(cg => cg.Count);
                stats.TotalAccessionCopies = stats.TotalBooks;

                stats.AvailableBooks = copyGroups.FirstOrDefault(g => g.Status == "Available")?.Count ?? 0;
                stats.IssuedBooks = copyGroups.FirstOrDefault(g => g.Status == "Issued")?.Count ?? 0;
                stats.ReservedBooks = copyGroups.FirstOrDefault(g => g.Status == "Reserved")?.Count ?? 0;
                stats.OverdueBooks = copyGroups.FirstOrDefault(g => g.Status == "Overdue")?.Count ?? 0;
                stats.LostBooks = copyGroups.FirstOrDefault(g => g.Status == "Lost")?.Count ?? 0;
                stats.DamagedBooks = copyGroups.FirstOrDefault(g => g.Status == "Damaged")?.Count ?? 0;

                // Unique Titles
                stats.TotalUniqueTitles = await _context.BookMasters.CountAsync(bm => !bm.IsDeleted);

                // Students
                stats.TotalStudents = await _context.Students.CountAsync(s => !s.IsDeleted);
                stats.ClearedStudents = await _context.Students.CountAsync(s => !s.IsDeleted && s.ClearanceStatus == "Cleared");
                stats.NotClearedStudents = stats.TotalStudents - stats.ClearedStudents;

                // Faculty & Staff
                var facultyStaffGroups = await _context.FacultyStaff
                    .Where(fs => !fs.IsDeleted)
                    .GroupBy(fs => fs.FacultyType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                stats.TotalFaculty = facultyStaffGroups.FirstOrDefault(g => g.Type == "PermanentFaculty" || g.Type == "Permanent Faculty")?.Count ?? 0;
                stats.TotalVisitingFaculty = facultyStaffGroups.FirstOrDefault(g => g.Type == "VisitingFaculty" || g.Type == "Visiting Faculty")?.Count ?? 0;
                stats.TotalStaff = facultyStaffGroups.FirstOrDefault(g => g.Type == "Staff")?.Count ?? 0;

                // Fines
                stats.PendingFines = await _context.Fines
                    .Where(f => !f.IsDeleted && (f.PaymentStatus == "Unpaid" || f.PaymentStatus == "Partial"))
                    .SumAsync(f => f.RemainingAmount);

                stats.FineCollectedToday = await _context.Fines
                    .Where(f => !f.IsDeleted && f.PaymentDate != null && f.PaymentDate.Value.Date == today)
                    .SumAsync(f => f.PaidAmount);

                stats.FineCollectedThisMonth = await _context.Fines
                    .Where(f => !f.IsDeleted && f.PaymentDate != null && f.PaymentDate.Value >= firstDayOfMonth)
                    .SumAsync(f => f.PaidAmount);

                // Other counts
                stats.NewArrivalsThisYear = await _context.NewArrivals
                    .Where(na => !na.IsDeleted && na.PurchaseYear == currentYear)
                    .SumAsync(na => na.Quantity);

                stats.AuditRecords = await _context.AuditRecords.CountAsync(ar => !ar.IsDeleted);
                stats.VisitRecords = await _context.VisitRecords.CountAsync(vr => !vr.IsDeleted);
                
                stats.FurnitureEquipmentTotal = await _context.InventoryItems
                    .Where(ii => !ii.IsDeleted)
                    .SumAsync(ii => ii.Quantity);

                stats.PendingNotifications = await _context.NotificationRecords
                    .CountAsync(nr => !nr.IsDeleted && nr.Status == "Pending");
            }
            catch (Exception)
            {
                // Soft fallback to empty stats on failure
            }

            return stats;
        }
    }
}
