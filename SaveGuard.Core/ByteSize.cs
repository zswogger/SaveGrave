namespace SaveGuard.Core;

/// <summary>Formats byte counts as human-readable sizes (e.g. "426 MB", "1.8 GB").</summary>
public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {Units[unit]}" : $"{size:0.0} {Units[unit]}";
    }
}
