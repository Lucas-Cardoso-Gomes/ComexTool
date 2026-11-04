// Program.cs - configure services, DB, authentication, and OCR
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ComexTool.Data;
using ComexTool.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages and MVC Controllers
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Register DB context (SQLite)
builder.Services.AddDbContext<ComexDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ComexDb")));

// Register OCR service (stub implementation) and Extractor
builder.Services.AddSingleton<TesseractOcrService>();
builder.Services.AddSingleton<ComexDocumentExtractor>();

// Configure client‑certificate authentication
// builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Certificate.CertificateAuthenticationDefaults.AuthenticationScheme)
//     .AddCertificate(options =>
//     {
//         options.AllowedCertificateTypes = Microsoft.AspNetCore.Authentication.Certificate.CertificateTypes.All;
//         // TODO: custom validation (e.g., check certificate CN) if needed
//     });

// Ensure database will be created after building the app (moved below)


var app = builder.Build();

// Garante que o banco de dados SQLite e a tabela Documents sejam criados
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ComexDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// app.UseAuthentication();
// // // app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
