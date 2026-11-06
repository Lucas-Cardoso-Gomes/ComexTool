using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ComexTool.Data;
using ComexTool.Models;

namespace ComexTool.Pages;

public class DocumentsModel : PageModel
{
    private readonly ComexDbContext _context;

    public DocumentsModel(ComexDbContext context)
    {
        _context = context;
    }

    public List<Document> Documents { get; set; } = new();

    public async Task OnGetAsync()
    {
        Documents = await _context.Documents
            .Include(d => d.Processo)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(Guid id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc != null)
        {
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
