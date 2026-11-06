using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ComexTool.Data;
using ComexTool.Models;

namespace ComexTool.Pages;

public class ProcessesModel : PageModel
{
    private readonly ComexDbContext _context;

    public ProcessesModel(ComexDbContext context)
    {
        _context = context;
    }

    public List<ProcessoComex> Processos { get; set; } = new();

    public async Task OnGetAsync()
    {
        Processos = await _context.Processos
            .Include(p => p.Documentos)
            .OrderByDescending(p => p.CriadoEm)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteProcessAsync(Guid id)
    {
        var proc = await _context.Processos
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proc != null)
        {
            _context.Documents.RemoveRange(proc.Documentos);
            _context.Processos.Remove(proc);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
