using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;

namespace BrewingExpansion
{
    public static class BEState
    {
        public static Texture2D HostTex = null;
        public static readonly Dictionary<string, Sprite> GeneratedSprites = new Dictionary<string, Sprite>();
        public static readonly List<Texture2D> KeptTextures = new List<Texture2D>();
        public static GridShape WinePrinterShape3x3 = null;
        public static readonly Dictionary<string, Func<GameItem>> MidautumnDelayedFactories = new Dictionary<string, Func<GameItem>>();
    }
}