using System.Security.Cryptography;

namespace SmartX.Api.Services;

// AES-256 with RANDOM IV per file — fixes Part 1 feedback (was using all-zero IV). Prepends the IV to the ciphertext so decryption is possible later. Also validates file type and size.
public class EncryptionService
{
    private static readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes("SmartXKey32BytesLongSecret!!1234"); // exactly 32 bytes
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".json", ".csv", ".png", ".jpg", ".jpeg"
    };

    public (bool Ok, string? Error) ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0) return (false, "No file stream payload provided.");
        if (file.Length > MaxFileSizeBytes) return (false, $"File exceeds {MaxFileSizeBytes / 1024 / 1024} MB limit.");
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext)) return (false, $"File type '{ext}' is not allowed.");
        return (true, null);
    }

    public async Task<string> EncryptToDiskAsync(IFormFile file, string uploadsFolder)
    {
        Directory.CreateDirectory(uploadsFolder);

        // RANDOM IV per file — the crucial fix.
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();
        var iv = aes.IV;

        var safeName = Path.GetFileName(file.FileName);
        var outPath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{safeName}.enc");

        await using var fs = new FileStream(outPath, FileMode.Create);
        // Write IV first (16 bytes) so we can decrypt later
        await fs.WriteAsync(iv, 0, iv.Length);

        await using (var cs = new CryptoStream(fs, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
        {
            await file.CopyToAsync(cs);
        }
        return outPath;
    }
}