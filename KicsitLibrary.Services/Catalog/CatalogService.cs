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
            await _context.Authors.AddAsync(author);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAuthorAsync(Author author)
        {
            _context.Authors.Update(author);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAuthorAsync(int id, string reason, int userId)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author != null)
            {
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
            await _context.Publishers.AddAsync(publisher);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePublisherAsync(Publisher publisher)
        {
            _context.Publishers.Update(publisher);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePublisherAsync(int id, string reason, int userId)
        {
            var publisher = await _context.Publishers.FindAsync(id);
            if (publisher != null)
            {
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

        public async Task<IEnumerable<DepartmentCategory>> GetAllDepartmentCategoriesAsync()
        {
            return await _context.DepartmentCategories.Where(d => !d.IsDeleted).OrderBy(d => d.Name).ToListAsync();
        }

        public async Task<IEnumerable<LiteratureCategory>> GetAllLiteratureCategoriesAsync()
        {
            return await _context.LiteratureCategories.Where(l => !l.IsDeleted).OrderBy(l => l.Name).ToListAsync();
        }

        public async Task<IEnumerable<Rack>> GetAllRacksAsync()
        {
            return await _context.Racks.Where(r => !r.IsDeleted).OrderBy(r => r.Name).ToListAsync();
        }

        public async Task<IEnumerable<Shelf>> GetShelvesByRackIdAsync(int rackId)
        {
            return await _context.Shelves.Where(s => !s.IsDeleted && s.RackId == rackId).OrderBy(s => s.Name).ToListAsync();
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
