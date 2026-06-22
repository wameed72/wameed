using System.Security.Cryptography;
using System.Text;

namespace ErrorDoctor.Core.Sync;

public static class ContentHasher
{
    public static string Compute(params string?[] parts)
    {
        var joined = string.Join("\u001f", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return System.Convert.ToHexString(bytes);
    }

    public static string ForDto(ErrorEntryDto dto) =>
        Compute(dto.ErrorCode, dto.Title, dto.Category, dto.Signature, dto.Cause, dto.Solution, dto.Source, dto.SourceUrl, dto.Tags, dto.Severity);
}
