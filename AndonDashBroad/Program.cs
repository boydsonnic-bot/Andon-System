using Microsoft.EntityFrameworkCore;
using AndonDashBroad.Data; // Thêm dòng này để Program.cs biết AndonDbContext nằm ở đâu

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 🟢 BẮT ĐẦU: THÊM ĐOẠN NÀY ĐỂ ĐĂNG KÝ DATABASE CONTEXT 🟢
builder.Services.AddDbContext<AndonDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// 🟢 KẾT THÚC 🟢

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();