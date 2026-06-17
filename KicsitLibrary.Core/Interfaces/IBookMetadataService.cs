using System.Collections.Generic;
using System.Threading.Tasks;

namespace KicsitLibrary.Core.Interfaces
{
    public class BookMetadataResult
    {
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public List<string> Authors { get; set; } = new();
        public string Publisher { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PublicationPlace { get; set; } = string.Empty;
        public int? PublicationYear { get; set; }
        public string CoverImageUrl { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public string Language { get; set; } = "English";
        public string BindingType { get; set; } = "Hardcover";
        public string PhysicalDescription { get; set; } = string.Empty;
    }

    public interface IBookMetadataService
    {
        Task<BookMetadataResult?> FetchByIsbnAsync(string isbn);
    }
}
