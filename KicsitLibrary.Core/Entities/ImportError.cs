namespace KicsitLibrary.Core.Entities
{
    public class ImportError
    {
        public int Id { get; set; }
        public int ImportBatchId { get; set; }
        public virtual ImportBatch ImportBatch { get; set; } = null!;
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? RawData { get; set; }
    }
}
