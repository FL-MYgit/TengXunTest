# TengXunTest

一个基于 Unity 2D、LLM for Unity 和 NavMeshPlus 的本地 AI NPC 交互原型。

玩家可以通过屏幕底部的输入框向 NPC 发送自然语言指令。游戏会把当前场景中的物品名称、描述和坐标一并发送给本地大语言模型；模型返回结构化 JSON 后，NPC 可以进行对话或使用 NavMesh 移动到指定位置。

## 当前功能

- 在 Unity 中本地运行 GGUF 大语言模型，无需远程 API。
- 输入自然语言并使用 Enter 发送给 NPC。
- 将苹果树、房屋、椅子、池塘和动物等场景物品信息发送给 AI。
- 使用约束后的 JSON 协议解析 AI 回复。
- 支持 `move_to`、`say` 和 `idle` 三种 AI 行为。
- 使用 NavMeshPlus 在 2D 场景中寻路。
- 自动把地图外或障碍物内的目标投射到最近的可行走位置。
- NavMesh 不可用时停止移动，避免 NPC 穿墙或离开地图。
- 摄像机跟随 NPC，并保持正确的 2D 摄像机深度。
- 非流式显示 AI 的完整回复。
- AI 请求期间显示“AI 思考中...”，并暂时禁用输入框。
- 带有动态中文字库、对话面板和退出游戏按钮。
- AI 回复解析失败时执行有限次数重试，不会无限重传。

## 环境要求

| 项目 | 版本或说明 |
| --- | --- |
| Unity | `2022.3.62f3c1` |
| 渲染类型 | Unity 2D |
| LLM for Unity | `3.0.3`，已嵌入 `Packages/ai.undream.llm` |
| NavMeshPlus | 通过 Git URL 安装 |
| AI Navigation | `1.1.7` |
| TextMesh Pro | `3.0.7` |
| 推荐平台 | Windows x64 |

首次解析 NavMeshPlus 依赖时需要能够访问 GitHub，并确保系统已安装 Git。

## 快速开始

1. 使用 Unity Hub 添加项目目录，并通过 Unity `2022.3.62f3c1` 打开。
2. 等待 Unity 完成 Package Manager 依赖导入和脚本编译。
3. 准备兼容 `llama.cpp` 的 GGUF 模型，例如：

   ```text
   Qwen3.5-2B-Q4_K_M.gguf
   ```

4. 打开 `Assets/Scenes/SampleScene.unity`。
5. 选择场景中的 `LLM` 对象，在 Inspector 中把 **Model** 设置为本机实际存在的 GGUF 文件。
6. 确认 NPC 上的 `LLMAgent` 引用了场景中的 `LLM` 组件。
7. 进入 PlayMode，在底部输入框中输入内容并按 Enter。

> GGUF 模型通常体积较大，已通过 `.gitignore` 排除，不会随仓库下载。每台电脑都需要单独准备模型并重新选择本地路径。

## 使用方式

可以输入类似内容：

```text
去苹果树旁边看看
```

发送给模型的实际内容由两部分组成：

```text
当前场景物品信息：
{"items":[{"name":"苹果树","description":"一颗高大的苹果树，上面结了许多苹果","x":11.076,"y":0.56}]}

玩家输入：
去苹果树旁边看看
```

模型思考期间，界面会显示“AI 思考中...”。收到并成功解析回复后，NPC 会显示回复文本，并在需要时通过 NavMesh 前往目标位置。

## AI 回复协议

模型必须只返回一个 JSON 对象，不要附加 Markdown 代码块或其他解释。

### 移动并说话

```json
{
  "action": "move_to",
  "x": 11.0,
  "y": 0.5,
  "message": "我去苹果树旁边看看。"
}
```

### 仅说话

```json
{
  "action": "say",
  "x": 0,
  "y": 0,
  "message": "你好！"
}
```

### 保持空闲

```json
{
  "action": "idle",
  "x": 0,
  "y": 0,
  "message": ""
}
```

若回复为空、不是合法 JSON 或包含未知 `action`，`NPCAgent` 会在配置的重试上限内重新请求。

## 项目结构

```text
Assets/
├─ ArtRes/Scene/                    # 场景美术资源
├─ Resources/
│  ├─ Prefabs/NPC.prefab           # NPC 预制体
│  └─ ScriptableObjects/            # 场景物品资料
├─ Scenes/
│  ├─ SampleScene.unity             # 当前主场景
│  └─ SampleScene/NavMesh-NavMesh.asset
├─ Scripts/
│  ├─ Camera/CameraController.cs
│  ├─ LLM/Message/
│  │  ├─ GameWorldMes.cs
│  │  └─ ReturnMes.cs
│  ├─ NPC/
│  │  ├─ NPCAgent.cs
│  │  └─ NPCController.cs
│  ├─ UI/
│  │  ├─ Field.cs
│  │  └─ GameUIController.cs
│  └─ World/WorldItem.cs
└─ TextMesh Pro/Fonts/              # 中文动态 SDF 字体

Packages/
└─ ai.undream.llm/                  # 内嵌的 LLM for Unity 包
```

## 主要类说明

| 类 | 作用 |
| --- | --- |
| `NPCAgent` | 组合场景信息和玩家输入，调用本地模型，控制重试并解析 JSON 回复。 |
| `NPCController` | 执行 NPC 的移动与说话；负责 NavMesh 初始化、目标采样和越界保护。 |
| `GameWorldMes` | 管理场景中的 `WorldItem` 列表，并生成可发送给 AI 的 JSON 快照。 |
| `WorldItem` | ScriptableObject 类型的场景物品数据，保存名称、描述和位置。 |
| `ReturnMes` | AI JSON 回复对应的数据结构。 |
| `Field` | 监听 TMP 输入框结束编辑事件，并把文本交给 `NPCAgent`。 |
| `GameUIController` | 在运行时创建和美化对话面板、思考状态与退出按钮。 |
| `CameraController` | 在 X/Y 平面跟随 NPC，同时保留摄像机 Z 深度。 |

## 添加新的场景物品

1. 在 Project 窗口中创建新的 `WorldItem` 资源。
2. 填写 `Name`、`Description` 和 `Pos`。
3. 在 `SampleScene` 中找到挂载 `GameWorldMes` 的对象。
4. 将新资源添加到 `World Obj Pos` 列表。
5. 确保填写的坐标与物体在场景中的实际位置一致。

`GameWorldMes` 会在每次请求前读取列表并生成普通 JSON 数据，因此后续修改资源内容会自动反映到下一次 AI 请求中。

## NavMesh 配置

本项目使用 NavMeshPlus 的 2D 导航组件，不能与 Unity AI Navigation 中同名的 Surface/Modifier 混用。

- 场景根节点使用 NavMeshPlus `NavMeshSurface`。
- 同一对象上挂载 `CollectSources2d`。
- Surface 旋转为 X 轴 `-90°`，用于 XY 平面的 2D 寻路。
- 地面和边界使用 NavMeshPlus `NavMeshModifier` 与 `Collider2D`。
- NPC 的 `NavMeshAgent` 初始保持禁用，由 `NPCController` 在 NavMesh 可用后启用。

修改地图碰撞体后，应重新烘焙 NavMesh。运行时若现有数据不可用，`NPCController` 也会尝试重建一次。

## 常见问题

### Model file not found

场景保存的模型路径只对原开发机器有效。请选中 `LLM` 对象，重新选择本机的 `.gguf` 模型文件。

### LlamaLib error -1: Error loading the model

- 确认模型是完整、有效并兼容当前运行库的 GGUF 文件。
- 先将 `Num GPU Layers` 设置为 `0`，确认 CPU 模式能够启动。
- 降低 Context Size 或使用更小的量化模型，以减少内存占用。
- 关闭多余的 Unity Editor 和其他高内存程序。

### Failed to create agent / Sources 0

- 确认使用的是 NavMeshPlus 版本的 `NavMeshSurface` 和 `NavMeshModifier`。
- 确认地面及边界对象存在有效 `Collider2D`。
- 退出 PlayMode，重新烘焙 NavMesh 后再启动。

### 中文显示为方框

项目中的 `SIMHEI SDF` 使用动态多图集。若仍有缺字，请确认相关 TMP 文本使用该字体，并检查目标字符是否存在于源字体 `SIMHEI.TTF` 中。

### 内存不足或 Unity 闪退

本地模型会占用较多内存。当前场景默认使用较小的上下文长度，但实际占用仍取决于模型和运行库。建议：

- 使用 2B 或更小的 Q4 量化模型。
- 降低 Context Size 和 Batch Size。
- 不要同时打开多个 Unity Editor 实例。
- 在退出 PlayMode 前取消仍在执行的模型请求。

## 构建说明

`SampleScene` 已加入 Build Settings。构建前请确认：

- LLM 组件使用的模型已正确配置。
- LLM for Unity 所需的原生运行库已包含在目标平台构建中。
- 目标机器具有足够内存。
- 在目标平台上测试 `Application.Quit()` 和模型加载流程。

## 当前状态

本项目目前是一个单 NPC、本地模型驱动的交互原型，重点验证以下链路：

```text
玩家输入
  → 场景物品 JSON
  → 本地 LLM
  → 结构化动作 JSON
  → NPC 对话 / NavMesh 移动
```

后续可以在此基础上扩展多 NPC、行为树、任务系统、记忆系统、动态世界状态和更完整的游戏规则。
