using System;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

namespace ComexTool.Services
{
    public class TesseractOcrService
    {
        private readonly TesseractEngine? _engine;
        private const string TessDataPath = "C:/Program Files/Tesseract-OCR/tessdata";

        public TesseractOcrService()
        {
            // Initialize TesseractEngine with Portuguese language pack.
            // If initialization fails, fall back to a no‑op engine.
            try
            {
                _engine = new TesseractEngine(TessDataPath, "por", EngineMode.Default);
            }
            catch (Exception)
            {
                // Swallow errors; OCR will be disabled.
                _engine = null;
            }
        }

        /// <summary>
        /// Runs OCR on the provided image stream and returns extracted text.
        /// Supports PNG, JPG, TIFF, BMP formats.
        /// </summary>
        public async Task<string> ExtractTextAsync(Stream imageStream)
        {
            if (_engine == null)
                return string.Empty;

            // Load the image into Pix format.
            using var pix = Pix.LoadFromMemory(ReadAllBytes(imageStream));
            using var page = _engine.Process(pix);
            var text = page.GetText();
            return await Task.FromResult(text?.Trim() ?? string.Empty);
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            // For simplicity and correctness, read the whole stream into a byte array.
            if (stream is MemoryStream ms)
                return ms.ToArray();

            using var ms2 = new MemoryStream();
            stream.CopyTo(ms2);
            return ms2.ToArray();
        }
    }
}
