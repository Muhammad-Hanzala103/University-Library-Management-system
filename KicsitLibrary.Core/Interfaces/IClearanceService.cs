using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IClearanceService
{
    Task<ClearanceCheckResult> CheckStudentClearanceAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<ClearanceCheckResult> CheckFacultyStaffClearanceAsync(
        int facultyStaffId,
        CancellationToken cancellationToken = default);

    Task<ClearanceActionResult> ApproveStudentClearanceAsync(
        int studentId,
        string remarks,
        CancellationToken cancellationToken = default);

    Task<ClearanceActionResult> ApproveFacultyStaffClearanceAsync(
        int facultyStaffId,
        string remarks,
        CancellationToken cancellationToken = default);

    Task<ClearanceActionResult> RevokeStudentClearanceAsync(
        int studentId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ClearanceActionResult> RevokeFacultyStaffClearanceAsync(
        int facultyStaffId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClearanceHistoryItem>> GetClearanceHistoryAsync(
        CancellationToken cancellationToken = default);

    Task<ClearanceActionResult> GenerateClearanceCertificateAsync(
        Core.Enums.MemberType memberType,
        int memberId,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default);
}
