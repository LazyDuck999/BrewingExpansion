# 第三方依赖说明

本仓库发布模组源码，不附带游戏程序集、MelonLoader 或其他依赖 DLL。
构建需使用本人合法安装的游戏、MelonLoader 与本机生成的 IL2CPP 包装程序集。
依赖的授权不由本项目的 MIT 许可证替代。

| 依赖 / 内容 | 本项目用途 | 原项目 / 权利信息入口 |
|---|---|---|
| Probably Stolen / Questing Goose Studio | 游戏 API 与运行环境 | 游戏发行方提供的使用条款 |
| MelonLoader | 模组加载及生命周期 | https://github.com/LavaGang/MelonLoader |
| Harmony / 0Harmony | 必要的原生入口挂钩 | https://github.com/pardeike/Harmony/blob/master/LICENSE |
| Il2CppInterop | IL2CPP 互操作与委托转换 | https://github.com/BepInEx/Il2CppInterop/blob/master/LICENSE |
| Mono.Cecil | 程序集与方法元数据处理 | https://github.com/jbevain/cecil/blob/master/LICENSE.txt |
| UnityEngine、TextMeshPro、游戏生成包装程序集 | 引用 Unity / 游戏 API | 使用本机安装所附许可与条款 |
| .NET | 构建与运行时 | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT |

资源文件名清单见 docs/assets-manifest.json。它只帮助开发者配置本地素材，不授予素材许可。
本仓库不包含其他作者的快速陈化或质量优化模组源码 / DLL，也不包含对游戏或这些模组的反编译列表。
若后续贡献包含第三方代码，请同时提供来源、授权及必须保留的声明，确认许可证兼容后再合并。
