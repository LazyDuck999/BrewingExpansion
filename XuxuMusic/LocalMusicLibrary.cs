using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace XuxuMusic;

// Only filenames and volume are saved. Songs remain local to each installation.
internal static class LocalMusicLibrary
{
    internal sealed record Song(string FileName, string FullPath, long Length, long WriteTicks)
    {
        internal string Name => Path.GetFileNameWithoutExtension(FileName);
        internal string TrackId => "be_local_mp3_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(FileName.ToUpperInvariant()))).ToLowerInvariant();
    }

    internal static string Folder(string gameDataPath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(gameDataPath))
            ?? throw new ArgumentException("Missing game directory."), "Mods", "xuxumusic");

    internal static void EnsureFolder(string gameDataPath)
    {
        var folder = Folder(gameDataPath);
        var legacy = Path.Combine(Path.GetDirectoryName(folder)!, "bemusic");
        // Rename the entire old folder when possible, retaining songs and all
        // other contents. Never replace an existing xuxumusic folder.
        if (!Directory.Exists(folder) && Directory.Exists(legacy))
            Directory.Move(legacy, folder);
        else
            Directory.CreateDirectory(folder);
    }

    internal static bool IsFileName(string name) => !string.IsNullOrEmpty(name)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.IndexOf('/') < 0 && name.IndexOf('\\') < 0
        && Path.GetFileName(name) == name && !Path.IsPathRooted(name)
        && string.Equals(Path.GetExtension(name), ".mp3", StringComparison.OrdinalIgnoreCase);

    internal static List<Song> Scan(string folder)
    {
        Directory.CreateDirectory(folder);
        var songs = new List<Song>();
        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsFileName(Path.GetFileName(path))) continue;
            try
            {
                var file = new FileInfo(path);
                if (file.Length > 0)
                    songs.Add(new Song(file.Name, file.FullName, file.Length, file.LastWriteTimeUtc.Ticks));
            }
            catch (IOException) { } // A song can be removed while the folder is being scanned.
            catch (UnauthorizedAccessException) { }
        }
        return songs.OrderBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FileName, StringComparer.Ordinal).ToList();
    }

    internal static int IndexOf(IReadOnlyList<Song> songs, string name)
    {
        for (int i = 0; i < songs.Count; i++)
            if (string.Equals(songs[i].FileName, name, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    internal static int NextIndex(int index, int offset, int count)
    {
        if (count <= 0) return -1;
        return (int)((((long)index + offset) % count + count) % count);
    }
}
