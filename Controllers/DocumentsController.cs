using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComexTool.Data;
using ComexTool.Models;
using ComexTool.Services;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ComexTool.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly ComexDbContext _db;
        private readonly TesseractOcrService _ocr;
        private readonly ComexDocumentExtractor _extractor;

        public DocumentsController(ComexDbContext db, TesseractOcrService ocr, ComexDocumentExtractor extractor)
        {
            _db = db;
            _ocr = ocr;
            _extractor = extractor;
        }

        // POST api/documents/upload - permite envio de arquivos
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] DocumentUploadDto dto)
        {
            if (dto.Files == null || dto.Files.Count == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var createdDocs = new List<object>();

            foreach (var file in dto.Files)
            {
                if (file.Length == 0) continue;

                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var content = ms.ToArray();

                // Extração completa de dados
                var (extractedText, extractedData) = _extractor.Extract(content, file.ContentType, file.FileName);

                // Se não extraiu texto e é imagem/PDF, pode tentar OCR
                if (string.IsNullOrWhiteSpace(extractedText) && (file.ContentType.StartsWith("image/") || file.ContentType == "application/pdf"))
                {
                    extractedText = await _ocr.ExtractTextAsync(new MemoryStream(content));
                }

                var doc = new Document
                {
                    Type = dto.Type,
                    FileName = file.FileName,
                    Content = content,
                    ExtractedText = extractedText,
                    ExtractedData = extractedData
                };

                _db.Documents.Add(doc);
                await _db.SaveChangesAsync();

                createdDocs.Add(new { doc.Id, doc.FileName, doc.UploadedAt });
            }

            return Ok(createdDocs);
        }

        // GET api/documents/download/{id}
        [HttpGet("download/{id:guid}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null || doc.Content == null)
                return NotFound("Documento não encontrado.");

            var contentType = doc.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) 
                ? "application/pdf" 
                : "application/octet-stream";

            return File(doc.Content, contentType, doc.FileName);
        }

        // GET api/documents  – lista todos
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var docs = await _db.Documents
                .AsNoTracking()
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new {
                    d.Id,
                    d.Type,
                    d.FileName,
                    d.UploadedAt,
                    d.ExtractedData
                })
                .ToListAsync();

            return Ok(docs);
        }

        // GET api/documents/{id} - obter dados detalhados
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            return Ok(new {
                doc.Id,
                doc.Type,
                doc.FileName,
                doc.UploadedAt,
                doc.ExtractedText,
                doc.ExtractedData
            });
        }
    }

    public class DocumentUploadDto
    {
        public DocumentType Type { get; set; }
        public List<IFormFile> Files { get; set; } = new();
    }
}
