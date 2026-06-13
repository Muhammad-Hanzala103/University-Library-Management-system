using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace KicsitLibrary.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KicsitLibraryDbContext>
    {
        public KicsitLibraryDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<KicsitLibraryDbContext>();
            
            // We use a dummy design-time database path.
            optionsBuilder.UseSqlite("Data Source=design_time_library.db");

            return new KicsitLibraryDbContext(optionsBuilder.Options);
        }
    }
}
