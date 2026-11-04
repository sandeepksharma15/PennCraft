using System.IO;
using System.Text.Json;

namespace PennCraft.Desktop.Infrastructure;

/// <summary>Flush a complete snapshot before replacing the previous file.</summary>
internal static class AtomicJsonFile
{
    public static void Write<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            // Only this invocation's unique temporary file; never touch a previous snapshot.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
