using XuxuMusic;

var checks = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception(description);
    checks++;
}
var root = Path.Combine(Path.GetTempPath(), "be-music-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var folder = LocalMusicLibrary.Folder(Path.Combine(root, "Game_Data"));
    Check(folder == Path.Combine(root, "Mods", "xuxumusic"), "Incorrect game-relative folder");
    var legacy = Path.Combine(root, "Mods", "bemusic");
    Directory.CreateDirectory(legacy);
    File.WriteAllBytes(Path.Combine(legacy, "旧歌曲.mp3"), new byte[] { 1, 2, 3 });
    File.WriteAllText(Path.Combine(legacy, "notes.txt"), "keep");
    LocalMusicLibrary.EnsureFolder(Path.Combine(root, "Game_Data"));
    Check(!Directory.Exists(legacy) && File.ReadAllBytes(Path.Combine(folder, "旧歌曲.mp3")).SequenceEqual(new byte[] { 1, 2, 3 })
        && File.ReadAllText(Path.Combine(folder, "notes.txt")) == "keep", "Legacy folder migration lost contents");
    File.Delete(Path.Combine(folder, "旧歌曲.mp3"));
    Directory.CreateDirectory(legacy);
    File.WriteAllText(Path.Combine(legacy, "keep.mp3"), "legacy");
    File.WriteAllText(Path.Combine(folder, "keep.mp3"), "current");
    LocalMusicLibrary.EnsureFolder(Path.Combine(root, "Game_Data"));
    Check(File.ReadAllText(Path.Combine(folder, "keep.mp3")) == "current"
        && File.ReadAllText(Path.Combine(legacy, "keep.mp3")) == "legacy", "Existing folders must not be replaced");
    File.Delete(Path.Combine(folder, "keep.mp3"));
    Check(LocalMusicLibrary.Scan(folder).Count == 0 && Directory.Exists(folder), "Missing folder should be created and empty");
    foreach (var name in new[] { "02 霓虹街道.MP3", "01 酒吧特调.mp3", "03 微醺之夜.mp3", "ignore.wav" })
        File.WriteAllBytes(Path.Combine(folder, name), new byte[] { 0x49, 0x44, 0x33 });
    File.WriteAllBytes(Path.Combine(folder, "empty.mp3"), Array.Empty<byte>());
    Directory.CreateDirectory(Path.Combine(folder, "nested"));
    File.WriteAllBytes(Path.Combine(folder, "nested", "inside.mp3"), new byte[] { 1 });
    var songs = LocalMusicLibrary.Scan(folder);
    Check(songs.Select(s => s.FileName).SequenceEqual(new[] { "01 酒吧特调.mp3", "02 霓虹街道.MP3", "03 微醺之夜.mp3" }),
        "Filename order, Unicode, uppercase MP3, empty file or recursion incorrect");
    Check(songs[0].Name == "01 酒吧特调" && songs[0].FullPath == Path.Combine(folder, songs[0].FileName), "Name or full path lost");
    Check(new Uri(songs[1].FullPath).IsFile && new Uri(songs[1].FullPath).LocalPath == songs[1].FullPath, "Unicode/space file URI does not round-trip");
    var saved = songs[1].FileName;
    var savedTrack = songs[1].TrackId;
    File.WriteAllBytes(Path.Combine(folder, "00 added.mp3"), new byte[] { 1 });
    var reordered = LocalMusicLibrary.Scan(folder);
    Check(LocalMusicLibrary.IndexOf(reordered, saved) == 2 && reordered[2].TrackId == savedTrack, "New files changed selected song identity");
    Check((songs[1] with { FullPath = Path.Combine(root, "moved", saved) }).TrackId == savedTrack,
        "Moving game folder changed track identity");
    Check((songs[1] with { FileName = saved.ToLowerInvariant() }).TrackId == savedTrack, "Windows filename case changed identity");
    var before = reordered[2];
    File.AppendAllText(before.FullPath, "new content");
    var updated = LocalMusicLibrary.Scan(folder)[2];
    Check(before != updated && before.TrackId == updated.TrackId, "Modified file cache stamp did not change");
    File.Delete(updated.FullPath);
    var deleted = LocalMusicLibrary.Scan(folder);
    Check(LocalMusicLibrary.IndexOf(deleted, saved) == 0 && deleted.Count == 3, "Removed song did not fall back safely");
    foreach (var unsafeName in new[] { "../outside.mp3", "..\\outside.mp3", Path.Combine(root, "outside.mp3"), "song.wav", "", "https://example.com/song.mp3" })
        Check(!LocalMusicLibrary.IsFileName(unsafeName), "Unsafe persisted filename accepted: " + unsafeName);
    Check(LocalMusicLibrary.NextIndex(0, -1, 3) == 2 && LocalMusicLibrary.NextIndex(2, 1, 3) == 0,
        "Previous/next did not wrap");
    Check(LocalMusicLibrary.NextIndex(0, 1, 1) == 0 && LocalMusicLibrary.NextIndex(0, 1, 0) == -1,
        "Single or empty playlist incorrect");
    Check(LocalMusicLibrary.NextIndex(int.MaxValue - 1, int.MaxValue, int.MaxValue) == int.MaxValue - 1,
        "Large index overflowed");
    Console.WriteLine("Local music filesystem and selection checks passed: " + checks);
}
finally
{
    var target = Path.GetFullPath(root);
    var parent = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(target)!);
    var expectedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
    if (!string.Equals(parent, expectedParent, StringComparison.OrdinalIgnoreCase)
        || !Path.GetFileName(target).StartsWith("be-music-check-", StringComparison.Ordinal))
        throw new Exception("Refusing to delete a test folder outside the intended temp directory.");
    Directory.Delete(target, true);
}
