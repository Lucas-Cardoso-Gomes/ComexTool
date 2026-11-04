using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ComexTool.Data;
using ComexTool.Models;

namespace ComexTool.Pages;

public class DocumentDetailModel : PageModel
{
    private readonly ComexDbContext _context;

    public DocumentDetailModel(ComexDbContext context)
    {
        _context = context;
    }

    public Document? Doc { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Doc = await _context.Documents.FindAsync(id);
        if (Doc == null) return NotFound();
        return Page();
    }
}
