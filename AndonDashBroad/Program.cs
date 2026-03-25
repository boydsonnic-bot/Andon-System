using AndonDashBroad.Data; // Thêm dòng này để Program.cs biết AndonDbContext nằm ở đâu
using Microsoft.EntityFrameworkCore;
using SharedLib.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 🟢 BẮT ĐẦU: THÊM ĐOẠN NÀY ĐỂ ĐĂNG KÝ DATABASE CONTEXT 🟢
builder.Services.AddDbContext<AndonDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// 🟢 KẾT THÚC 🟢
builder.Services.AddSingleton(new EmailSender(
    builder.Configuration["Smtp:Host"],
    builder.Configuration.GetValue<int>("Smtp:Port"),
    builder.Configuration["Smtp:User"],
    builder.Configuration["Smtp:Pass"],
    builder.Configuration["Smtp:Sender"]));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ZaloSender>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new ZaloSender(http,
        builder.Configuration["Zalo:AccessToken"],
        builder.Configuration["Zalo:OAId"]);
});

builder.Services.AddHostedService<EscalationWorker>();
builder.Services.AddHostedService<WeeklyReportWorker>();
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