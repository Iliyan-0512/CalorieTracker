#if ANDROID || IOS || MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#endif

namespace CalorieTracker.Maui.Services;

public record PhotoData(byte[] Bytes, string MediaType);

/// <summary>Снима или избира снимка и я смалява, за да се праща бързо към AI (лимитът на API-то е 5 MB).</summary>
public static class PhotoService
{
    private const int MaxSide = 1280;
    private const int MaxBytes = 3_700_000; // след base64 остава под 5 MB

    public static async Task<PhotoData?> CaptureAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("Това устройство няма камера. Изберете снимка от галерията.");

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Няма разрешение за камерата.");

        return await ReadAsync(await MediaPicker.Default.CapturePhotoAsync());
    }

    public static async Task<PhotoData?> PickAsync()
    {
        var files = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 });
        return await ReadAsync(files?.FirstOrDefault());
    }

    private static async Task<PhotoData?> ReadAsync(FileResult? file)
    {
        if (file is null) return null;

        await using var stream = await file.OpenReadAsync();

#if ANDROID || IOS || MACCATALYST
        var image = PlatformImage.FromStream(stream);
        var resized = image.Width > MaxSide || image.Height > MaxSide ? image.Downsize(MaxSide, true) : image;
        var bytes = await resized.AsBytesAsync(ImageFormat.Jpeg, 0.8f);
        var mediaType = "image/jpeg";
#else
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var mediaType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
#endif

        if (bytes.Length > MaxBytes)
            throw new InvalidOperationException("Снимката е твърде голяма (над 3.7 MB). Изберете по-малка.");

        return new PhotoData(bytes, mediaType);
    }
}
