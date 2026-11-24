# 从零创建 C# 模组

准备 .NET SDK 和支持模组的 Windows 游戏。通过 Steam 的「管理 → 浏览本地文件」找到含 `FrontierTale.exe` 的目录。以下过程使用该目录里的游戏程序集。

## 做一个能加载的最小模组

1. 新建工作目录 `HelloMod`，在里面建两个文件夹 `Source` 和 `hello-mod`。将[示例工程文件](../Examples/GamepadAimAssist/Source/GamepadAimAssist.csproj)单独复制到 `Source`，改名为 `HelloMod.csproj`。把 `CopyMod` 中的 `DestinationFolder` 改为 `$(MSBuildThisFileDirectory)../hello-mod`，把 `Version` 改为 `1.0.0`。工程文件名决定输出的 DLL 名。
2. 在 `Source` 创建 `HelloMod.cs`，写入下面的入口。宿主按清单中的完整类型名创建它。

   ```csharp
   using FrontierTale.Modding;
   using UnityEngine;

   namespace HelloMod
   {
       public sealed class Entry : GameMod
       {
           public override void Load(ModContext context)
           {
               Debug.Log("[HelloMod] 模组已加载");
           }
       }
   }
   ```

3. 在 `hello-mod` 放入自己的 `preview.png`，并将下面内容保存为 `manifest.json`。发布自己的作品时，把示例 ID 换成自己独有的 ID。

   ```json
   {
     "schemaVersion": 1,
     "type": "code",
     "sdkVersion": 1,
     "id": "lin.hello-mod",
     "title": "我的第一个模组",
     "description": "游戏启动时在日志里打个招呼。",
     "version": "1.0.0",
     "assembly": "HelloMod.dll",
     "entryType": "HelloMod.Entry"
   }
   ```

4. 在 `HelloMod` 工作目录执行下列命令，把 `GameDir` 换成自己的安装目录。编译完成后，`hello-mod` 中应有清单、封面和 `HelloMod.dll`。

   ```shell
   dotnet build Source/HelloMod.csproj -c Release "-p:GameDir=D:/SteamLibrary/steamapps/common/FrontierTale"
   ```

5. 把 `hello-mod` 复制到游戏「本地模组 → 复制路径」显示的目录。启用并重启，在游戏的 `Player.log` 搜索 `[HelloMod] 模组已加载`。Windows 上可从模组目录返回上一级找到这份日志。发布步骤见[作者指南](../README.md)。

## SDK v1 常用接口

| 需求 | 接口及生命周期 |
| --- | --- |
| 初始化、释放资源 | 在 `GameMod.Load(context)` 中注册事件，在 `Unload()` 中注销并释放作者创建的资源。 |
| 每帧读取输入 | `OnFrame()` 在已加载时调用。把初始化、磁盘读取和程序集成员查找放到合适的一次性步骤。 |
| 读取随包资源 | `context.Folder` 是当前模组目录。配置和资源的文件名写进清单的 `files` 数组。 |
| 打开自定义窗口 | `context.OpenWindow("settings")` 返回窗口根节点，可在其下放控件或预制体。窗口名在每个模组内独立；用 `context.CloseWindow("settings")` 关闭。 |
| 向已有界面加控件 | 找到游戏界面的父节点后，调用 `context.Attach(parent, name)`。辅助瞄准示例在游戏设置里加了一行可手柄导航的滑条。[设置行源码](../Examples/GamepadAimAssist/Source/AimStrengthRow.cs) |
| 跟随界面数据绑定 | 在宿主分发的 `OnViewBound(view)` 中读取 `view.View` 和 `view.Model`，用 `view.Attach` 挂载控件。跨帧回调先检查 `view.IsCurrent`；重新绑定或回收后，上一轮挂载会由宿主清理。 |

入口类型须为公开、可无参构造的 `GameMod` 子类。入口 DLL 最大 16 MiB。清单的 `files` 最多列 64 个附加文件，每个最大 64 MiB，可用扩展名为 `.cfg`、`.json`、`.bundle`、`.png`。游戏已经提供 SDK 和 Unity 程序集，发布时只带作者自己的入口 DLL 与资源。

游戏版本更新后，直接依赖游戏内部类、方法或预制体节点路径的功能需要用新版实际验证。辅助瞄准示例中，手柄开火取点和设置页导航属于这类依赖。[SDK 接口源码](../SDK/GameMod.cs) · [辅助瞄准源码](../Examples/GamepadAimAssist/Source/AimAssistMod.cs)
