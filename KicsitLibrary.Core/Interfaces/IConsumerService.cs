using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IConsumerService
    {
        // ==========================================
        // STUDENTS MANAGEMENT
        // ==========================================
        Task<IEnumerable<Student>> GetAllStudentsAsync();
        Task<IEnumerable<Student>> SearchStudentsAsync(
            string? query,
            string? program,
            string? department,
            string? batch,
            ClearanceStatus? clearanceStatus,
            bool? activeStatus);
        Task<Student?> GetStudentByIdAsync(int id);
        Task<Student?> GetStudentByRegistrationNumberAsync(string registrationNumber);
        Task AddStudentAsync(Student student);
        Task UpdateStudentAsync(Student student);
        Task DeleteStudentAsync(int id, string reason, int userId);
        Task<bool> IsStudentRegistrationNumberDuplicateAsync(string regNum, int? excludeId = null);
        Task<bool> IsStudentCNICDuplicateAsync(string cnic, int? excludeId = null);
        Task<bool> IsStudentEmailDuplicateAsync(string email, int? excludeId = null);

        // ==========================================
        // FACULTY & STAFF MANAGEMENT
        // ==========================================
        Task<IEnumerable<FacultyStaff>> GetAllFacultyStaffAsync();
        Task<IEnumerable<FacultyStaff>> SearchFacultyStaffAsync(
            string? query,
            FacultyType? facultyType,
            string? department,
            bool? activeStatus);
        Task<FacultyStaff?> GetFacultyStaffByIdAsync(int id);
        Task<FacultyStaff?> GetFacultyStaffByPersonnelNumberAsync(string personnelNumber);
        Task AddFacultyStaffAsync(FacultyStaff facultyStaff);
        Task UpdateFacultyStaffAsync(FacultyStaff facultyStaff);
        Task DeleteFacultyStaffAsync(int id, string reason, int userId);
        Task<bool> IsFacultyPersonnelNumberDuplicateAsync(string personnelNum, int? excludeId = null);
        Task<bool> IsFacultyCNICDuplicateAsync(string cnic, int? excludeId = null);

        // ==========================================
        // VISITOR LOG RECORDS
        // ==========================================
        Task<IEnumerable<VisitRecord>> GetAllVisitRecordsAsync();
        Task<IEnumerable<VisitRecord>> SearchVisitRecordsAsync(
            string? organizationName,
            DateTime? date,
            string? purpose);
        Task<VisitRecord?> GetVisitRecordByIdAsync(int id);
        Task AddVisitRecordAsync(VisitRecord record);
        Task UpdateVisitRecordAsync(VisitRecord record);
        Task DeleteVisitRecordAsync(int id, string reason, int userId);
        Task<string> GenerateUniqueVisitNumberAsync();

        // ==========================================
        // VISITOR FEEDBACK MANAGEMENT
        // ==========================================
        Task<IEnumerable<VisitorFeedback>> GetAllVisitorFeedbacksAsync();
        Task AddVisitorFeedbackAsync(VisitorFeedback feedback);
        Task UpdateVisitorFeedbackAsync(VisitorFeedback feedback);

        // ==========================================
        // HISTORY & PROFILE DETAILS
        // ==========================================
        Task<IEnumerable<IssueRecord>> GetBorrowingHistoryAsync(int memberId, MemberType type);
        Task<IEnumerable<Fine>> GetFineHistoryAsync(int memberId, MemberType type);
        Task<IEnumerable<Reservation>> GetReservationHistoryAsync(int memberId, MemberType type);
    }
}
