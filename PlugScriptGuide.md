# PlugScript 插件开发指南（v0.1）

PlugScript 是 PCL - DTA 使用的插件脚本语言。它的写法接近 JavaScript，但**不是 JavaScript**：PlugScript 按行解析，使用 `island` 表达嵌套结构，并且只支持本指南列出的语法和内置 API。

本指南面向两类读者：

- 想为自己的 PCL - DTA 写插件的用户；
- 需要根据需求生成插件的 AI 助手。

> 当前版本为 **v0.1**。请只使用本指南明确写出的语法。未列出的 JavaScript 语法不保证可用。

---

## 1. 简介

一个 PlugScript 插件是一个文件夹，入口文件固定为 `plugin.plugscript`。插件可以：

- 声明一个独立窗口（`UI.type = "new_window"`）；
- 在首次加载时设置窗口标题、尺寸和存储配额；
- 通过 `island` 声明需要申请的权限；
- 使用 `print` / `log` 输出日志；
- 使用 `Storage` 在自己的插件目录内读写小型文本数据；
- 使用 `Permission.has` 查询权限是否已授予。

v0.1 不提供网络、文件系统任意读写、执行系统命令等能力。插件默认被限制在自己的目录内。

---

## 2. 快速开始

### 2.1 放置目录

把插件文件夹放到 PCL 数据目录下：

```text
PCL\Plugin\<PluginName>\plugin.plugscript
```

例如：

```text
PCL\Plugin\HelloWindow\plugin.plugscript
```

约定：

- 文件夹名就是插件名，会出现在 `Plugin.Name` 中；
- 入口文件名必须是 `plugin.plugscript`（小写）；
- 一个插件文件夹至少包含这一个入口文件；
- 文件建议使用 UTF-8 编码（无 BOM）；
- 不要直接把 `.plugscript` 文件放在 `PCL\Plugin\` 根目录下。

### 2.2 加载流程

宿主加载插件时按以下顺序处理：

1. 读取 `PCL\Plugin\<PluginName>\plugin.plugscript`；
2. 解析语法；如果有错误，报告错误所在行号；
3. 首次加载时调用 `on_first_launch()`，读取 UI 声明和权限声明；
4. 根据 `Plugin.AccessRequest` 向用户申请权限；
5. 调用 `main()`，执行插件的主要逻辑。

因此：

- **UI 和权限声明放在 `on_first_launch()` 中**；
- **常规逻辑放在 `main()` 中**；
- 不要依赖 `on_first_launch()` 在每次启动时都执行，它只保证在首次加载时调用。

### 2.3 最小示例

```plugscript
// 最小插件：只打开一个窗口并输出一行日志
function on_first_launch() {
   UI.type = "new_window"
   UI.title = "Hello PlugScript"
   UI.width = 720
   UI.height = 460
   UI.FileSize = 20
}

function main() {
   print("Hello from PlugScript!")
}
```

完整文件见 [`Samples/PlugScript/HelloWindow/plugin.plugscript`](Samples/PlugScript/HelloWindow/plugin.plugscript)。

---

## 3. 语法速查

### 3.1 行、注释与分号

- 一行就是一条语句，按行解析；
- `//` 开始单行注释，直到行尾；
- 空行会被忽略；
- 行尾的 `;` 可选，写不写都可以；推荐不写；
- 只支持 `//` 注释，不要使用 `/* ... */`。

```plugscript
// 这是注释
let player = "steve"
const play = True

print(player)   // 行尾分号可选
```

### 3.2 变量与常量

使用 `let` 声明变量，使用 `const` 声明常量。

```plugscript
let player = "steve"
let score = 0
const play = True

score = score + 1
```

规则：

- 变量可以重新赋值；
- `const` 声明的常量不能再赋值，否则会报错；
- 赋值既可以是标识符，也可以是成员：

```plugscript
UI.title = "新的标题"
```

- v0.1 只有 `=` 赋值，不支持 `+=`、`-=`、`++`、`--`。

### 3.3 数据类型

v0.1 使用以下字面量：

| 类型 | 写法 | 示例 |
| --- | --- | --- |
| 字符串 | 双引号 | `"hello"`、`"C:\\Games"` |
| 数字 | 数字字面量 | `0`、`20`、`3.14` |
| 布尔真 | `True` | `const enabled = True` |
| 布尔假 | `False` | `const enabled = False` |
| 空值 | `Nothing` | `let value = Nothing` |
| 数组 | 方括号，逗号分隔 | `[1, 2, 3]`、`["a", "b"]` |

字符串规则：

- 必须使用双引号，不能使用单引号；
- 支持的转义：`\"`、`\\`、`\n`、`\t`；
- 不支持模板字符串（反引号）。

### 3.4 运算符

支持的运算符：

```text
+  -  *  /  %  ==  !=  >  <  >=  <=  &&  ||
```

也支持关键字形式：

```text
and  or  not
```

示例：

```plugscript
let total = 2 + 3 * 4
let same = total == 14
let ok = same and True
let bad = not ok
```

注意：

- 字符串拼接使用 `+`；
- 相等比较使用 `==`，**不要使用 `===` 或 `!==`**；
- 逻辑非使用 `not`，v0.1 不保证支持 `!`；
- 不支持三元表达式 `? :`。

### 3.5 条件

使用 `if ... { ... } else { ... }`：

```plugscript
let score = 10

if score >= 60 {
   print("pass")
} else {
   print("fail")
}
```

条件直接写在 `if` 后面，不要额外加一对括号。需要多分支时使用嵌套 `if`。

### 3.6 循环

v0.1 支持 `while`：

```plugscript
let i = 0

while i < 3 {
   print(i)
   i = i + 1
}
```

没有 `for`、`do ... while` 或 `break` / `continue` 的文档化语法，请用 `while` 和条件变量实现。

### 3.7 函数

自定义函数使用 `function` 声明。v0.1 的自定义函数没有形参和返回值语法：

```plugscript
function main() {
   print("hello")
}
```

也允许省略花括号，用缩进表示函数体：

```plugscript
function main()
   print("hello")
```

花括号形式和缩进形式二选一。使用花括号时，**必须写对应的 `}`**。

调用函数和调用内置函数的写法相同：

```plugscript
function main() {
   print("before")
   print("after")
}
```

> v0.1 不支持 `function greet(name)` 这样的形参，也不要在插件里使用 `return`。需要传递数据时，可以写入变量或 `Storage`，再由 `main` 读取。

### 3.8 成员访问与赋值

使用点号访问对象成员：

```plugscript
UI.type = "new_window"
UI.title = "示例插件"
UI.width = 720
UI.height = 460
UI.FileSize = 20
```

可赋值的成员由宿主提供，详见“UI 声明”和“内置 API”。

### 3.9 数组

数组使用方括号和逗号：

```plugscript
let names = ["alex", "steve"]
let numbers = [1, 2, 3]
```

v0.1 只保证数组字面量和“把数组作为值使用”。不要依赖数组索引、`.length` 或其他未列出的数组成员。

---

## 4. `island` 详解

`island` 用来表达嵌套的配置结构，例如权限清单。它有两种写法：

1. 带花括号：`island { ... }`
2. 缩进块：`island` 后面跟一个缩进块，不需要花括号

`island` 的缩进是**相对的**：子项只要比父项缩进更深即可，不要求固定空格数。可以使用空格或 Tab，但同一层要保持相对一致。

### 4.1 基本规则

| 写法 | 含义 |
| --- | --- |
| `key: value` | 一个属性 |
| `-key: value` | 新建一个列表项对象，并设置它的第一个属性 |
| `key: a, b, c` | 一行内写出列表 |
| `key:` 后跟缩进的裸值 | 列表 |
| `key:` 后跟缩进的 `key: value` 行 | 嵌套对象 |
| `key: island { ... }` | 嵌套的 `island` 值 |

### 4.2 属性、列表与嵌套对象

```plugscript
let config = island {
   name: "demo"
   tags: alpha, beta, gamma

   window:
      width: 720
      height: 460

   limits:
      10
      20

   -item: first
      enabled: True

   -item: second
      enabled: False
}
```

说明：

- `name: "demo"` 是字符串属性；
- `tags: alpha, beta, gamma` 是一行内列表；
- `window:` 下面缩进的 `key: value` 组成嵌套对象；
- `limits:` 下面缩进的裸值组成列表；
- `-item: first` 开始一个列表项对象，后面的 `enabled: True` 属于这个列表项；
- 新的 `-item: second` 开始下一个列表项。

### 4.3 嵌套 `island`

```plugscript
let config = island {
   name: "demo"
   window: island {
      width: 720
      height: 460
   }
}
```

`window:` 的值本身是另一个 `island`，适用于结构比较复杂的配置。

### 4.4 不带花括号的写法

```plugscript
let config = island
   name: "demo"
   tags: alpha, beta
```

缩进块结束时，`island` 自然结束，不需要 `}`。如果使用花括号写法，则每一层都要正确闭合。

### 4.5 权限清单示例

权限清单使用 `-permission:` 开始一个权限项，后面的 `reason`、`whenToAsk`、`range` 都属于这一项：

```plugscript
Plugin.AccessRequest = island {
-permission: camera
   reason: "打开摄像头来拍照"
   whenToAsk: called

-permission: storage
   range:
      "&PCL/xx/xx"
      "&root"
   reason: "to find where the Game is stored"
   whenToAsk: firstLaunch, called
}
```

要点：

- 权限项必须以 `-permission:` 开头，不能写成 `permission:`；
- `reason` 必须是字符串；
- `whenToAsk` 可以使用裸词，多个值用逗号分隔；
- `range` 只用于 `storage`，下面缩进的字符串组成列表；
- 权限项之间用空行分隔只是为了让代码更易读，不是语法要求。

实际使用时，通常把这段赋值放在 `on_first_launch()` 内，而不是放在文件顶层。

---

## 5. 插件生命周期

### 5.1 `on_first_launch()`：首次加载

`on_first_launch()` 在插件首次加载时调用。它负责：

- 声明 UI 类型；
- 设置窗口标题和初始尺寸；
- 设置插件存储配额；
- 声明权限申请。

```plugscript
function on_first_launch() {
   UI.type = "new_window"
   UI.title = "我的插件"
   UI.width = 720
   UI.height = 460
   UI.FileSize = 20
}
```

不要在 `on_first_launch()` 中放置必须每次启动都执行的业务逻辑；这类逻辑应放在 `main()`。

### 5.2 `main()`：入口

`main()` 是插件的主要入口。宿主完成 UI 和权限初始化后调用它。

```plugscript
function main() {
   print("插件已启动")
}
```

建议：

- 在 `main()` 中先用 `Permission.has` 检查权限；
- 文件和存储操作放在 `main()` 中；
- 用 `print` / `log` 输出关键状态，方便排查问题。

---

## 6. UI 声明

在 `on_first_launch()` 中设置以下成员：

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `UI.type` | 字符串 | v0.1 只支持 `"new_window"` |
| `UI.title` | 字符串 | 窗口标题 |
| `UI.width` | 数字 | 窗口宽度 |
| `UI.height` | 数字 | 窗口高度 |
| `UI.FileSize` | 数字 | 插件存储配额，单位 MB |

示例：

```plugscript
function on_first_launch() {
   UI.type = "new_window"
   UI.title = "示例插件"
   UI.width = 720
   UI.height = 460
   UI.FileSize = 20
}
```

注意：

- `UI.type` 必须写成带引号的字符串 `"new_window"`；
- v0.1 不支持 `new_innerTab` 或其他 UI 类型；
- `UI.FileSize` 是配额，不是当前已用空间；
- 请设置合理的窗口尺寸和配额，不要无必要地申请很大空间。

---

## 7. 权限与安全

### 7.1 默认隔离

插件默认只能在自己的插件目录内读写：

```text
PCL\Plugin\<PluginName>\
```

访问该目录之外的资源需要声明权限。插件不能通过 `..`、绝对路径等方式绕过隔离。

### 7.2 声明权限

在 `on_first_launch()` 中给 `Plugin.AccessRequest` 赋值：

```plugscript
Plugin.AccessRequest = island {
-permission: storage
   range:
      "&PCL/versions"
   reason: "读取游戏版本目录"
   whenToAsk: firstLaunch
}
```

字段说明：

| 字段 | 是否必需 | 说明 |
| --- | --- | --- |
| `permission` | 是 | 权限名，见下表 |
| `reason` | 是 | 向用户说明为什么需要该权限，必须是字符串 |
| `whenToAsk` | 否 | 询问时机，可使用 `firstLaunch`、`called`，多个用逗号分隔 |
| `range` | 仅 `storage` | 允许访问的路径范围，字符串列表 |

如果插件只使用自己的插件目录和 `Storage`，不需要访问外部资源，可以完全不设置 `Plugin.AccessRequest`。

### 7.3 可申请的权限

| 权限名 | 用途 | v0.1 状态 |
| --- | --- | --- |
| `camera` | 摄像头 | 仅声明，不会实际授予 |
| `storage` | 访问插件目录之外的存储路径 | 可声明；是否授予取决于宿主与用户 |
| `executeSystemShell` | 执行系统命令 | 仅声明，不会实际授予 |

重要限制：

- v0.1 没有提供摄像头相关的内置函数，`camera` 只是为后续版本预留；
- v0.1 没有提供执行系统命令的内置函数，`executeSystemShell` 也只是声明，插件无法真正执行命令；
- 因此在这些能力开放前，`Permission.has("camera")` 和 `Permission.has("executeSystemShell")` 都会返回 `False`；
- 不要声称插件已经能够拍照或执行 shell 命令。

### 7.4 路径前缀

在字符串中可以使用以下路径前缀：

| 前缀 | 含义 |
| --- | --- |
| `&PCL` | PCL 根目录 |
| `&root` | 文件系统根目录（全盘访问） |

示例：

```plugscript
"&PCL/versions"
"&PCL/Plugin/MyPlugin"
"&root"
```

`&root` 范围非常大。除非确实需要，否则不要申请 `&root`，应尽量把 `range` 缩小到具体目录。

### 7.5 存储配额

`UI.FileSize` 设置插件存储配额，单位为 MB。宿主会按照该配额限制插件存储；超出配额后写入会失败。

建议：

- 只存小型文本数据；
- 不要让插件存储无限增长；
- 不要把大文件写入插件目录。

### 7.6 查询权限

使用 `Permission.has(name)` 查询：

```plugscript
if Permission.has("storage") {
   print("storage granted")
} else {
   print("storage not granted")
}
```

只有权限确实已授予时才返回 `True`。未声明、未授予或当前版本不支持的能力都返回 `False`。

---

## 8. 内置 API

v0.1 只提供以下内置函数和对象。不要假设存在其他 API。

### 8.1 日志

| 调用 | 说明 |
| --- | --- |
| `print(value)` | 把值写入插件日志 |
| `log(value)` | 把值写入插件日志 |

```plugscript
print("hello")
log("debug: plugin started")
print(42)
```

`print` 和 `log` 都接受一个值。需要输出多个值时，用 `+` 拼接字符串。

### 8.2 `Storage`

`Storage` 用于访问插件自己的存储目录。`name` 是存储目录内的相对文件名。

| 调用 | 说明 |
| --- | --- |
| `Storage.read(name)` | 读取文本文件，返回字符串 |
| `Storage.write(name, text)` | 写入文本文件 |
| `Storage.exists(name)` | 文件存在时返回 `True`，否则返回 `False` |
| `Storage.delete(name)` | 删除文件 |
| `Storage.list()` | 返回存储目录中的文件名数组 |

示例：

```plugscript
function main() {
   if Storage.exists("note.txt") {
      let text = Storage.read("note.txt")
      print(text)
   } else {
      Storage.write("note.txt", "first run")
   }
}
```

注意：

- 不要使用 `..` 或绝对路径；
- 文件不存在时先调用 `Storage.exists` 判断，再读取或删除；
- `Storage.list()` 返回的是数组，v0.1 不要依赖数组索引或 `.length`。

### 8.3 `Permission`

| 调用 | 说明 |
| --- | --- |
| `Permission.has(name)` | 权限已授予时返回 `True`，否则返回 `False` |

```plugscript
if Permission.has("storage") {
   print("can use storage")
}
```

### 8.4 `Plugin`

| 成员 | 说明 |
| --- | --- |
| `Plugin.Name` | 当前插件的名称（由宿主提供） |
| `Plugin.Path` | 当前插件的目录路径（由宿主提供） |
| `Plugin.AccessRequest` | 权限申请清单，在 `on_first_launch()` 中赋值 |

```plugscript
function main() {
   print("plugin: " + Plugin.Name)
   print("path: " + Plugin.Path)
}
```

### 8.5 `UI`

`UI` 的成员只在 `on_first_launch()` 中声明，详见“UI 声明”：

- `UI.type`
- `UI.FileSize`
- `UI.title`
- `UI.width`
- `UI.height`

---

## 9. 完整示例

以下三个示例都可以直接复制到对应的插件目录中运行。

### 9.1 HelloWindow：最小窗口插件

路径：[`Samples/PlugScript/HelloWindow/plugin.plugscript`](Samples/PlugScript/HelloWindow/plugin.plugscript)

```plugscript
// HelloWindow：最小可运行插件示例
// 位置：PCL\Plugin\HelloWindow\plugin.plugscript

function on_first_launch() {
   UI.type = "new_window"
   UI.title = "Hello PlugScript"
   UI.width = 720
   UI.height = 460
   UI.FileSize = 20
}

function main() {
   print("Hello from HelloWindow!")
}
```

### 9.2 PermissionDemo：权限声明

路径：[`Samples/PlugScript/PermissionDemo/plugin.plugscript`](Samples/PlugScript/PermissionDemo/plugin.plugscript)

```plugscript
// PermissionDemo：权限声明示例
// 位置：PCL\Plugin\PermissionDemo\plugin.plugscript

function on_first_launch() {
   UI.type = "new_window"
   UI.title = "权限申请示例"
   UI.width = 720
   UI.height = 460
   UI.FileSize = 10

   Plugin.AccessRequest = island {
   -permission: camera
      reason: "拍照用于生成玩家头像"
      whenToAsk: called

   -permission: storage
      range:
         "&PCL/Plugin/PermissionDemo"
         "&PCL/versions"
      reason: "读取游戏版本目录，以便定位游戏文件"
      whenToAsk: firstLaunch, called

   -permission: executeSystemShell
      reason: "预留启动游戏进程的能力"
      whenToAsk: firstLaunch, called
   }
}

function main() {
   print("插件名称：" + Plugin.Name)

   if Permission.has("camera") {
      print("camera 权限已授予")
   } else {
      print("camera 权限未授予（v0.1 中该权限仅声明，不会实际授予）")
   }
}
```

这个示例演示了：

- `island { ... }` 权限清单；
- `camera`、`storage`、`executeSystemShell` 三种权限声明；
- `range:` 下缩进的路径列表；
- `whenToAsk: firstLaunch, called` 逗号列表；
- `main()` 中使用 `Permission.has`。

### 9.3 StorageDemo：插件存储

路径：[`Samples/PlugScript/StorageDemo/plugin.plugscript`](Samples/PlugScript/StorageDemo/plugin.plugscript)

```plugscript
// StorageDemo：插件私有存储示例
// 位置：PCL\Plugin\StorageDemo\plugin.plugscript

function on_first_launch() {
   UI.type = "new_window"
   UI.title = "存储示例"
   UI.width = 640
   UI.height = 420
   UI.FileSize = 5
}

function main() {
   const counterFile = "counter.txt"

   if Storage.exists(counterFile) {
      let last = Storage.read(counterFile)
      print("上次记录：" + last)
      Storage.write(counterFile, "再次启动")
   } else {
      Storage.write(counterFile, "首次启动")
      print("已创建 " + counterFile)
   }

   let files = Storage.list()
   print("插件存储目录中的文件：")
   print(files)
}
```

这个示例演示了 `Storage.exists`、`Storage.read`、`Storage.write` 和 `Storage.list`。所有文件都位于 `PCL\Plugin\StorageDemo\` 内。

---

## 10. 常见错误

解释器会在错误信息中给出类似 `第 12 行：...` 的行号。先看行号，再检查该行和上一行的括号、缩进与关键字。

### 10.1 忘记闭合 `}`

错误示例：

```text
function main() {
   print("hello")
```

修复：为每个 `{` 补上对应的 `}`。

```plugscript
function main() {
   print("hello")
}
```

`island { ... }` 和嵌套 `island` 同样需要闭合。

### 10.2 使用 JavaScript 的 `var`

错误写法：

```text
var player = "steve"
```

正确写法：

```plugscript
let player = "steve"
```

### 10.3 使用 `===` 或 `!==`

错误写法：

```text
if score === 10 {
```

正确写法：

```plugscript
if score == 10 {
   print("exactly ten")
}
```

### 10.4 使用箭头函数、`async` / `await`

v0.1 不支持：

```text
const run = () => { }
async function main() { }
await something()
```

请删除这些语法，改用普通的 `function` 和同步语句。

### 10.5 使用单引号或模板字符串

错误写法：

```text
let name = 'steve'
let text = `hello ${name}`
```

正确写法：

```plugscript
let name = "steve"
let text = "hello " + name
```

### 10.6 在权限项中使用 `permission:` 而不是 `-permission:`

错误写法：

```text
Plugin.AccessRequest = island {
   permission: camera
      reason: "拍照"
}
```

正确写法：

```plugscript
Plugin.AccessRequest = island {
-permission: camera
   reason: "拍照"
}
```

`-permission:` 的短横线表示“开始一个新的列表项对象”，不能省略。

### 10.7 把多个路径写成一个字符串

错误写法：

```text
range: "&PCL/versions, &PCL/Plugin/MyPlugin"
```

正确写法：

```plugscript
range:
   "&PCL/versions"
   "&PCL/Plugin/MyPlugin"
```

或者使用逗号列表：

```plugscript
range: "&PCL/versions", "&PCL/Plugin/MyPlugin"
```

### 10.8 `UI.type` 忘记加引号

错误写法：

```text
UI.type = new_window
```

正确写法：

```plugscript
UI.type = "new_window"
```

### 10.9 修改 `const`

错误写法：

```text
const play = True
play = False
```

正确写法：需要变化时使用 `let`。

```plugscript
let play = True
play = False
```

### 10.10 在函数中使用形参

错误写法：

```text
function greet(name) {
   print(name)
}
```

v0.1 不支持自定义函数形参。请把值放在变量或 `Storage` 中，在函数体内直接读取。

### 10.11 使用中文标点

错误写法：

```text
print（"hello"）
UI.title ＝ "示例"
```

正确写法：使用英文括号、引号和等号。

```plugscript
print("hello")
UI.title = "示例"
```

### 10.12 使用未列出的 API

不要使用 `Fetch`、`File`、`Shell`、`Camera`、`Http` 等未在本指南出现的对象或函数。v0.1 只提供第 8 节列出的 API。

---

## 11. 给 AI 的生成提示词

把下面整段复制给 AI，并在末尾补充你的需求。提示词要求 AI 只输出插件源码。

```text
你是一个 PlugScript v0.1 插件生成器。请根据我最后给出的需求，生成一个完整、可直接保存为
PCL\Plugin\<PluginName>\plugin.plugscript 的插件源码。

硬性规则：
1. 只使用 PlugScript v0.1 语法，不要使用 JavaScript 语法。
2. 允许：// 注释、let、const、= 赋值、字符串（双引号）、数字、True、False、Nothing、
   数组 [a, b]、+ - * / % == != > < >= <= && ||、and/or/not、if/else、while、
   function name() { ... }、成员赋值、island { ... }。
3. 禁止：var、===、!==、=>、async、await、function 形参、return、for、switch、
   try/catch、class、import、new、this、单引号、模板字符串、++、--、+=、-=、
   对象字面量 {}（island 除外）。
4. 每个 { 都必须有对应的 }。每个函数、if、while、island 都要正确闭合，不能漏 }。
5. 只使用以下内置 API：
   print(value)、log(value)
   Storage.read(name)、Storage.write(name, text)、Storage.exists(name)、
   Storage.delete(name)、Storage.list()
   Permission.has(name)
   Plugin.Name、Plugin.Path、Plugin.AccessRequest
   UI.type、UI.FileSize、UI.title、UI.width、UI.height
6. UI.type 只能是 "new_window"。
7. 权限只能声明 camera、storage、executeSystemShell；权限项必须写成 -permission: 名称。
   camera 和 executeSystemShell 在 v0.1 中只是声明，不会授予，也不要生成调用摄像头
   或执行系统命令的代码。
8. 除非需求明确要求并声明 storage 权限，否则所有文件操作都必须在插件自己的目录内，
   使用 Storage，不要使用绝对路径或 ..。
9. 在 on_first_launch() 中设置 UI 和 Plugin.AccessRequest；在 main() 中写主要逻辑。
10. 输出前逐行检查：括号是否闭合、字符串是否使用双引号、是否使用了未列出的 API。

输出要求：
只输出插件源码，不要输出解释、Markdown 代码围栏或额外文字。

我的插件需求：
<在这里写你的需求>
```

---

## 12. 版本与限制

本指南对应 **PlugScript v0.1**。

v0.1 的定位是“先让插件跑起来”，表达式能力会在后续版本扩展。当前明确限制：

- 只支持 `new_window` 一种 UI 类型；
- 自定义函数不支持形参和 `return`；
- 只支持 `while`，不支持 `for`；
- 数组只保证字面量和值传递，不提供索引、`.length` 等成员；
- `camera` 和 `executeSystemShell` 只做权限声明，不会实际授予；
- 没有网络、任意文件读写、系统命令执行等 API；
- 解释器按行解析，错误会带行号。

请不要使用以下 JavaScript 特性：

```text
var
===
!==
=>
async / await
function 形参
return
for
switch
try / catch
class
import
new
this
单引号字符串
模板字符串
++
--
+=
-=
对象字面量 {}
```

如果本指南没有写到某个语法或 API，就视为 v0.1 不支持。遇到不确定的写法时，优先使用本指南中的示例结构，并保持每个 `{` 都有对应的 `}`。

---

## 附录：文件清单

- [`PlugScriptGuide.md`](PlugScriptGuide.md)：本指南；
- [`Samples/PlugScript/HelloWindow/plugin.plugscript`](Samples/PlugScript/HelloWindow/plugin.plugscript)：最小窗口示例；
- [`Samples/PlugScript/PermissionDemo/plugin.plugscript`](Samples/PlugScript/PermissionDemo/plugin.plugscript)：权限声明示例；
- [`Samples/PlugScript/StorageDemo/plugin.plugscript`](Samples/PlugScript/StorageDemo/plugin.plugscript)：插件存储示例。
