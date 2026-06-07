namespace KicsitLibrary.Core.Entities;

public class StockVerificationSessionRecord : EntityBase
{
    public string SessionNumber { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public int StartedByUserId { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? Remarks { get; set; }
    public virtual User StartedByUser { get; set; } = null!;
    public virtual ICollection<StockVerificationEntry> Items { get; set; } = [];
}
