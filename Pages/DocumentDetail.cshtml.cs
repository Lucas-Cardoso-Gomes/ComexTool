using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ComexTool.Data;
using ComexTool.Models;
using System.IO;
using Microsoft.AspNetCore.Http;
using ComexTool.Services;

namespace ComexTool.Pages;

public class DocumentDetailModel : PageModel
{
    private readonly ComexDbContext _context;
    private readonly ComexDocumentExtractor _extractor;

    public DocumentDetailModel(ComexDbContext context, ComexDocumentExtractor extractor)
    {
        _context = context;
        _extractor = extractor;
    }

    public Document? Doc { get; set; }

    [BindProperty]
    public DocumentType UpdatedType { get; set; }

    [BindProperty]
    public IFormFile? ReplaceFile { get; set; }

    public string? Message { get; set; }
    public string? AlertClass { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Doc = await _context.Documents
            .Include(d => d.Processo)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (Doc == null) return NotFound();
        UpdatedType = Doc.Type;
        return Page();
    }

    public async Task<IActionResult> OnPostReanalyzeAsync(Guid id)
    {
        Doc = await _context.Documents
            .Include(d => d.Processo)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (Doc == null) return NotFound();

        if (Doc.Content != null && Doc.Content.Length > 0)
        {
            var (text, data) = _extractor.Extract(Doc.Content, "application/pdf", Doc.FileName);
            Doc.ExtractedText = text;
            Doc.ExtractedData = data;
            await _context.SaveChangesAsync();
            Message = "Documento reanalisado com sucesso!";
            AlertClass = "alert-success";
        }
        else
        {
            Message = "Não há conteúdo binário armazenado para este arquivo.";
            AlertClass = "alert-warning";
        }

        return RedirectToPage(new { id });
    }

    // New handler to force OCR re‑run (useful for scanned PDFs or images)
    public async Task<IActionResult> OnPostReocrAsync(Guid id)
    {
        Doc = await _context.Documents
            .Include(d => d.Processo)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (Doc == null) return NotFound();

        if (Doc.Content != null && Doc.Content.Length > 0)
        {
            var ocrService = new TesseractOcrService();
            using var ms = new MemoryStream(Doc.Content);
            var ocrTask = ocrService.ExtractTextAsync(ms);
            ocrTask.Wait();
            var ocrText = ocrTask.Result ?? string.Empty;

            // Run normal extraction to populate structured data
            var (text, data) = _extractor.Extract(Doc.Content, "application/pdf", Doc.FileName);
            // Prefer longer OCR text if it is better
            Doc.ExtractedText = ocrText.Length > text.Length ? ocrText : text;
            Doc.ExtractedData = data;
            await _context.SaveChangesAsync();
            Message = "OCR executado e dados atualizados.";
            AlertClass = "alert-success";
        }
        else
        {
            Message = "Nenhum conteúdo binário para executar OCR.";
            AlertClass = "alert-warning";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateDocumentAsync(Guid id)
    {
        Doc = await _context.Documents
            .Include(d => d.Processo)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (Doc == null) return NotFound();

        Doc.Type = UpdatedType;

        if (ReplaceFile != null && ReplaceFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await ReplaceFile.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (text, data) = _extractor.Extract(bytes, ReplaceFile.ContentType, ReplaceFile.FileName);
            Doc.FileName = ReplaceFile.FileName;
            Doc.Content = bytes;
            Doc.ExtractedText = text;
            Doc.ExtractedData = data;
        }
        else if (Doc.Content != null && Doc.Content.Length > 0)
        {
            // Reanalyse caso tenha mudado o tipo
            var (text, data) = _extractor.Extract(Doc.Content, "application/pdf", Doc.FileName);
            Doc.ExtractedText = text;
            Doc.ExtractedData = data;
        }

        await _context.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc != null)
        {
            var procId = doc.ProcessoId;
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();

            if (procId.HasValue)
            {
                return RedirectToPage("/ProcessDetail", new { id = procId.Value });
            }
        }
        return RedirectToPage("/Documents");
    }
}
