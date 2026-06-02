using System;

namespace KicsitLibrary.Core.Models
{
    public class DashboardStats
    {
        public int TotalBooks { get; set; }
        public int TotalUniqueTitles { get; set; }
        public int TotalAccessionCopies { get; set; }
        public int AvailableBooks { get; set; }
        public int IssuedBooks { get; set; }
        public int ReservedBooks { get; set; }
        public int OverdueBooks { get; set; }
        public int LostBooks { get; set; }
        public int DamagedBooks { get; set; }
        
        public int TotalStudents { get; set; }
        public int ClearedStudents { get; set; }
        public int NotClearedStudents { get; set; }
        
        public int TotalFaculty { get; set; }
        public int TotalVisitingFaculty { get; set; }
        public int TotalStaff { get; set; }
        
        public decimal PendingFines { get; set; }
        public decimal FineCollectedToday { get; set; }
        public decimal FineCollectedThisMonth { get; set; }
        
        public int NewArrivalsThisYear { get; set; }
        public int AuditRecords { get; set; }
        public int VisitRecords { get; set; }
        public int FurnitureEquipmentTotal { get; set; }
        public int PendingNotifications { get; set; }
    }
}
