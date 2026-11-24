# 《开拓物语》模组作者套件

用支持模组的 Windows x64 Mono 游戏版本（SDK v1），可以在本地修改、启用模组，再从游戏内投稿到 Steam 创意工坊。改配方只需要文本编辑器，写 C# 功能还需要 .NET SDK。下面两个成品可以直接安装。

| 想做什么 | 从哪里开始 | 在游戏里看什么 |
| --- | --- | --- |
| 修改现有配方 | [见习短弓的成品文件夹](Examples/RecipeMaterials/ReleaseExample/recipe-template) · [材料清单](Examples/RecipeMaterials/ReleaseExample/recipe-template/recipes.json) | 手工台显示木材 ×2、麻绳 ×5 |
| 用 C# 改变操作、在设置页加控件 | [手柄辅助瞄准 1.0.1 成品](Examples/GamepadAimAssist/ReleaseExample/gamepad-aim-assist) · [源码](Examples/GamepadAimAssist/Source) · [编译和手柄测试](Examples/GamepadAimAssist/README.md) | 「设置 → 游戏」出现辅助瞄准强度，100% 时用远程武器开火会吸附附近的可见怪物 |

第一次编写代码模组，可以先做一个在游戏日志中打招呼的小模组，再接入窗口或现有 UI。[从零创建 C# 模组与 SDK 接口](Reference/代码模组与SDK.md)

模组可以只用作者编写的一种语言，也可以让设置项和窗口跟随游戏语言切换。[模组多语言指南](Reference/模组多语言.md)

## 先在本地跑通

1. 下载作者套件 ZIP 并解压。在 GitHub 上也可以选 **Code → Download ZIP**。
2. 在新版 Windows 游戏中，手动安装目录是 `游戏安装目录/FrontierTale_Data/Mods`。在 Steam 库右键游戏，选 **管理 → 浏览本地文件**，打开 `FrontierTale_Data`，如果没有 `Mods` 文件夹就新建一个。也可以在游戏内选 **模组 → 本地模组 → 复制路径**，查看当前版本实际使用的目录。把上表中的一个**成品文件夹**复制进去。

   ```text
   FrontierTale_Data/Mods/
     recipe-template/
       manifest.json
       recipes.json
       preview.png
   ```

3. 重新打开模组管理，勾选该模组，退出整个游戏并重新启动。用测试存档核对上表的效果。如果同时安装了相同 ID 的订阅副本，这次测试只启用本地这份。

## 制作自己的配方

复制 `recipe-template`，给文件夹起自己的名字，例如 `cheap-bow`。打开 `manifest.json`，将 `id` 改成自己的唯一标识（例如 `lin.cheap-bow`），填写自己的 `title`、`description` 和 `version`，将 `preview.png` 换成自己的封面。[示例清单](Examples/RecipeMaterials/ReleaseExample/recipe-template/manifest.json)

例如，把 `recipes.json` 改成下面这样，见习短弓就会需要 **木材 ×3、麻绳 ×2**。每条记录给出该配方的完整材料列表，每种道具写一次，数量填正整数。

```json
[
  { "configID": 16000050, "materials": "10000001_3,10000040_2" }
]
```

按中文名查[配方 ID](Reference/配方ID.md) 和[道具与材料 ID](Reference/道具与材料ID.md)。本地测试时只启用新模组，重启后在手工台核对数值。

## 投稿目录格式

| 类型 | 清单 | 同目录文件 |
| --- | --- | --- |
| 配方 | `schemaVersion: 1`、`type: "recipes"`、作品信息 | `recipes.json`、`preview.png` |
| C# | `schemaVersion: 1`、`type: "code"`、`sdkVersion: 1`、作品信息、`assembly`、`entryType` | 入口 DLL、`preview.png`、`files` 中列出的资源。[完整示例](Examples/GamepadAimAssist/ReleaseExample/gamepad-aim-assist/manifest.json) |

作品信息包括 `id`、`title`、`description`、`version`。ID 长度为 1–100 个字符，使用英文字母、数字、点、短横线和下划线。同一作品升级时保留 ID。JSON 用 UTF-8 保存。封面使用小于 1 MiB 的 PNG，宽和高各不超过 2048 像素。

## 上传、订阅和更新

1. 登录自己的 Steam 账号，在 **模组 → 本地模组** 选中作品，点 **发布到创意工坊**。核对标题、描述、封面和可见度，首次自测可以选「私有」。
2. 点 **确认上传 → 查看工坊页面**，核对作者并记下作品编号。首次投稿如果出现 Steam 创意工坊协议，按页面提示完成。
3. 验证订阅时，在作品页点「订阅」，等游戏的「已订阅」页显示下载完成。停用同 ID 的本地模组，启用订阅版，重启游戏并核对实际效果。跨账号验收时，给测试者选择可访问的作品可见度。
4. 更新时保留原 `id`，提高 `version`，改好内容并在本地验证。用**原投稿账号和原投稿电脑**再次发布，核对作品编号与更新记录。游戏在本机按账号和模组 ID 保存投稿关联。

找不到本地模组时，先核对复制出来的路径，确认从 `Mods` 往下一级就是含有 `manifest.json` 的文件夹。订阅后仍显示旧效果时，先核对作品编号和下载状态，再检查本次启用的副本，并重启游戏。
