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
        Task<Category?> GetCategoryByIdAsync(int id);
        Task AddCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(int id, string reason, int userId);
        Task<bool> IsCategoryDuplicateAsync(string name, int? excludeId = null);

        Task<IEnumerable<DepartmentCategory>> GetAllDepartmentCategoriesAsync();
        Task<DepartmentCategory?> GetDepartmentCategoryByIdAsync(int id);
        Task AddDepartmentCategoryAsync(DepartmentCategory dept);
        Task UpdateDepartmentCategoryAsync(DepartmentCategory dept);
        Task DeleteDepartmentCategoryAsync(int id, string reason, int userId);
        Task<bool> IsDepartmentCategoryDuplicateAsync(string name, int? excludeId = null);

        Task<IEnumerable<LiteratureCategory>> GetAllLiteratureCategoriesAsync();
        Task<IEnumerable<Rack>> GetAllRacksAsync();
        Task AddRackAsync(Rack rack);
        Task UpdateRackAsync(Rack rack);
        Task DeleteRackAsync(int id, string reason, int userId);
        Task<bool> IsRackDuplicateAsync(string name, int? excludeId = null);

        Task<IEnumerable<Shelf>> GetShelvesByRackIdAsync(int rackId);
        Task AddShelfAsync(Shelf shelf);
        Task UpdateShelfAsync(Shelf shelf);
        Task DeleteShelfAsync(int id, string reason, int userId);
        Task<bool> IsShelfDuplicateAsync(string name, int rackId, int? excludeId = null);

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
