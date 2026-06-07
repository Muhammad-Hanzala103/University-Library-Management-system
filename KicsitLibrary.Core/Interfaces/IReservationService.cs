using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IReservationService
{
    Task<ReservationEligibilityResult> CheckReservationEligibilityAsync(
        int bookMasterId,
        int memberId,
        MemberType memberType,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResult> CreateReservationAsync(
        int bookMasterId,
        int memberId,
        MemberType memberType,
        string? remarks = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationQueueItem>> GetReservationQueueAsync(
        int bookMasterId,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResult> CancelReservationAsync(
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResult> ExpireReservationAsync(
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<int> ExpireOldReservationsAsync(
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default);

    Task<ReservationAvailabilityResult> MarkReservationAvailableAsync(
        int bookMasterId,
        CancellationToken cancellationToken = default);

    Task<ReservationFulfillmentResult> FulfillReservationAsync(
        int reservationId,
        string? accessionNumber = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationQueueItem>> GetReservationsAsync(
        string? searchText = null,
        ReservationStatus? status = null,
        MemberType? memberType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationQueueItem>> GetMemberReservationsAsync(
        int memberId,
        MemberType memberType,
        CancellationToken cancellationToken = default);
}
