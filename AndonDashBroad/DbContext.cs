using Microsoft.EntityFrameworkCore;
using SharedLib.Model;

namespace AndonDashBroad.Data
{
    public class AndonDbContext : DbContext
    {
        public AndonDbContext(DbContextOptions<AndonDbContext> options) : base(options) { }

        public DbSet<IncidentTicket> IncidentTickets { get; set; }

        // ────────────────────────────────────────────────────────────
        // THÊM HÀM NÀY ĐỂ ÉP EF CORE MAP ĐÚNG TÊN BẢNG TRONG SQLITE
        // ────────────────────────────────────────────────────────────
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Chỉ định rõ class IncidentTicket móc nối vào bảng "Tickets"
            modelBuilder.Entity<IncidentTicket>().ToTable("Tickets");
        }
    }
}