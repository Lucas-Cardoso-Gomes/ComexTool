using Microsoft.AspNetCore.Mvc.RazorPages;
using ComexTool.Models;
using ComexTool.Data;
using Microsoft.EntityFrameworkCore;

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
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }
}
