using System;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

namespace ComexTool.Services
{
    public class TesseractOcrService
    {
        // NOTE: Leptonica native DLL is not available on this platform.
        // For now we provide a stub implementation that returns an empty string.
        // This allows the web app to run without OCR functionality.
        public TesseractOcrService()
        {
            // No initialization of TesseractEngine to avoid DllNotFoundException.
        }

        public Task<string> ExtractTextAsync(Stream imageStream)
        {
            // Return empty text as OCR placeholder.
            return Task.FromResult(string.Empty);
        }
    }
}
