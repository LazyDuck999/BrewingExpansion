# 本地构建说明

## 环境

1. 安装 .NET SDK。本仓库纯逻辑测试目标为 net10.0，正式模组目标为 net6.0。
2. 安装游戏和适配的 MelonLoader，并至少启动一次游戏，完成本机 IL2CPP 包装程序集生成。
3. 不要把 MelonLoader/net6、MelonLoader/Il2CppAssemblies 或任何游戏 DLL 提交到 Git。

GameDir 可用命令行 `-p:GameDir=...`，或环境变量 PROBABLY_STOLEN_GAME_DIR 指定。
构建前会检查 MelonLoader.dll 和 Assembly-CSharp.dll 是否存在。

## 素材

公开源码不含媒体。资源清单 docs/assets-manifest.json 列出原构建中使用的 PNG / MP3 文件名。
取得独立合法授权后，按清单的相对路径放入：

- BrewingExpansion/BrewingExpansion/Textures/**/*.png
- BrewingExpansion/BrewingExpansion/music/*.mp3

项目会自动把这些本地文件嵌入 BrewingExpansion.dll。MP3 插件复用前置中的 mp3.png，不重复嵌入。
缺少素材的编译可以检查 C# 代码，但不能作为完整游戏发布版本；部分图标与内置磁带不会正常显示 / 播放。
.gitignore 默认排除媒体，取得授权不等于可以直接公开再分发。

## 构建命令

```powershell
pwsh ./scripts/build.ps1 -GameDir 'D:/Games/Probably Stolen Demo'
```

脚本只编译，不修改 Mods。正式部署请退出游戏，再手动复制两份 DLL；MP3 是可选插件。
不要在发布构建中定义实机夹具符号；它们可以创建临时场景并阻止保存，仅用于隔离测试。

## 验证完整资源构建

```powershell
dotnet run --project XuxuMusic/tests/ReleaseCheck -c Release --   XuxuMusic/bin/Release/XuxuMusic.dll   BrewingExpansion/BrewingExpansion/bin/Release/BrewingExpansion.dll   BrewingExpansion/BrewingExpansion/Textures/mp3.png
```

该检查需要完整、有授权的贴图和内置 MP3；仅代码构建不满足此检查。

## 测试范围

- AgingBatchCheck：批量陈化计划、溢出边界与对照数值。
- SeedOddsCheck：稀有度概率、保底计数与封顶。
- LocalMusicCheck：本地 MP3 文件夹扫描、文件名排序与选择逻辑。
- ReleaseCheck：完整 DLL 的资源、前置 API、保存标识及挂钩检查。

原生游戏回调与第三方模组组合仍需实机验证。纯逻辑测试通过不代表任意游戏版本或模组组合已适配。
