using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ComexTool.Models;
using ComexTool.Data;
using ComexTool.Services;

namespace ComexTool.Pages;

public class UploadModel : PageModel
{
    private readonly ComexDbContext _context;
    private readonly ComexDocumentExtractor _extractor;

    public UploadModel(ComexDbContext context, ComexDocumentExtractor extractor)
    {
        _context = context;
        _extractor = extractor;
    }

    [BindProperty]
    public DocumentType Type { get; set; }

    [BindProperty]
    public List<IFormFile> Files { get; set; } = new();

    public string? Message { get; set; }
    public string? AlertClass { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (Files == null || Files.Count == 0)
        {
            Message = "Por favor, selecione ao menos um arquivo.";
            AlertClass = "alert-danger";
            return Page();
        }

        var uploadedIds = new List<Guid>();

        foreach (var file in Files)
        {
            if (file.Length == 0) continue;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (extractedText, extractedData) = _extractor.Extract(bytes, file.ContentType, file.FileName);

            var doc = new Document
            {
                Type = Type,
                FileName = file.FileName,
                Content = bytes,
                ExtractedText = extractedText,
                ExtractedData = extractedData
            };

            _context.Documents.Add(doc);
            uploadedIds.Add(doc.Id);
        }

        await _context.SaveChangesAsync();

        if (uploadedIds.Count == 1)
        {
            // Redireciona para a página de detalhe do documento
            return RedirectToPage("/DocumentDetail", new { id = uploadedIds[0] });
        }

        Message = $"<strong>{uploadedIds.Count} documentos</strong> enviados e analisados com sucesso! Clique em cada um para ver os dados extraídos.";
        AlertClass = "alert-success";

        return RedirectToPage("/Documents");
    }
}
