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

namespace KicsitLibrary.Services.Consumer
{
    public class ConsumerService : IConsumerService
    {
        private readonly KicsitLibraryDbContext _context;

        public ConsumerService(KicsitLibraryDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ==========================================
        // STUDENTS MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<Student>> GetAllStudentsAsync()
        {
            return await _context.Students
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Student>> SearchStudentsAsync(
            string? query,
            string? program,
            string? department,
            string? batch,
            ClearanceStatus? clearanceStatus,
            bool? activeStatus)
        {
            var studentQuery = _context.Students.Where(s => !s.IsDeleted);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                studentQuery = studentQuery.Where(s => 
                    s.Name.Contains(q) || 
                    s.RegistrationNumber.Contains(q) || 
                    s.RollNumber.Contains(q) || 
                    s.FatherName.Contains(q) ||
                    (s.CNIC != null && s.CNIC.Contains(q)));
            }

            if (!string.IsNullOrWhiteSpace(program))
            {
                studentQuery = studentQuery.Where(s => s.Program == program);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                studentQuery = studentQuery.Where(s => s.Department == department);
            }

            if (!string.IsNullOrWhiteSpace(batch))
            {
                studentQuery = studentQuery.Where(s => s.Batch == batch);
            }

            if (clearanceStatus.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.ClearanceStatus == clearanceStatus.Value);
            }

            if (activeStatus.HasValue)
            {
                studentQuery = studentQuery.Where(s => s.ActiveStatus == activeStatus.Value);
            }

            return await studentQuery.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            return await _context.Students
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        }

        public async Task<Student?> GetStudentByRegistrationNumberAsync(string registrationNumber)
        {
            return await _context.Students
                .FirstOrDefaultAsync(s => s.RegistrationNumber == registrationNumber && !s.IsDeleted);
        }

        public async Task AddStudentAsync(Student student)
        {
            if (await IsStudentRegistrationNumberDuplicateAsync(student.RegistrationNumber))
            {
                throw new InvalidOperationException($"Duplicate Student Registration Number detected: {student.RegistrationNumber}");
            }

            if (!string.IsNullOrWhiteSpace(student.CNIC) && await IsStudentCNICDuplicateAsync(student.CNIC))
            {
                throw new InvalidOperationException($"Duplicate CNIC detected: {student.CNIC}");
            }

            await _context.Students.AddAsync(student);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateStudentAsync(Student student)
        {
            if (await IsStudentRegistrationNumberDuplicateAsync(student.RegistrationNumber, student.Id))
            {
                throw new InvalidOperationException($"Duplicate Student Registration Number detected: {student.RegistrationNumber}");
            }

            if (!string.IsNullOrWhiteSpace(student.CNIC) && await IsStudentCNICDuplicateAsync(student.CNIC, student.Id))
            {
                throw new InvalidOperationException($"Duplicate CNIC detected: {student.CNIC}");
            }

            _context.Students.Update(student);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteStudentAsync(int id, string reason, int userId)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                student.IsDeleted = true;
                student.DeletedAt = DateTime.UtcNow;
                student.DeletedReason = reason;
                student.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(student, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });

                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "Students",
                    RecordId = student.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsStudentRegistrationNumberDuplicateAsync(string regNum, int? excludeId = null)
        {
            var query = _context.Students.Where(s => s.RegistrationNumber == regNum && !s.IsDeleted);
            if (excludeId.HasValue)
            {
                query = query.Where(s => s.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<bool> IsStudentCNICDuplicateAsync(string cnic, int? excludeId = null)
        {
            var query = _context.Students.Where(s => s.CNIC == cnic && !s.IsDeleted);
            if (excludeId.HasValue)
            {
                query = query.Where(s => s.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        // ==========================================
        // FACULTY & STAFF MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<FacultyStaff>> GetAllFacultyStaffAsync()
        {
            return await _context.FacultyStaff
                .Where(fs => !fs.IsDeleted)
                .OrderBy(fs => fs.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<FacultyStaff>> SearchFacultyStaffAsync(
            string? query,
            FacultyType? facultyType,
            string? department,
            bool? activeStatus)
        {
            var fsQuery = _context.FacultyStaff.Where(fs => !fs.IsDeleted);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                fsQuery = fsQuery.Where(fs => 
                    fs.Name.Contains(q) || 
                    fs.PersonnelNumber.Contains(q) || 
                    fs.Designation.Contains(q) ||
                    (fs.CNIC != null && fs.CNIC.Contains(q)));
            }

            if (facultyType.HasValue)
            {
                fsQuery = fsQuery.Where(fs => fs.FacultyType == facultyType.Value);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                fsQuery = fsQuery.Where(fs => fs.Department == department);
            }

            if (activeStatus.HasValue)
            {
                fsQuery = fsQuery.Where(fs => fs.ActiveStatus == activeStatus.Value);
            }

            return await fsQuery.OrderBy(fs => fs.Name).ToListAsync();
        }

        public async Task<FacultyStaff?> GetFacultyStaffByIdAsync(int id)
        {
            return await _context.FacultyStaff
                .FirstOrDefaultAsync(fs => fs.Id == id && !fs.IsDeleted);
        }

        public async Task<FacultyStaff?> GetFacultyStaffByPersonnelNumberAsync(string personnelNumber)
        {
            return await _context.FacultyStaff
                .FirstOrDefaultAsync(fs => fs.PersonnelNumber == personnelNumber && !fs.IsDeleted);
        }

        public async Task AddFacultyStaffAsync(FacultyStaff facultyStaff)
        {
            if (await IsFacultyPersonnelNumberDuplicateAsync(facultyStaff.PersonnelNumber))
            {
                throw new InvalidOperationException($"Duplicate Personnel Number detected: {facultyStaff.PersonnelNumber}");
            }

            if (!string.IsNullOrWhiteSpace(facultyStaff.CNIC) && await IsFacultyCNICDuplicateAsync(facultyStaff.CNIC))
            {
                throw new InvalidOperationException($"Duplicate CNIC detected: {facultyStaff.CNIC}");
            }

            await _context.FacultyStaff.AddAsync(facultyStaff);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateFacultyStaffAsync(FacultyStaff facultyStaff)
        {
            if (await IsFacultyPersonnelNumberDuplicateAsync(facultyStaff.PersonnelNumber, facultyStaff.Id))
            {
                throw new InvalidOperationException($"Duplicate Personnel Number detected: {facultyStaff.PersonnelNumber}");
            }

            if (!string.IsNullOrWhiteSpace(facultyStaff.CNIC) && await IsFacultyCNICDuplicateAsync(facultyStaff.CNIC, facultyStaff.Id))
            {
                throw new InvalidOperationException($"Duplicate CNIC detected: {facultyStaff.CNIC}");
            }

            _context.FacultyStaff.Update(facultyStaff);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteFacultyStaffAsync(int id, string reason, int userId)
        {
            var fs = await _context.FacultyStaff.FindAsync(id);
            if (fs != null)
            {
                fs.IsDeleted = true;
                fs.DeletedAt = DateTime.UtcNow;
                fs.DeletedReason = reason;
                fs.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(fs, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });

                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "FacultyStaff",
                    RecordId = fs.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsFacultyPersonnelNumberDuplicateAsync(string personnelNum, int? excludeId = null)
        {
            var query = _context.FacultyStaff.Where(fs => fs.PersonnelNumber == personnelNum && !fs.IsDeleted);
            if (excludeId.HasValue)
            {
                query = query.Where(fs => fs.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<bool> IsFacultyCNICDuplicateAsync(string cnic, int? excludeId = null)
        {
            var query = _context.FacultyStaff.Where(fs => fs.CNIC == cnic && !fs.IsDeleted);
            if (excludeId.HasValue)
            {
                query = query.Where(fs => fs.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        // ==========================================
        // VISITOR LOG RECORDS
        // ==========================================
        public async Task<IEnumerable<VisitRecord>> GetAllVisitRecordsAsync()
        {
            return await _context.VisitRecords
                .Include(vr => vr.CreatedByUser)
                .Where(vr => !vr.IsDeleted)
                .OrderByDescending(vr => vr.VisitDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<VisitRecord>> SearchVisitRecordsAsync(
            string? organizationName,
            DateTime? date,
            string? purpose)
        {
            var query = _context.VisitRecords
                .Include(vr => vr.CreatedByUser)
                .Where(vr => !vr.IsDeleted);

            if (!string.IsNullOrWhiteSpace(organizationName))
            {
                query = query.Where(vr => vr.OrganizationName.Contains(organizationName.Trim()));
            }

            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                query = query.Where(vr => vr.VisitDate.Date == targetDate);
            }

            if (!string.IsNullOrWhiteSpace(purpose))
            {
                query = query.Where(vr => vr.Purpose.Contains(purpose.Trim()));
            }

            return await query.OrderByDescending(vr => vr.VisitDate).ToListAsync();
        }

        public async Task<VisitRecord?> GetVisitRecordByIdAsync(int id)
        {
            return await _context.VisitRecords
                .Include(vr => vr.CreatedByUser)
                .Include(vr => vr.VisitFiles)
                .FirstOrDefaultAsync(vr => vr.Id == id && !vr.IsDeleted);
        }

        public async Task AddVisitRecordAsync(VisitRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.VisitNumber))
            {
                record.VisitNumber = await GenerateUniqueVisitNumberAsync();
            }

            await _context.VisitRecords.AddAsync(record);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateVisitRecordAsync(VisitRecord record)
        {
            _context.VisitRecords.Update(record);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteVisitRecordAsync(int id, string reason, int userId)
        {
            var record = await _context.VisitRecords.FindAsync(id);
            if (record != null)
            {
                record.IsDeleted = true;
                record.DeletedAt = DateTime.UtcNow;
                record.DeletedReason = reason;
                record.DeletedByUserId = userId;

                var serialized = JsonSerializer.Serialize(record, new JsonSerializerOptions 
                { 
                    ReferenceHandler = ReferenceHandler.IgnoreCycles 
                });

                await _context.DeletedRecordArchives.AddAsync(new DeletedRecordArchive
                {
                    TableName = "VisitRecords",
                    RecordId = record.Id,
                    SerializedData = serialized,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedReason = reason
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task<string> GenerateUniqueVisitNumberAsync()
        {
            var prefix = "KICSIT-V-";
            var maxSequence = 0;

            var visitNumbers = await _context.VisitRecords
                .Where(vr => vr.VisitNumber.StartsWith(prefix))
                .Select(vr => vr.VisitNumber)
                .ToListAsync();

            foreach (var vNum in visitNumbers)
            {
                var seqPart = vNum.Substring(prefix.Length);
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
        // HISTORY & PROFILE DETAILS
        // ==========================================
        public async Task<IEnumerable<IssueRecord>> GetBorrowingHistoryAsync(int memberId, MemberType type)
        {
            var query = _context.IssueRecords
                .Include(ir => ir.BookCopy).ThenInclude(bc => bc.BookMaster)
                .Include(ir => ir.ReceiveRecord)
                .Where(ir => !ir.IsDeleted);

            if (type == MemberType.Student)
            {
                query = query.Where(ir => ir.StudentId == memberId);
            }
            else
            {
                query = query.Where(ir => ir.FacultyStaffId == memberId);
            }

            return await query.OrderByDescending(ir => ir.IssueDate).ToListAsync();
        }

        public async Task<IEnumerable<Fine>> GetFineHistoryAsync(int memberId, MemberType type)
        {
            var query = _context.Fines
                .Include(f => f.IssueRecord).ThenInclude(ir => ir.BookCopy).ThenInclude(bc => bc.BookMaster)
                .Where(f => !f.IsDeleted);

            if (type == MemberType.Student)
            {
                query = query.Where(f => f.StudentId == memberId);
            }
            else
            {
                query = query.Where(f => f.FacultyStaffId == memberId);
            }

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<Reservation>> GetReservationHistoryAsync(int memberId, MemberType type)
        {
            var query = _context.Reservations
                .Include(r => r.BookMaster)
                .Where(r => !r.IsDeleted);

            if (type == MemberType.Student)
            {
                query = query.Where(r => r.StudentId == memberId);
            }
            else
            {
                query = query.Where(r => r.FacultyStaffId == memberId);
            }

            return await query.OrderByDescending(r => r.ReservationDate).ToListAsync();
        }
    }
}
