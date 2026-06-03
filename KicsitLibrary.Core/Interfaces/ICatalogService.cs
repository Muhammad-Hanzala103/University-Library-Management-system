using System.Collections.Generic;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Interfaces
{
    public interface ICatalogService
    {
        // Authors Management
        Task<IEnumerable<Author>> GetAllAuthorsAsync();
        Task<Author?> GetAuthorByIdAsync(int id);
        Task AddAuthorAsync(Author author);
        Task UpdateAuthorAsync(Author author);
        Task DeleteAuthorAsync(int id, string reason, int userId);

        // Publishers Management
        Task<IEnumerable<Publisher>> GetAllPublishersAsync();
        Task<Publisher?> GetPublisherByIdAsync(int id);
        Task AddPublisherAsync(Publisher publisher);
        Task UpdatePublisherAsync(Publisher publisher);
        Task DeletePublisherAsync(int id, string reason, int userId);

        // Metadata & Locations Lookups
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<DepartmentCategory>> GetAllDepartmentCategoriesAsync();
        Task<IEnumerable<LiteratureCategory>> GetAllLiteratureCategoriesAsync();
        Task<IEnumerable<Rack>> GetAllRacksAsync();
        Task<IEnumerable<Shelf>> GetShelvesByRackIdAsync(int rackId);

        // Book Masters Management
        Task<IEnumerable<BookMaster>> SearchBooksAsync(
            string? title,
            int? authorId,
            int? categoryId,
            int? departmentCategoryId,
            int? publisherId,
            string? isbn,
            string? accessionNumber);
        
        Task<BookMaster?> GetBookByIdAsync(int id);
        Task AddBookAsync(BookMaster book, List<int> authorIds);
        Task UpdateBookAsync(BookMaster book, List<int> authorIds);
        Task DeleteBookAsync(int id, string reason, int userId);
        Task<string> GenerateUniqueTitleNumberAsync();

        // Book Copies Management
        Task<IEnumerable<BookCopy>> GetCopiesByBookIdAsync(int bookMasterId);
        Task<BookCopy?> GetCopyByIdAsync(int id);
        Task<BookCopy?> GetCopyByAccessionNumberAsync(string accessionNumber);
        Task AddCopyAsync(BookCopy copy);
        Task UpdateCopyAsync(BookCopy copy);
        Task DeleteCopyAsync(int id, string reason, int userId);
        Task<bool> IsAccessionNumberDuplicateAsync(string accessionNumber, int? excludeCopyId = null);
        Task<string> AutoGenerateAccessionNumberAsync();
    }
}
