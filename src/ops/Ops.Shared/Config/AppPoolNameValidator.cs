using System.Text.RegularExpressions;

namespace Ops.Shared.Config;

public static class AppPoolNameValidator
{
    public const int MaxLength = 128;
    public const string InvalidMessage = "App Pool Name chỉ cho phép chữ, số, khoảng trắng và ký tự . _ - ( )";
    public const string LengthMessage = "App Pool Name tối đa 128 ký tự";

    private static readonly Regex AllowedPattern = new(@"^[\p{L}\p{N} ._()\-]+$", RegexOptions.Compiled);

    public static bool TryValidate(string? value, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            errorMessage = LengthMessage;
            return false;
        }

        if (!AllowedPattern.IsMatch(trimmed))
        {
            errorMessage = InvalidMessage;
            return false;
        }

        return true;
    }
}
