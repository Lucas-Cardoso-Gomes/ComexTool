using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

    // Campos do Processo
    [BindProperty]
    public string? NumeroProcesso { get; set; }

    [BindProperty]
    public string? DescricaoProcesso { get; set; }

    [BindProperty]
    public Guid? ProcessoExistenteId { get; set; }

    // Upload por Pasta no Servidor/Disco Local
    [BindProperty]
    public string? LocalFolderPath { get; set; }

    // Upload por Arquivos da Pasta (via input webkitdirectory)
    [BindProperty]
    public List<IFormFile> FolderFiles { get; set; } = new();

    // Upload Avulso Individual
    [BindProperty]
    public DocumentType IndividualType { get; set; }

    [BindProperty]
    public List<IFormFile> IndividualFiles { get; set; } = new();

    // Upload por Processo - Documentos Separados
    [BindProperty]
    public List<IFormFile>? Faturas { get; set; }

    [BindProperty]
    public List<IFormFile>? PackingLists { get; set; }

    [BindProperty]
    public List<IFormFile>? NotasFiscais { get; set; }

    [BindProperty]
    public List<IFormFile>? CSIs { get; set; }

    [BindProperty]
    public List<IFormFile>? CertificadosOrigem { get; set; }

    [BindProperty]
    public List<IFormFile>? CRTs { get; set; }

    [BindProperty]
    public List<IFormFile>? MIC_DTAs { get; set; }

    [BindProperty]
    public List<IFormFile>? DUEs { get; set; }

    [BindProperty]
    public List<IFormFile>? OutrosDocumentos { get; set; }

    public List<ProcessoComex> ProcessosExistentes { get; set; } = new();

    public string? Message { get; set; }
    public string? AlertClass { get; set; }

    public async Task OnGetAsync()
    {
        ProcessosExistentes = await _context.Processos.OrderByDescending(p => p.CriadoEm).ToListAsync();
    }

    // =========================================================================
    // 1. IMPORTAÇÃO POR PASTA (Automática: Lê todos os arquivos, classifica e analisa)
    // =========================================================================
    public async Task<IActionResult> OnPostFolderUploadAsync()
    {
        var filesToProcess = new List<(string FileName, byte[] Bytes)>();
        string? suggestedProcessNum = null;

        // Opção A: Caminho no disco (ex: C:\Users\...\0110958E26 - CH)
        if (!string.IsNullOrWhiteSpace(LocalFolderPath))
        {
            var trimmed = LocalFolderPath.Trim().Trim('"', '\'');
            if (Directory.Exists(trimmed))
            {
                suggestedProcessNum = Path.GetFileName(trimmed);
                var diskFiles = Directory.GetFiles(trimmed, "*.*")
                    .Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase));

                foreach (var df in diskFiles)
                {
                    try
                    {
                        var bytes = await System.IO.File.ReadAllBytesAsync(df);
                        if (bytes.Length > 0)
                            filesToProcess.Add((Path.GetFileName(df), bytes));
                    }
                    catch { }
                }
            }
            else
            {
                Message = $"A pasta informada não foi encontrada: {trimmed}";
                AlertClass = "alert-danger";
                ProcessosExistentes = await _context.Processos.OrderByDescending(p => p.CriadoEm).ToListAsync();
                return Page();
            }
        }

        // Opção B: Arquivos enviados pelo seletor de pasta do navegador (webkitdirectory)
        if (FolderFiles != null && FolderFiles.Count > 0)
        {
            foreach (var f in FolderFiles)
            {
                if (f.Length == 0) continue;
                using var ms = new MemoryStream();
                await f.CopyToAsync(ms);
                filesToProcess.Add((f.FileName, ms.ToArray()));
            }
        }

        if (filesToProcess.Count == 0)
        {
            Message = "Nenhum arquivo encontrado na pasta selecionada.";
            AlertClass = "alert-danger";
            ProcessosExistentes = await _context.Processos.OrderByDescending(p => p.CriadoEm).ToListAsync();
            return Page();
        }

        // Cria ou localiza o processo
        ProcessoComex? processo = null;
        if (ProcessoExistenteId.HasValue && ProcessoExistenteId.Value != Guid.Empty)
        {
            processo = await _context.Processos.FindAsync(ProcessoExistenteId.Value);
        }

        if (processo == null)
        {
            var num = !string.IsNullOrWhiteSpace(NumeroProcesso)
                ? NumeroProcesso.Trim()
                : (!string.IsNullOrWhiteSpace(suggestedProcessNum) ? suggestedProcessNum : $"PROC-{DateTime.UtcNow:yyyyMMdd-HHmmss}");

            processo = new ProcessoComex
            {
                NumeroProcesso = num,
                Descricao = DescricaoProcesso ?? $"Processo importado automaticamente da pasta: {suggestedProcessNum ?? "pasta local"}",
                CriadoEm = DateTime.UtcNow
            };
            _context.Processos.Add(processo);
            await _context.SaveChangesAsync();
        }

        // Processa cada arquivo da pasta com classificação automática
        foreach (var (fileName, bytes) in filesToProcess)
        {
            // Extrai texto para classificação e preenchimento de campos
            string contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "image/jpeg";
            var (text, data) = _extractor.Extract(bytes, contentType, fileName);

            // Classifica automaticamente o tipo do documento (Fatura, NF-e, DUE, CO, CRT, Packing List, etc.)
            var detectedType = DocumentClassifier.Classify(fileName, text);

            var doc = new Document
            {
                ProcessoId = processo.Id,
                Type = detectedType,
                FileName = fileName,
                Content = bytes,
                ExtractedText = text,
                ExtractedData = data
            };

            _context.Documents.Add(doc);
        }

        processo.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToPage("/ProcessDetail", new { id = processo.Id });
    }

    // =========================================================================
    // 2. UPLOAD POR DOCUMENTOS ESPECÍFICOS DO PROCESSO
    // =========================================================================
    public async Task<IActionResult> OnPostProcessUploadAsync()
    {
        ProcessoComex? processo = null;

        if (ProcessoExistenteId.HasValue && ProcessoExistenteId.Value != Guid.Empty)
        {
            processo = await _context.Processos.FindAsync(ProcessoExistenteId.Value);
        }

        if (processo == null)
        {
            var num = string.IsNullOrWhiteSpace(NumeroProcesso)
                ? $"PROC-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
                : NumeroProcesso.Trim();

            processo = new ProcessoComex
            {
                NumeroProcesso = num,
                Descricao = DescricaoProcesso,
                CriadoEm = DateTime.UtcNow
            };
            _context.Processos.Add(processo);
            await _context.SaveChangesAsync();
        }

        var filesToProcess = new List<(DocumentType Type, IFormFile File)>();

        void AddFiles(DocumentType type, List<IFormFile>? files)
        {
            if (files != null)
            {
                foreach (var f in files)
                {
                    if (f.Length > 0) filesToProcess.Add((type, f));
                }
            }
        }

        AddFiles(DocumentType.Fatura, Faturas);
        AddFiles(DocumentType.PackingList, PackingLists);
        AddFiles(DocumentType.NotaFiscal, NotasFiscais);
        AddFiles(DocumentType.CSI, CSIs);
        AddFiles(DocumentType.CertificadoOrigem, CertificadosOrigem);
        AddFiles(DocumentType.CRT, CRTs);
        AddFiles(DocumentType.MIC_DTA, MIC_DTAs);
        AddFiles(DocumentType.DUE, DUEs);
        AddFiles(DocumentType.Outro, OutrosDocumentos);

        if (filesToProcess.Count == 0)
        {
            Message = "Nenhum arquivo foi selecionado para upload.";
            AlertClass = "alert-danger";
            ProcessosExistentes = await _context.Processos.OrderByDescending(p => p.CriadoEm).ToListAsync();
            return Page();
        }

        foreach (var (type, file) in filesToProcess)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (text, data) = _extractor.Extract(bytes, file.ContentType, file.FileName);

            var doc = new Document
            {
                ProcessoId = processo.Id,
                Type = type,
                FileName = file.FileName,
                Content = bytes,
                ExtractedText = text,
                ExtractedData = data
            };

            _context.Documents.Add(doc);
        }

        processo.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToPage("/ProcessDetail", new { id = processo.Id });
    }

    // =========================================================================
    // 3. UPLOAD AVULSO INDIVIDUAL
    // =========================================================================
    public async Task<IActionResult> OnPostIndividualUploadAsync()
    {
        if (IndividualFiles == null || IndividualFiles.Count == 0)
        {
            Message = "Por favor, selecione ao menos um arquivo.";
            AlertClass = "alert-danger";
            ProcessosExistentes = await _context.Processos.OrderByDescending(p => p.CriadoEm).ToListAsync();
            return Page();
        }

        var uploadedIds = new List<Guid>();

        foreach (var file in IndividualFiles)
        {
            if (file.Length == 0) continue;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var (extractedText, extractedData) = _extractor.Extract(bytes, file.ContentType, file.FileName);

            var doc = new Document
            {
                Type = IndividualType,
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
            return RedirectToPage("/DocumentDetail", new { id = uploadedIds[0] });
        }

        return RedirectToPage("/Documents");
    }
}
