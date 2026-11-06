using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ComexTool.Data;
using ComexTool.Models;
using ComexTool.Services;

namespace ComexTool.Pages;

public class ProcessDetailModel : PageModel
{
    private readonly ComexDbContext _context;
    private readonly ProcessComparisonService _comparisonService;
    private readonly ComexDocumentExtractor _extractor;

    public ProcessDetailModel(ComexDbContext context, ProcessComparisonService comparisonService, ComexDocumentExtractor extractor)
    {
        _context = context;
        _comparisonService = comparisonService;
        _extractor = extractor;
    }

    public ProcessoComex? Processo { get; set; }
    public ComparisonSummary? Comparison { get; set; }

    [BindProperty]
    public DocumentType NewDocType { get; set; }

    [BindProperty]
    public IFormFile? NewDocFile { get; set; }

    public string? Message { get; set; }
    public string? AlertClass { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Processo = await _context.Processos
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Processo == null) return NotFound();

        Comparison = _comparisonService.CompareProcessDocuments(Processo);
        return Page();
    }

    public async Task<IActionResult> OnPostReanalyzeAllAsync(Guid id)
    {
        Processo = await _context.Processos
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Processo == null) return NotFound();

        int reanalyzed = 0;
        foreach (var doc in Processo.Documentos)
        {
            if (doc.Content != null && doc.Content.Length > 0)
            {
                var (text, data) = _extractor.Extract(doc.Content, "application/pdf", doc.FileName);
                doc.ExtractedText = text;
                doc.ExtractedData = data;
                reanalyzed++;
            }
        }

        Processo.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        Message = $"{reanalyzed} documento(s) deste processo foram reanalisados com sucesso com as novas regras!";
        AlertClass = "alert-success";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteProcessAsync(Guid id)
    {
        var proc = await _context.Processos
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proc != null)
        {
            // Remove todos os documentos vinculados
            _context.Documents.RemoveRange(proc.Documentos);
            _context.Processos.Remove(proc);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Processes");
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(Guid id, Guid documentId)
    {
        var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.ProcessoId == id);
        if (doc != null)
        {
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddDocumentAsync(Guid id)
    {
        Processo = await _context.Processos
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Processo == null) return NotFound();

        if (NewDocFile != null && NewDocFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await NewDocFile.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (text, data) = _extractor.Extract(bytes, NewDocFile.ContentType, NewDocFile.FileName);

            var doc = new Document
            {
                ProcessoId = Processo.Id,
                Type = NewDocType,
                FileName = NewDocFile.FileName,
                Content = bytes,
                ExtractedText = text,
                ExtractedData = data
            };

            _context.Documents.Add(doc);
            Processo.AtualizadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }
}
