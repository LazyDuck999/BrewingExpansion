# 酿酒拓展 / Brewing Expansion

Probably Stolen 的社区酿酒模组，维护者 **xuxu / LazyDuck999**。
当前源码版本：**酿酒拓展 3.5**；配套独立播放器：**xuxu的MP3 1.0.0**。

**本仓库只公开代码与文档，未公开贴图和音乐。** 无素材时可以编译代码，
但产物不等同于包含完整资源的可游玩发布版：图标、内置磁带播放和依赖素材的界面不能完整显示。
资源授权范围见 [COPYRIGHT.md](COPYRIGHT.md)，文件名清单见 [docs/assets-manifest.json](docs/assets-manifest.json)。

## 内容

- 酿造、种子与果实、酿酒商店、四十七和泥泥，以及外环区声望。
- 流年酒柜的批量陈化、冰镇、扩展天数与旧存档分区兼容。
- 原版酒架陈化及已核验质量模块的结算优化。
- 由游戏事件、物品操作回调和必要挂钩驱动；发布版不自行实现 OnUpdate / OnLateUpdate。
- XuxuMusic 为独立 DLL，以配套酿酒拓展为前置，从 Mods\xuxumusic 播放玩家的 MP3。

详细变更：[3.2.1 → 3.5 更新说明](docs/CHANGELOG-3.5.md)。

## 目录

```text
BrewingExpansion/BrewingExpansion/  酿酒拓展源码，输出 BrewingExpansion.dll
BrewingExpansion/tests/            纯逻辑、模拟及程序集检查工具
XuxuMusic/                        独立 MP3 源码，输出 XuxuMusic.dll
XuxuMusic/tests/                  播放器模拟 / 发布检查
docs/                            构建、更新与资源清单
scripts/build.ps1                 本地构建入口
```

## 编译

需要 Windows、.NET SDK（测试使用 .NET 10 SDK，模组目标为 net6.0）、合法安装的游戏与 MelonLoader。
先让游戏完成一次 MelonLoader 初始化，以生成 MelonLoader/Il2CppAssemblies。
游戏和依赖的 DLL 不会随仓库提供。

```powershell
pwsh ./scripts/build.ps1 -GameDir 'D:/Games/Probably Stolen Demo'
```

也可以直接构建：

```powershell
dotnet build XuxuMusic/XuxuMusic.csproj -c Release -p:GameDir='D:/Games/Probably Stolen Demo' -p:SkipGameCopy=true
```

两份 DLL 分别输出到 BrewingExpansion/BrewingExpansion/bin/Release 和 XuxuMusic/bin/Release。
默认不复制到游戏 Mods；完整本地构建及素材准备说明见 [docs/BUILDING.md](docs/BUILDING.md)。

## 测试

以下测试不需要游戏或资源文件：

```powershell
dotnet run --project BrewingExpansion/tests/AgingBatchCheck -c Release
dotnet run --project BrewingExpansion/tests/SeedOddsCheck -c Release
dotnet run --project BrewingExpansion/tests/LocalMusicCheck -c Release
```

GitHub Actions 运行这三项测试。它们检查数值和文件逻辑，不代表游戏内兼容性已经验证。
ReleaseCheck 需要本地有授权素材的完整 DLL；其他检查工具可能需要先运行各自 extract.py。
源码中的条件编译实机夹具仅供开发测试，正常发布不启用 BREWING_TEST / GLOBAL_MACHINE_TEST 等测试符号。

## 安装与兼容

有授权素材的完整构建完成后，退出游戏，将 BrewingExpansion.dll 放入 Mods。
使用 MP3 时同时放入配套 XuxuMusic.dll，自定义文件直接放入 Mods/xuxumusic。
升级前备份存档；只保留一份现用模组 DLL。旧物品 ID、内部命名及存档键保留用于兼容。
公开版 3.2.1 不具备当前独立 MP3 需要的前置接口；3.3 曾为内部测试，当前仓库为 3.5。
当前开源范围是酿酒拓展和配套 MP3，香烟拓展是另一个项目。

## 版权

代码及配套文档采用 [MIT](LICENSE)：Copyright © 2026 xuxu and contributors。
**贴图、美术与音乐不随代码开放授权；未经许可不得另行提取利用或用于 AI 训练。**
各依赖、游戏及第三方资源保留自身权利，见 [第三方依赖说明](THIRD_PARTY_NOTICES.md)。

---

Community brewing mod for Probably Stolen. Code: MIT. Artwork and music are excluded
and are not licensed under MIT. Build locally against your own game/MelonLoader references;
provide independently authorized assets for a fully functional build. See COPYRIGHT.md for details.
