# 构建、测试和发布

Windows 使用 .NET Framework 和 WinForms 绘制桌宠。Mac 使用 AppKit 和 Core Graphics，在 Apple 芯片和 Intel 上运行同一个通用包。同一套 C# 绘制代码生成 Mac 的分层画稿和动作。

## Windows

从仓库根目录运行以下命令。编译器来自 Windows 自带的 .NET Framework 4.x。

```powershell
.\源码\build.cmd
.\源码\Test-InputPrivacy.ps1
.\scripts\test-window-lock.ps1
```

输出为 `PawPaw-Windows.exe`。输入测试使用人工构造的事件，覆盖微信输入、游戏键位、隐私状态和彩蛋。锁定测试将一个独立窗口放在猫咪后方，检查点击目标及解锁后的恢复。

## macOS

在装有 Xcode Command Line Tools 的 Mac 上运行以下命令。

```sh
sh 源码/macOS/build.sh
sh scripts/test-macos.sh "源码/macOS/build/PawPaw.app"
```

构建脚本下载配套的公开版 Mac ZIP，核对发布的 SHA-256，再提取同一版的 `Artwork.bin` 和 `Artwork.json`。已有公开 ZIP 时可以离线构建。

```sh
PAWPAW_ART_ARCHIVE="/path/to/PawPaw-macOS-universal.zip" sh 源码/macOS/build.sh
```

修改花纹、动作或衣服后，在 Windows 上运行 `scripts\export-art.cmd 输出目录`，将生成的两个文件复制到 Mac 的同一个目录，然后用 `PAWPAW_ART_DIR="/path/to/artwork" sh 源码/macOS/build.sh` 构建。两份资源必须配套。

## 发布

在 `CHANGELOG.md` 添加版本说明并同步 Windows 与 Mac 的版本号。推送形如 `v2.0.0` 的标签后，GitHub Actions 从该提交生成 Windows 程序和全新的 Mac 画稿，在 Apple 芯片和 Intel 上验证同一份 Mac 包，再发布二进制和 `SHA256SUMS.txt`。发布仅包含版本库中的公开角色，自建角色保存在用户设置中。
