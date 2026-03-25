using AndonDashBroad.Data;
using AndonDashBroad.Services;
using Microsoft.EntityFrameworkCore;
using SharedLib.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Đăng ký Analytics Service
builder.Services.AddScoped<AnalyticsService>();

// DbContext
builder.Services.AddDbContext<AndonDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Email + Zalo (an toàn null)
builder.Services.AddSingleton(new EmailSender(
    host: builder.Configuration["Smtp:Host"] ?? "",
    port: builder.Configuration.GetValue<int>("Smtp:Port", 587),
    user: builder.Configuration["Smtp:User"] ?? "",
    pass: builder.Configuration["Smtp:Pass"] ?? "",
    sender: builder.Configuration["Smtp:Sender"] ?? ""
));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ZaloSender>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new ZaloSender(
        http,
        builder.Configuration["Zalo:AccessToken"] ?? "",
        builder.Configuration["Zalo:OAId"] ?? ""
    );
});

// Background workers
builder.Services.AddHostedService<EscalationWorker>();
builder.Services.AddHostedService<WeeklyReportWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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