# xuxu的MP3

单独分发为 `XuxuMusic.dll`，版本1.0.0，以配套酿酒拓展3.5为前置。需要 MelonLoader。

播放器代码在本项目；酿酒拓展公开 `MusicSupport`，复用原语言、贴图加载、归属判断与读档状态。`mp3.png` 留在前置中，播放器不重复嵌入资源。

```powershell
dotnet build XuxuMusic/XuxuMusic.csproj -c Release -p:GameDir='D:/Games/Probably Stolen Demo' -p:SkipGameCopy=true
```

项目引用会构建配套前置，`Private=false`，不在音乐输出目录复制前置。构建不自动修改游戏 Mods。

保留 `be_mp3_player` 和 `BrewingExpansion.DesktopMp3.*.v1` 作为旧存档兼容标识。行为和 Harmony 补丁已从前置移除。前置依赖通过 MelonAdditionalDependencies 声明；手动安装补丁前检查前置 API 和旧内置播放器，避免重复入口。

测试：`tests/ReleaseCheck`读取两份发布DLL的元数据，检查依赖、共享资源、存档兼容标识、播放器不重复和无全局OnUpdate；音乐文件/目录测试在 `BrewingExpansion/tests/LocalMusicCheck`，引用本项目的纯文件扫描逻辑。

本仓库只公开代码，贴图和音乐不包含在 MIT 授权内，详见根目录 COPYRIGHT.md。构建需配置本机游戏目录，详见 docs/BUILDING.md。
