using Microsoft.EntityFrameworkCore;
using EXE.Data;
using EXE.Models;
using EXE.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<BankTransferSettings>(builder.Configuration.GetSection("BankTransfer"));
builder.Services.AddScoped<IBankTransferService, BankTransferService>();
builder.Services.AddHttpClient<ISePayTransactionLookupService, SePayTransactionLookupService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.Configure<GHNSettings>(builder.Configuration.GetSection("GHN"));
builder.Services.AddHttpClient<IGHNService, GHNService>();

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.UseStaticFiles();

app.UseSession();

app.UseRouting();

// Seed minimal data so location suggestions & chat work out-of-box.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
