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

    public enum NotificationType
    {
        BeforeDueDateReminder,
        DueDateReminder,
        OverdueReminder,
        FinePendingReminder,
        ReservationAvailableReminder,
        ClearancePendingReminder
    }

    public enum ReservationStatus
    {
        Pending,
        Available,
        Issued,
        Cancelled,
        Expired
    }
}
