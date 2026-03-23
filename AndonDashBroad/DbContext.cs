using Microsoft.EntityFrameworkCore;
using SharedLib.Model;
 // Dùng chung model với Terminal

namespace AndonDashBroad.Data
{
    public class AndonDbContext : DbContext
    {
        public AndonDbContext(DbContextOptions<AndonDbContext> options) : base(options) { }

        public DbSet<IncidentTicket> IncidentTickets { get; set; }
    }
}