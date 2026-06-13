namespace KicsitLibrary.Core.Enums
{
    public enum BookStatus
    {
        Available,
        Issued,
        Reserved,
        Overdue,
        Lost,
        Damaged,
        UnderRepair,
        Deleted,
        Withdrawn,
        Missing
    }

    public enum MaterialType
    {
        Book,
        Journal,
        Magazine,
        Newspaper
    }

    public enum MemberType
    {
        Student,
        FacultyStaff
    }

    public enum FacultyType
    {
        PermanentFaculty,
        VisitingFaculty,
        Staff,
        Guest
    }

    public enum FineStatus
    {
        Unpaid,
        Paid,
        Partial,
        Waived
    }

    public enum AuditStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Completed,
        Closed
    }

    public enum DocumentType
    {
        LibrarySop,
        NationalLibraryRates,
        LibraryPolicy,
        AuditEvidence,
        VisitEvidence,
        Invoice,
        InventoryDocument,
        GeneralDocument
    }

    public enum InventoryItemType
    {
        Chair,
        Table,
        Cupboard,
        Rack,
        Computer,
        Printer,
        BarcodeScanner,
        Ups,
        Battery,
        Fan,
        Ac,
        Other
    }

    public enum ClearanceStatus
    {
        NotCleared,
        Cleared
    }

    public enum NotificationType
    {
        BeforeDueDateReminder,
        DueDateReminder,
        OverdueReminder,
        FinePendingReminder,
        ReservationAvailableReminder,
        ClearancePendingReminder,
        SystemAlert,
        TwoFactorOtp
    }

    public enum ReservationStatus
    {
        Pending,
        Available,
        Issued,
        Cancelled,
        Expired
    }

    public enum NotificationStatus
    {
        Pending,
        Sent,
        Failed
    }
}
