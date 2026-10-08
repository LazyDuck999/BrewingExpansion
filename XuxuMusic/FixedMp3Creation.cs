using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace XuxuMusic;

// The scene calls CreateFixed directly. Only saved-node decoding may resolve
// an old MP3 item through DirectoryMaster; spawn/catalogue requests cannot.
internal static class FixedMp3Creation
{
    [ThreadStatic] private static int _decodeDepth;
    [ThreadStatic] private static List<GameItem> _savedPlayers;
    internal static bool DecodingSave => _decodeDepth > 0;
    internal static void BeginDecode()
    {
        if (_decodeDepth++ == 0) _savedPlayers = new List<GameItem>();
    }
    internal static void Track(GameItem item) => _savedPlayers?.Add(item);
    internal static void EndDecode(bool success)
    {
        _decodeDepth = Math.Max(0, _decodeDepth - 1);
        if (_decodeDepth != 0) return;
        var items = _savedPlayers;
        _savedPlayers = null;
        // Native decoding restores sprite paths, tags and content windows after
        // creating the item. Normalize only these legacy players once decoding
        // completes, independently of BrewingExpansion's maintenance sweep.
        if (!success || items == null) return;
        foreach (var item in items)
        {
            try { LocalMp3Player.EnsureLoaded(item); }
            catch (Exception ex) { Core.Log.Warning("Saved MP3 restoration: " + ex.Message); }
        }
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
internal static class Mp3SavedNodeScopePatch
{
    private static void Prefix(ref bool __state)
    {
        FixedMp3Creation.BeginDecode();
        __state = true;
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) FixedMp3Creation.EndDecode(__exception == null);
        return __exception;
    }
}

[HarmonyPatch(typeof(DirectoryMaster), nameof(DirectoryMaster.Item))]
internal static class Mp3FixedCreationPatch
{
    private static bool Prefix(string identifier, ref GameItem __result)
    {
        if (identifier != LocalMp3Player.Id) return true;
        __result = FixedMp3Creation.DecodingSave ? LocalMp3Player.CreateFixed() : null;
        if (__result != null) FixedMp3Creation.Track(__result);
        return false;
    }
}
