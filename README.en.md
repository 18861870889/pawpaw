# PawPaw

PawPaw (爪爪) is a desktop cat for Windows and macOS. It taps along as you type, follows your mouse, and catches a fish when you type `fish`. You can dress it up, create a cat, or leave it to groom and chase its tail.

**[Download for Windows](https://github.com/18861870889/pawpaw/releases/latest/download/PawPaw-Windows.exe)** · **[Download for Mac](https://github.com/18861870889/pawpaw/releases/latest/download/PawPaw-macOS-universal.zip)** · [All releases and checksums](https://github.com/18861870889/pawpaw/releases) · [中文](README.md)

![PawPaw jumping up to press the screen, dancing a step-touch and tracing a heart with an ice cream cone](docs/preview.gif)

## Getting started

On Windows, open the downloaded executable. On macOS 11 or later, unzip the archive, move `PawPaw.app` to Applications, and open it. The same archive runs on Apple silicon and Intel. Grant Input Monitoring in System Settings to enable typing interactions across apps. The Mac app is ad-hoc signed and has not been notarized by Apple. [Apple’s opening instructions](https://support.apple.com/en-us/102445)

Type `xiaoyu` in a text field. The last `u` makes the cat hug a fish and show 小鱼, even if you later choose the homophone 小雨. The English trigger `fish` also works. Drag the cat to move it. Right-click it or use the tray or menu bar icon to open the wardrobe (装扮工作室), pause keyboard interaction (暂停键鼠互动), or get help (帮助与反馈). The application menus are in Chinese. Choose from eleven cats, including solid black, white, orange tabby and calico. In the wardrobe or the 猫咪体型 menu, pick a petite, balanced, round, slender or sturdy build. Each cat remembers its own build and outfit. Type `jinitaimei` or `jntm` for a basketball dribble, crossover and shoulder dance. Choose 锁定桌宠（点击穿透） to send clicks and scrolls through the cat. Only the small lock button at its upper right unlocks it.

All twelve tricks also accept simplified or traditional Chinese in supported editors, including pasted words. A spelling and its matching Chinese word play once per entry, and a new entry can play again immediately. Try `yijiansanlian` for three airborne paw presses, `tiaowu` for a step-touch, or `mxbctmm` for the ice cream dance. On Mac, enable Accessibility from the word tricks menu to match committed text.

Matching stays on the device. In ordinary editable fields PawPaw checks at most eight characters before the caret against its vocabulary. It does not access the clipboard or keep typing history. In secure fields the cat covers its eyes and clears its input animations.

To build on Windows, run `源码\build.cmd`. On a Mac with Xcode Command Line Tools, run `sh 源码/macOS/build.sh`. The first Mac build fetches and verifies its versioned artwork. [Build and test details](docs/development.md) · [Report an issue](https://github.com/18861870889/pawpaw/issues/new/choose)
