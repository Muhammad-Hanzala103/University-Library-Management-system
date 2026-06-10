using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;

namespace KicsitLibrary.Services.Catalog
{
    public class CatalogService : ICatalogService
    {
        private readonly KicsitLibraryDbContext _context;

        public CatalogService(KicsitLibraryDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ==========================================
        // AUTHORS MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<Author>> GetAllAuthorsAsync()
        {
            return await _context.Authors
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<Author?> GetAuthorByIdAsync(int id)
        {
            return await _context.Authors.FindAsync(id);
        }

        public async Task AddAuthorAsync(Author author)
        {
            if (await _context.Authors.AnyAsync(a => !a.IsDeleted && a.Name.ToLower() == author.Name.Trim().ToLower()))
            {
                throw new InvalidOperationException($"An author with the name '{author.Name}' already exists.");
            }
            await _context.Authors.AddAsync(author);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAuthorAsync(Author author)
        {
            if (await _context.Authors.AnyAsync(a => !a.IsDeleted && a.Id != author.Id && a.Name.ToLower() == author.Name.Trim().ToLower()))
            {
                throw new InvalidOperationException($"An author with the name '{author.Name}' already exists.");
            }
            _context.Authors.Update(author);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAuthorAsync(int id, string reason, int userId)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author != null)
            {
                var isLinked = await _context.BookAuthors.AnyAsync(ba => ba.AuthorId == id);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this author because they are linked to one or more books in the catalog.");
                }

                author.IsDeleted = true;
                author.DeletedAt = DateTime.UtcNow;
                author.DeletedReason = reason;
                author.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(author, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Authors",
                    RecordId = author.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // PUBLISHERS MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<Publisher>> GetAllPublishersAsync()
        {
            return await _context.Publishers
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<Publisher?> GetPublisherByIdAsync(int id)
        {
            return await _context.Publishers.FindAsync(id);
        }

        public async Task AddPublisherAsync(Publisher publisher)
        {
            if (await _context.Publishers.AnyAsync(p => !p.IsDeleted && p.Name.ToLower() == publisher.Name.Trim().ToLower()))
            {
                throw new InvalidOperationException($"A publisher with the name '{publisher.Name}' already exists.");
            }
            await _context.Publishers.AddAsync(publisher);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePublisherAsync(Publisher publisher)
        {
            if (await _context.Publishers.AnyAsync(p => !p.IsDeleted && p.Id != publisher.Id && p.Name.ToLower() == publisher.Name.Trim().ToLower()))
            {
                throw new InvalidOperationException($"A publisher with the name '{publisher.Name}' already exists.");
            }
            _context.Publishers.Update(publisher);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePublisherAsync(int id, string reason, int userId)
        {
            var publisher = await _context.Publishers.FindAsync(id);
            if (publisher != null)
            {
                var isLinked = await _context.BookMasters.AnyAsync(bm => !bm.IsDeleted && bm.PublisherId == id);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this publisher because they are linked to one or more books in the catalog.");
                }

                publisher.IsDeleted = true;
                publisher.DeletedAt = DateTime.UtcNow;
                publisher.DeletedReason = reason;
                publisher.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(publisher, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Publishers",
                    RecordId = publisher.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // METADATA LOOKUPS
        // ==========================================
        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task AddCategoryAsync(Category category)
        {
            if (await IsCategoryDuplicateAsync(category.Name))
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            if (await IsCategoryDuplicateAsync(category.Name, category.Id))
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int id, string reason, int userId)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                var isLinked = await _context.BookMasters.AnyAsync(bm => !bm.IsDeleted && bm.CategoryId == id);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this category because it is linked to one or more books in the catalog.");
                }

                var hasSubcategories = await _context.Categories.AnyAsync(c => !c.IsDeleted && c.ParentCategoryId == id);
                if (hasSubcategories)
                {
                    throw new InvalidOperationException("Cannot delete this category because it has subcategories linked to it.");
                }

                category.IsDeleted = true;
                category.DeletedAt = DateTime.UtcNow;
                category.DeletedReason = reason;
                category.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(category, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Categories",
                    RecordId = category.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsCategoryDuplicateAsync(string name, int? excludeId = null)
        {
            var query = _context.Categories.Where(c => !c.IsDeleted && c.Name.ToLower() == name.Trim().ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<DepartmentCategory>> GetAllDepartmentCategoriesAsync()
        {
            return await _context.DepartmentCategories.Where(d => !d.IsDeleted).OrderBy(d => d.Name).ToListAsync();
        }

        public async Task<DepartmentCategory?> GetDepartmentCategoryByIdAsync(int id)
        {
            return await _context.DepartmentCategories.FindAsync(id);
        }

        public async Task AddDepartmentCategoryAsync(DepartmentCategory dept)
        {
            if (await IsDepartmentCategoryDuplicateAsync(dept.Name))
            {
                throw new InvalidOperationException($"A department category with the name '{dept.Name}' already exists.");
            }
            await _context.DepartmentCategories.AddAsync(dept);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateDepartmentCategoryAsync(DepartmentCategory dept)
        {
            if (await IsDepartmentCategoryDuplicateAsync(dept.Name, dept.Id))
            {
                throw new InvalidOperationException($"A department category with the name '{dept.Name}' already exists.");
            }
            _context.DepartmentCategories.Update(dept);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteDepartmentCategoryAsync(int id, string reason, int userId)
        {
            var dept = await _context.DepartmentCategories.FindAsync(id);
            if (dept != null)
            {
                var isLinked = await _context.BookMasters.AnyAsync(bm => !bm.IsDeleted && bm.DepartmentCategoryId == id);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this department category because it is linked to one or more books in the catalog.");
                }

                dept.IsDeleted = true;
                dept.DeletedAt = DateTime.UtcNow;
                dept.DeletedReason = reason;
                dept.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(dept, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "DepartmentCategories",
                    RecordId = dept.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsDepartmentCategoryDuplicateAsync(string name, int? excludeId = null)
        {
            var query = _context.DepartmentCategories.Where(d => !d.IsDeleted && d.Name.ToLower() == name.Trim().ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(d => d.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<LiteratureCategory>> GetAllLiteratureCategoriesAsync()
        {
            return await _context.LiteratureCategories.Where(l => !l.IsDeleted).OrderBy(l => l.Name).ToListAsync();
        }

        public async Task<IEnumerable<Rack>> GetAllRacksAsync()
        {
            return await _context.Racks.Where(r => !r.IsDeleted).OrderBy(r => r.Name).ToListAsync();
        }

        public async Task AddRackAsync(Rack rack)
        {
            if (await IsRackDuplicateAsync(rack.Name))
            {
                throw new InvalidOperationException($"A rack with the name '{rack.Name}' already exists.");
            }
            await _context.Racks.AddAsync(rack);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRackAsync(Rack rack)
        {
            if (await IsRackDuplicateAsync(rack.Name, rack.Id))
            {
                throw new InvalidOperationException($"A rack with the name '{rack.Name}' already exists.");
            }
            _context.Racks.Update(rack);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRackAsync(int id, string reason, int userId)
        {
            var rack = await _context.Racks.Include(r => r.Shelves).FirstOrDefaultAsync(r => r.Id == id);
            if (rack != null)
            {
                var isLinked = await _context.BookCopies.AnyAsync(bc => !bc.IsDeleted && bc.RackNumber == rack.Name);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this rack because it is linked to one or more physical book copies.");
                }

                rack.IsDeleted = true;
                rack.DeletedAt = DateTime.UtcNow;
                rack.DeletedReason = reason;
                rack.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(rack, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Racks",
                    RecordId = rack.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsRackDuplicateAsync(string name, int? excludeId = null)
        {
            var query = _context.Racks.Where(r => !r.IsDeleted && r.Name.ToLower() == name.Trim().ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Shelf>> GetShelvesByRackIdAsync(int rackId)
        {
            return await _context.Shelves.Where(s => !s.IsDeleted && s.RackId == rackId).OrderBy(s => s.Name).ToListAsync();
        }

        public async Task AddShelfAsync(Shelf shelf)
        {
            if (await IsShelfDuplicateAsync(shelf.Name, shelf.RackId))
            {
                throw new InvalidOperationException($"A shelf with the name '{shelf.Name}' already exists in this rack.");
            }
            await _context.Shelves.AddAsync(shelf);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateShelfAsync(Shelf shelf)
        {
            if (await IsShelfDuplicateAsync(shelf.Name, shelf.RackId, shelf.Id))
            {
                throw new InvalidOperationException($"A shelf with the name '{shelf.Name}' already exists in this rack.");
            }
            _context.Shelves.Update(shelf);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteShelfAsync(int id, string reason, int userId)
        {
            var shelf = await _context.Shelves.Include(s => s.Rack).FirstOrDefaultAsync(s => s.Id == id);
            if (shelf != null)
            {
                var isLinked = await _context.BookCopies.AnyAsync(bc => !bc.IsDeleted && bc.RackNumber == shelf.Rack.Name && bc.ShelfNumber == shelf.Name);
                if (isLinked)
                {
                    throw new InvalidOperationException("Cannot delete this shelf because it is linked to one or more physical book copies.");
                }

                shelf.IsDeleted = true;
                shelf.DeletedAt = DateTime.UtcNow;
                shelf.DeletedReason = reason;
                shelf.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(shelf, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Shelves",
                    RecordId = shelf.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsShelfDuplicateAsync(string name, int rackId, int? excludeId = null)
        {
            var query = _context.Shelves.Where(s => !s.IsDeleted && s.RackId == rackId && s.Name.ToLower() == name.Trim().ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(s => s.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        // ==========================================
        // BOOK MASTERS MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<BookMaster>> SearchBooksAsync(
            string? title,
            int? authorId,
            int? categoryId,
            int? departmentCategoryId,
            int? publisherId,
            string? isbn,
            string? accessionNumber)
        {
            var query = _context.BookMasters
                .Include(bm => bm.BookAuthors).ThenInclude(ba => ba.Author)
                .Include(bm => bm.Publisher)
                .Include(bm => bm.Category)
                .Include(bm => bm.BookCopies)
                .Where(bm => !bm.IsDeleted);

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(bm => bm.Title.Contains(title) || (bm.SubTitle != null && bm.SubTitle.Contains(title)));
            }

            if (authorId.HasValue)
            {
                query = query.Where(bm => bm.BookAuthors.Any(ba => ba.AuthorId == authorId.Value));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(bm => bm.CategoryId == categoryId.Value);
            }

            if (departmentCategoryId.HasValue)
            {
                query = query.Where(bm => bm.DepartmentCategoryId == departmentCategoryId.Value);
            }

            if (publisherId.HasValue)
            {
                query = query.Where(bm => bm.PublisherId == publisherId.Value);
            }

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                query = query.Where(bm => bm.ISBN == isbn);
            }

            if (!string.IsNullOrWhiteSpace(accessionNumber))
            {
                query = query.Where(bm => bm.BookCopies.Any(bc => bc.AccessionNumber == accessionNumber && !bc.IsDeleted));
            }

            return await query.ToListAsync();
        }

        public async Task<BookMaster?> GetBookByIdAsync(int id)
        {
            return await _context.BookMasters
                .Include(bm => bm.BookAuthors).ThenInclude(ba => ba.Author)
                .Include(bm => bm.Publisher)
                .Include(bm => bm.Category)
                .Include(bm => bm.DepartmentCategory)
                .Include(bm => bm.LiteratureCategory)
                .Include(bm => bm.BookCopies)
                .FirstOrDefaultAsync(bm => bm.Id == id && !bm.IsDeleted);
        }

        public async Task AddBookAsync(BookMaster book, List<int> authorIds)
        {
            if (string.IsNullOrWhiteSpace(book.UniqueTitleNumber))
            {
                book.UniqueTitleNumber = await GenerateUniqueTitleNumberAsync();
            }

            await _context.BookMasters.AddAsync(book);
            await _context.SaveChangesAsync();

            foreach (var authorId in authorIds)
            {
                await _context.BookAuthors.AddAsync(new BookAuthor
                {
                    BookMasterId = book.Id,
                    AuthorId = authorId
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBookAsync(BookMaster book, List<int> authorIds)
        {
            _context.BookMasters.Update(book);

            var existingAuthors = await _context.BookAuthors
                .Where(ba => ba.BookMasterId == book.Id)
                .ToListAsync();

            _context.BookAuthors.RemoveRange(existingAuthors);

            foreach (var authorId in authorIds)
            {
                await _context.BookAuthors.AddAsync(new BookAuthor
                {
                    BookMasterId = book.Id,
                    AuthorId = authorId
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteBookAsync(int id, string reason, int userId)
        {
            var book = await _context.BookMasters.FindAsync(id);
            if (book != null)
            {
                book.IsDeleted = true;
                book.DeletedAt = DateTime.UtcNow;
                book.DeletedReason = reason;
                book.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(book, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "BookMasters",
                    RecordId = book.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                var copies = await _context.BookCopies.Where(bc => bc.BookMasterId == id && !bc.IsDeleted).ToListAsync();
                foreach (var copy in copies)
                {
                    copy.IsDeleted = true;
                    copy.DeletedAt = DateTime.UtcNow;
                    copy.DeletedReason = $"Parent book deleted: {reason}";
                    copy.DeletedByUserId = userId;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<string> GenerateUniqueTitleNumberAsync()
        {
            var prefix = "KICSIT-T-";
            var maxSequence = 0;

            var titleNumbers = await _context.BookMasters
                .Where(bm => bm.UniqueTitleNumber.StartsWith(prefix))
                .Select(bm => bm.UniqueTitleNumber)
                .ToListAsync();

            foreach (var tNum in titleNumbers)
            {
                var seqPart = tNum.Substring(prefix.Length);
                if (int.TryParse(seqPart, out var seqVal))
                {
                    if (seqVal > maxSequence)
                    {
                        maxSequence = seqVal;
                    }
                }
            }

            return $"{prefix}{(maxSequence + 1):D5}";
        }

        // ==========================================
        // BOOK COPIES MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<BookCopy>> GetCopiesByBookIdAsync(int bookMasterId)
        {
            return await _context.BookCopies
                .Where(bc => bc.BookMasterId == bookMasterId && !bc.IsDeleted)
                .OrderBy(bc => bc.CopyNumber)
                .ToListAsync();
        }

        public async Task<BookCopy?> GetCopyByIdAsync(int id)
        {
            return await _context.BookCopies
                .Include(bc => bc.BookMaster)
                .FirstOrDefaultAsync(bc => bc.Id == id && !bc.IsDeleted);
        }

        public async Task<BookCopy?> GetCopyByAccessionNumberAsync(string accessionNumber)
        {
            return await _context.BookCopies
                .Include(bc => bc.BookMaster)
                .FirstOrDefaultAsync(bc => bc.AccessionNumber == accessionNumber && !bc.IsDeleted);
        }

        public async Task AddCopyAsync(BookCopy copy)
        {
            if (string.IsNullOrWhiteSpace(copy.AccessionNumber))
            {
                copy.AccessionNumber = await AutoGenerateAccessionNumberAsync();
            }

            if (await IsAccessionNumberDuplicateAsync(copy.AccessionNumber))
            {
                throw new InvalidOperationException($"Duplicate Accession Number detected: {copy.AccessionNumber}");
            }

            await _context.BookCopies.AddAsync(copy);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCopyAsync(BookCopy copy)
        {
            if (await IsAccessionNumberDuplicateAsync(copy.AccessionNumber, copy.Id))
            {
                throw new InvalidOperationException($"Duplicate Accession Number detected: {copy.AccessionNumber}");
            }

            _context.BookCopies.Update(copy);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCopyAsync(int id, string reason, int userId)
        {
            var copy = await _context.BookCopies.FindAsync(id);
            if (copy != null)
            {
                copy.IsDeleted = true;
                copy.DeletedAt = DateTime.UtcNow;
                copy.DeletedReason = reason;
                copy.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(copy, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });
                
                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "BookCopies",
                    RecordId = copy.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsAccessionNumberDuplicateAsync(string accessionNumber, int? excludeCopyId = null)
        {
            var query = _context.BookCopies
                .Where(bc => bc.AccessionNumber == accessionNumber && !bc.IsDeleted);

            if (excludeCopyId.HasValue)
            {
                query = query.Where(bc => bc.Id != excludeCopyId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<string> AutoGenerateAccessionNumberAsync()
        {
            var prefix = "KICSIT-AC-";
            var maxSequence = 0;

            var accessionNumbers = await _context.BookCopies
                .Where(bc => bc.AccessionNumber.StartsWith(prefix))
                .Select(bc => bc.AccessionNumber)
                .ToListAsync();

            foreach (var accNum in accessionNumbers)
            {
                var seqPart = accNum.Substring(prefix.Length);
                if (int.TryParse(seqPart, out var seqVal))
                {
                    if (seqVal > maxSequence)
                    {
                        maxSequence = seqVal;
                    }
                }
            }

            return $"{prefix}{(maxSequence + 1):D5}";
        }
    }
}
