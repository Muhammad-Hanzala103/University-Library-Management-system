using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IStockVerificationService
{
    Task<IReadOnlyList<StockVerificationItem>> GetStockVerificationItemsAsync(StockVerificationFilter filter, CancellationToken cancellationToken = default);
    Task<StockVerificationResult> StartVerificationSessionAsync(string remarks, CancellationToken cancellationToken = default);
    Task<StockVerificationResult> VerifyBookCopyAsync(int sessionId, int bookCopyId, BookStatus actualStatus, string remarks, bool reconcile, string reconciliationReason, CancellationToken cancellationToken = default);
    Task<StockVerificationResult> BulkMarkUnverifiedAsync(int sessionId, string remarks, CancellationToken cancellationToken = default);
    Task<StockVerificationResult> CompleteVerificationSessionAsync(int sessionId, string remarks, CancellationToken cancellationToken = default);
    Task<StockVerificationSummary> GetStockVerificationSummaryAsync(int sessionId, CancellationToken cancellationToken = default);
}
