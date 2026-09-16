# 星语庄园（三消游戏）— 完整说明文档

## 一、游戏概述

**《星语庄园》** 是一款竖屏三消（Match-3）游戏。玩家在限定步数内交换相邻棋子制造三连消除，完成收集/清除目标，通关获得金币与星星。实现依据《三消游戏需求文档.md》《星语庄园-游戏策划案.md》《星语庄园-美术及UI需求文档.md》。

- 棋盘规格：7 列 × 9 行（竖屏），基础棋子 6 色（红花/蓝滴/绿叶/黄星/紫晶/橙果）
- 特殊棋子：4 种（横向/纵向火箭、炸弹、彩球、螺旋桨），组合技 6 类
- 关卡内容：10 个渐进关卡（`levels.json` 配置驱动），覆盖收集颜色、破坏木箱、清除冰层三类目标
- 技术栈：Unity 2022.3.39f1 + uGUI，棋盘逻辑为纯 C# 数据层（无 MonoBehaviour 依赖）
- 运行方式：打开 `Assets/Scenes/MainMenu.unity` → Play；Build Settings 已配置 MainMenu / Gameplay 两场景，默认竖屏（1170×2532 设计分辨率）

---

## 二、游戏功能说明

### 1. 主界面（庄园）

程序启动后进入主界面，包含：

| 元素 | 位置 | 说明 |
|------|------|------|
| 庄园背景 | 全屏 | 横版素材等比 Cover 铺满裁切 |
| 顶栏 | 顶部 | 生命（❤ 3/5 + 30 分钟恢复倒计时）、金币、星星，图标与数字间距固定不重叠 |
| 标题 "星语庄园" | 顶部居中 | 深色描边大字 |
| 关卡地图面板 | 居中 | 带顶部缎带横幅的九宫格面板，标题"选择关卡"置于缎带内 |
| 关卡节点网格 | 面板内 | 3 列 × 4 行；节点三态（完成/当前/锁定）+ 困难节点专用配色图；当前关数字深紫、其余白字深描边 |
| 底部主按钮区 | 底部锚定 | "▶ 继续关卡"（进入当前解锁关卡）、"商城"、"活动（敬请期待）" |
| 设置按钮 | 顶栏右缘 | 齿轮图标 + "设置"文字提示，打开设置面板（音效开关等） |

### 2. 关卡内界面

| 元素 | 位置 | 说明 |
|------|------|------|
| 关卡目标栏 | 顶部左侧 | 每个目标一个槽位：棋子图标 + "收集红花/消除木箱/清除冰层"文字说明 + "0/8" 进度数字 |
| 步数 | 顶部中间 | 数字 + "步数"说明，≤5 步时红色脉冲警告 |
| 暂停按钮 | 顶栏右缘 | 齿轮图标 + "暂停"文字提示 |
| 棋盘 | 居中 | 7×9 逐格平铺的浅/深格底，棋子视图与模型数据逐格对齐 |
| 暂停面板 | 模态弹窗 | 继续 / 重新开始 / 退出关卡（主动退出不扣生命） |
| 胜利结算 | 模态弹窗 | 星星+金币入账、"下一关"按钮 |
| 失败结算 | 模态弹窗 | 剩余目标进度、**900 金币 +5 步续关**（单关上限 2 次）、放弃 |
| 生命不足面板 | 模态弹窗 | 生命耗尽时拦截进关，显示恢复倒计时 |

### 3. 三消核心机制

**基本规则**
- 拖拽/点选交换相邻棋子；非法交换自动回弹，**不消耗步数**
- 每次有效交换步数 -1；连锁消除不额外计步
- 棋盘上无可用移动时自动洗牌（不耗步数），单关洗牌上限 5 次

**消除流程（状态机）**
```
Idle ──交换/点击特殊棋子──→ Resolving
         │
         ├─ 组合技演出（飘字+闪光+彩虹波，停留 0.45s）
         ├─ 级联循环：消除波浪爆发 → 障碍扣血 → 特殊棋子生成 → 重力下落 → 视图校准 → 检测新三连
         ├─ 无新三连 → 洗牌检查 → 胜负结算
         └─ 结束 → Idle
```

### 4. 特殊棋子（4 种，Royal Match 规则）

| 特殊棋子 | 生成条件 | 图标 | 单击 | 效果 |
|---|---|---|---|---|
| 横向火箭 | 横排 4 连 | **横向**（素材旋转 -90°） | 直接引爆，不耗步数 | 清除整行 |
| 纵向火箭 | 竖排 4 连 | 竖向（素材原样） | 直接引爆，不耗步数 | 清除整列 |
| 炸弹 | T/L 交叉 ≥5 格 | — | 直接引爆，不耗步数 | 以落点为中心 3×3 |
| 螺旋桨 | 2×2 方块 | — | 直接起飞，不耗步数 | 飞向未完成目标 > 障碍 > 随机，十字 5 格 |
| 彩球 | 直线 5 连 | — | **仅选中**，需与相邻棋子交换生效 | 见组合表 |

- 特殊棋子被普通消除波及时直接引爆；特殊 + 普通交换（不成三连）时在落点直接引爆
- T/L 交叉去重：同一次消除按 彩球 > 炸弹 > 火箭 的优先级只生成一个特殊棋子

### 5. 组合技（6 类，以交换落点为中心）

| 组合 | 效果 |
|---|---|
| 火箭 + 火箭 | 十字清除（1 行 + 1 列） |
| 火箭 + 炸弹 | **横向火箭 → 清除 3 行；纵向火箭 → 清除 3 列**（由火箭朝向决定） |
| 炸弹 + 炸弹 | 5×5 爆炸 |
| 彩球 + 普通棋子 | 清除棋盘上该颜色**全部**棋子 |
| 彩球 + 特殊棋子 | 该颜色全部棋子**转化为同样的特殊棋子**后连锁引爆 |
| 彩球 + 彩球 | 全棋盘清除 |

- 组合触发时播放专属演出：全屏闪光 + 组合名称飘字 + 彩虹波扩散，**停留 0.45s** 后才开始消除
- 彩球自身只被消耗、不参与连锁展开（避免额外多清一种颜色）

### 6. 演出与动画节奏（可读性优先）

| 演出环节 | 时长 | 说明 |
|---|---|---|
| 单击激活蓄力 | 0.45s | 特殊棋子脉冲放大 1.5 倍回弹，明确激活来源 |
| 激活飘字+爆光停留 | 0.35s | "火箭发射！/炸弹引爆！/螺旋桨起飞！" |
| 组合技演出停留 | 0.45s | 飘字 1.4s（前 70% 不透明）、闪光 0.55s、彩虹波 0.8s |
| 消除爆发 | 0.55s/个 | 平方曲线淡出；大面积消除**按与波源距离延迟 0~0.3s 波浪式依次爆开** |
| 消除后停顿 | 0.3s | 看清哪些棋子消失后再重力下落 |
| 特殊棋子生成 | 0.45s | PopScale 弹出 |

### 7. 关卡目标与障碍物

| 目标/障碍 | 判定 | 说明 |
|---|---|---|
| 收集颜色 | 累计消除指定色棋子数 | 如"收集 10 个紫晶" |
| 木箱（crate） | **相邻**消除扣 1 层，层归零销毁 | 阻挡下落（分段重力），HP 标签即时刷新 |
| 冰层（ice） | **格内**消除扣 1 层，层归零销毁 | 按格计数（hp 只影响破坏次数）；棋子正常下落 |

- 目标栏进度实时更新；目标数必须等于棋盘上的障碍格数（已全局校验 6–10 关）

### 8. 经济与存档（PlayerPrefs）

| 资源 | 规则 |
|---|---|
| 生命 | 上限 5，30 分钟/点自动恢复，顶栏显示倒计时；失败扣 1，主动退出不扣 |
| 金币 | 通关奖励（普通 120 / 困难 240）；续关消耗 900 金币 +5 步（单关 2 次上限） |
| 星星 | 首次通关 +1（预留庄园建设消耗口） |
| 解锁进度 | 通关解锁下一关，锁定节点不可点击 |

### 9. 音效

无音频资源，`SoundManager` 运行时**程序生成音效**（正弦/噪声合成）：消除 pop、组合爆炸、按钮点击、胜负音乐。

---

## 三、脚本结构和内容

全部脚本位于 `Assets/Scripts/`，命名空间 `StarManor`。

### Core/（核心层）

#### GameConfig.cs（全局配置）
```
GameConfig（静态常量，对应策划案 §4.3 level_config 字段）
├── BoardCols/BoardRows      — 7×9 棋盘
├── MaxLives/LifeRegenMinutes— 生命 5 上限 / 30 分钟恢复
├── ContinueCostGold 等      — 续关 900 金币 +5 步 ×2 次
├── RewardGoldNormal/Hard    — 通关金币
├── ShuffleLimit             — 洗牌上限 5
├── DesignWidth/Height       — 1170×2532 竖屏设计分辨率
└── CellSize/CellGap         — 格子像素尺寸与间距

PieceColor 枚举             — 6 色棋子（Rose/Drop/Leaf/Star/Gem/Orange）
SpecialKind 枚举            — RocketRow/RocketCol/Bomb/Rainbow/Propeller
PieceColorExt 扩展          — 名称↔枚举↔中文名三向转换
```

#### LevelData.cs（关卡数据）
```
GoalDef   — { type: "collect"|"clear", color, target: "crate"|"ice", count }
CellDef   — { x, y, hp } 障碍实例
LevelDef  — { id, steps, difficulty, rewardGold, goals[], crates[], ices[] }
LevelDatabase — 静态类，从 Resources/Levels/levels.json（JsonUtility）加载与查询
```

#### BoardModel.cs（棋盘纯数据层，核心算法）
```
Piece        — { color, special }；IsSpecial / IsRainbow
Obstacle     — { kind: "crate"|"ice", hp }
ClearPlan    — { cells: HashSet<Vector2Int>, spawns: List<SpecialSpawn>, comboName }
ClearResult  — { collected, cratesDestroyed, icesDestroyed, totalPieces }
Run          — 一次连续同色段 { x, y, len, horiz, color }

BoardModel
├── Init(LevelDef)          — 建棋盘、铺障碍
├── Get/InBounds            — 格子访问
├── Swap(a,b)               — 交换两格
├── TrySwap(a,b)            — 交换 → FindRuns → 无三连回弹返回 null
├── FindRuns()              — 全盘横/纵三连扫描（≥3）
├── PlanFromRuns(...)       — 消除方案生成 + 特殊棋子生成判定：
│     ① 直线 5+ → 彩球（swapPos 优先）
│     ② T/L 交叉同色 ≥5 格 → 炸弹（consumed 集合去重）
│     ③ 4 连 → 火箭（横 4 → RocketRow，纵 4 → RocketCol）
│     ④ 2×2 独立方块 → 螺旋桨（优先指向目标色/障碍）
├── AddPropellerTargetToPlan — 螺旋桨目标格并入方案（未完成目标 > 障碍 > 随机）
├── ExpandChain(plan)       — 波及的特殊棋子连锁展开（队列式）
├── BuildComboPlan(a,b)     — 6 类组合技方案（见功能 §5）
├── DetonateAt(x,y)         — 单击激活：按棋子类型生成单点引爆方案
├── ApplyPlan(plan)         — 扣除棋子 → 障碍扣血（木箱相邻/冰层格内）→ 结算 ClearResult
├── ApplyGravity()          — 分段重力（木箱阻挡分段下落），返回 GravityMove 列表
├── HasValidMove()          — 全盘可移动检测（含特殊+普通交换恒合法）
├── Shuffle()               — Fisher-Yates 重排不产生直接三连，上限 5 次
└── AddPropellerTargetToPlan 等
```

#### GameFlow.cs（流程单例）
场景切换（主界面 ↔ 关卡内）、当前关卡号传递、关卡加载入口 `StartLevel(id)`。

#### SaveSystem.cs（存档）
PlayerPrefs 持久化：生命值与恢复时间戳、金币、星星、最大解锁关卡。每次读取时按流逝时间结算生命恢复。

#### SoundManager.cs（音效单例）
程序生成音效（`AudioClip.Create` 合成波形）：pop / combo / click / win / lose，`Play(name)` 播放。

#### Bootstrap.cs（场景自举）
两个场景共用：`Awake()` 中检测场景名，程序化创建 EventSystem + Canvas，挂载 GameFlow / SoundManager / EffectsRunner 单例，再按场景挂 MainMenuUI 或 BoardController。**场景内零序列化引用**，GUID 固定（`4f2a1b8c...`，被两个场景 YAML 引用，勿删 .meta）。

### UI/（表现层）

#### BoardController.cs（关卡内控制器，最核心）
```
BoardController
├── 状态机              — Idle / Resolving（防动画期间非法输入）
├── StartLevel(def)     — 初始化模型 → 全量建视图 → 建 HUD
├── PieceInput 回调
│   ├── OnDrag(...)     — 拖拽超过阈值 → AttemptSwap（isClick=false 不触发单击）
│   └── OnPieceUp(...)  — 点击特殊棋子（火箭/炸弹/螺旋桨）→ ActivateSpecialAt；点击普通棋子 → 选中
├── AttemptSwap(a,b)    — 关键：模型交换前用 Piece 键预取视图（防错位），传入 SwapAndResolve
├── SwapAndResolve      — 两棋子并行滑动 → 步数-1 → ExecutePlan；非法交换回弹
├── ActivateRoutine     — 单击激活演出：PulseView 蓄力(0.45s) → 飘字+爆光(0.35s) → ExecutePlan
├── ExecutePlan(plan)   — 组合技演出(0.45s) → CascadeLoop → 洗牌检查 → 胜负结算（交换/单击共用）
├── CascadeLoop         — 级联循环（螺旋桨目标并入 → ResolvePlan → 检测新三连，guard<100）
├── ResolvePlan(plan)   — 波浪式消除爆发（按与波源平均位置距离延迟 0~0.3s）
│                        → 障碍视图全量刷新 → 特殊棋子生成 PopScale → 目标进度 → 停顿 0.3s
│                        → ApplyGravity → AnimateGravity → SyncViewsToModel
├── SyncViewsToModel()  — 以模型为唯一事实源全量校准视图（位置吸附/cell 同步/缺建/孤儿清理）
├── CreatePieceView     — 创建棋子视图（RocketRow 旋转 -90°，RocketCol 原样）
├── CreateObstacleView / RefreshObstacleView — 木箱 HP 标签、冰层视图的创建与刷新
├── HUD 构建            — 目标栏（图标+文字说明+进度）、步数、暂停、棋盘格底（7×9 逐格平铺）
├── 弹窗                — CreateModal 九宫格面板：暂停/胜利/失败/续关/生命不足
└── WinBonusSequence    — 剩余步数逐个转化为随机特殊棋子引爆
```

#### MainMenuUI.cs（主界面）
庄园背景 Cover → 顶栏（生命/金币/星星，右缘锚定设置按钮）→ 关卡地图九宫格面板（缎带标题）→ 关卡节点网格（三态 + 困难配色 + 锁定态）→ 底部按钮区 → 设置/商城弹窗 → Toast。每帧刷新生命恢复倒计时。

#### UIFactory.cs（UGUI 工厂）
全程序化 UI 的基础工具：`Panel / MakeImage(支持关闭等比) / Text(带描边重载) / TextButton(GameObject 重载，文字光学居中) / IconButton / ProgressBar / Anchored / FitCover(背景等比铺满)`。中文字体走 `Font.CreateDynamicFontFromOSFont`（微软雅黑）。

#### SpriteLib.cs（素材库）
`Resources.Load<Texture2D> + Sprite.Create` 加载 `Art/Resources/Clean/` 下的独立 PNG；九宫格边框统一设置（按钮/进度条/面板）；含 `PieceFor / FxSparkle / FxRainbowWave` 等查询接口。**已无手工切图坐标**（素材管线预裁切，见关键技术点）。

#### PieceInput.cs（棋子输入组件）
挂在每个棋子视图上：记录 `cell` 坐标，区分点击/拖拽（位移阈值），回调给 BoardController。

#### EffectsRunner.cs（特效运行器，单例）
| 方法 | 时长 | 用途 |
|---|---|---|
| Burst / BurstDelayed | 0.55s（可延迟） | 消除爆发，缩放放大+平方淡出 |
| FloatText | 1.4s | 飘字（前 70% 不透明，55px/s 上浮） |
| Flash | 0.45s 默认 | 全屏闪光 |
| RainbowWave | 0.8s | 彩虹波扩散 |
| MoveRect / PopScale | 参数化 | 补间/弹出（SmoothStep + 过冲） |

---

## 四、数据流和调用链

### 启动流程
```
打开 MainMenu.unity
  └─ Bootstrap.Awake()
       ├─ 创建 EventSystem / Canvas / GameFlow / SoundManager / EffectsRunner
       ├─ [MainMenu 场景] 挂 MainMenuUI → 构建主界面全部 UI
       └─ [Gameplay 场景] 挂 BoardController → GameFlow.StartLevel 读 LevelDatabase
            └─ BoardModel.Init(level) → 建视图 → HUD
```

### 玩家一次交换的完整链路
```
PieceInput.OnDrag
  └─ BoardController.AttemptSwap(a, b)
       ├─ 模型交换前按 Piece 预取 va/vb（防错位的关键）
       ├─ model.TrySwap(a,b) → ClearPlan 或 null
       └─ SwapAndResolve
            ├─ [null] 两视图回弹 → Idle（不耗步数）
            └─ [plan] 并行滑动 → 步数-1 → ExecutePlan(plan)
                 ├─ [有 comboName] 闪光+飘字+彩虹波 → 停 0.45s
                 └─ CascadeLoop(plan)
                      └─ ResolvePlan
                           ├─ model.ApplyPlan → ClearResult（消除/障碍扣血）
                           ├─ 波浪式 Burst（中心向外延迟）→ 障碍视图刷新
                           ├─ 特殊棋子生成（consumed 去重后按优先级）
                           ├─ 目标进度更新 → [全完成] WinBonusSequence
                           ├─ model.ApplyGravity → AnimateGravity → SyncViewsToModel
                           └─ FindRuns 有新三连 → 下一轮级联
```

### 单击特殊棋子链路
```
PieceInput.OnPieceUp(isClick=true)
  └─ ActivateSpecialAt(cell)
       └─ model.DetonateAt → ActivateRoutine
            ├─ PulseView（棋子脉冲放大 1.5×回弹，0.45s）
            ├─ 飘字 ComboHint + Burst，停 0.35s
            └─ ExecutePlan(plan)   ← 与交换共用同一套结算
```

---

## 五、关键技术点

### 场景自举架构（零序列化引用）
两个场景的 YAML 只含 Camera + Bootstrap 对象，所有 UI/棋盘运行时程序化构建。优点：可用文本工具直接生成/修改场景，无 Prefab 拖拽依赖，任何文件级改动都能被 Unity 直接编译运行。

### 纯 C# 数据层与视图校准
`BoardModel` 不依赖 UnityEngine.Object（仅用 Vector2Int 等），消除/重力/洗牌全部在数据层完成后再驱动表现。`SyncViewsToModel()` 在每次重力后以模型为唯一事实源全量校准（位置吸附、cell 同步、缺视图补建、孤儿清理）——彻底杜绝"视图与数据错位"类 bug（图标重叠、所见三连与实际不符、空位不补）。

### 交换动画的取视图时序
`TrySwap` 会立即交换模型数据，因此必须在**模型交换前**按 `Piece` 对象键预取两个视图再传入动画协程——按位置取视图会拿到"交换后"的相反棋子，导致动画与数据永久错位（本项目的根因级 bug 教训）。

### 素材清理管线（.workbuddy/tools/）
美术 PNG 的"透明棋盘格"是**烘焙在图里的假透明**（alpha 全 255）。管线：泛洪填充去除棋盘格底 + 抗锯齿噪点清理（3×3 开运算）→ 自动擦除水印 → 按帧 alpha 包围盒精确裁切 → 输出约 35 张干净独立 PNG 至 `Assets/Art/Resources/Clean/{Pieces,Buttons,Icons,Levels,Bars,Effects,UI}`，并生成拼贴验证图。运行时直接加载单帧图，**不存在切图坐标错误的可能性**。

### 波浪式消除演出
大面积消除（火箭十字、5×5、全屏清除）按每格与波源平均位置的距离延迟 `min(dist×0.018, 0.3)s` 爆发，清除扩散方向清晰可见。波源通过 foreach 求平均得到（`ClearPlan.cells` 是 HashSet，不可下标索引）。

### 分段重力
`ApplyGravity` 以木箱为分段边界：每段内部棋子独立下落补位，顶部新棋子从棋盘外生成下落；木箱自身不动。返回 `GravityMove{col, fromRow, toRow, piece}` 列表供表现层做逐棋子补间。

### 竖屏 UI 适配
CanvasScaler 以高度为基准（matchWidthOrHeight=1），画布高恒为 2532、宽随屏幕比例变化；`FitCover` 按"实际画布宽 = 2532 × 屏幕宽高比"计算等比缩放铺满裁切。功能性元素（顶栏、底部按钮）锚定屏幕边缘，棋盘/面板居中锚定，任意宽高比下不漂移。

### 已实现范围与规划边界

| 模块 | 状态 |
|---|---|
| 三消核心 / 4 特殊棋子 / 6 组合技 / 单击激活 | ✅ 完整实现 |
| 木箱 / 冰层障碍 + 分段重力 | ✅ 完整实现 |
| 关卡配置驱动（JSON）+ 10 关 | ✅ 完整实现 |
| 生命/金币/星星/续关/存档 | ✅ 完整实现 |
| 主界面 / 关卡内全部 UI / 程序音效 | ✅ 完整实现 |
| 庄园建设 / 装饰 / 剧情 | ⬜ 占位（星星已入账，消耗口待建） |
| 团队 / 排行榜 / 活动 / 商城 / 内购 | ⬜ 占位入口（对应策划案 P2–P5 里程碑） |
| 锁链/石块/泡沫等其余 10 种障碍 | ⬜ 障碍框架已按 kind+hp 配置化，扩展只需加 kind |

---

## 六、关卡配置（levels.json）

路径：`Assets/Resources/Levels/levels.json`，JsonUtility 反序列化为 `LevelList`。

```json
{
  "id": 9, "steps": 28, "difficulty": "NORMAL", "rewardGold": 150,
  "goals": [
    { "type": "clear", "target": "ice", "count": 8 },
    { "type": "collect", "color": "gem", "count": 10 }
  ],
  "crates": [],
  "ices": [ { "x": 1, "y": 2, "hp": 2 }, ... ]
}
```

**字段说明**
- `steps` — 步数；`difficulty` — NORMAL/HARD（影响金币奖励与 UI 标注）；`rewardGold` — 通关金币
- `goals[]` — 最多 3 目标；`collect` 按颜色计数，`clear` 按障碍格销毁数计数
- `crates[]/ices[]` — 障碍实例，`hp` 为层数；**约束：clear 目标的 count 必须等于对应障碍格数**（ice/crate 均按格计数，hp 只影响破坏次数）

**10 关渐进设计**
| 关 | 目标 | 新要素 |
|---|---|---|
| 1–2 | 收集颜色 | 基础三连 |
| 3–5 | 收集 + 步数收紧 | 特殊棋子自然产出 |
| 6–8 | 收集 + 木箱 4–6 | 木箱阻挡下落 |
| 9 | 冰层 8 + 收集 10 | 冰层（格内消除扣血） |
| 10 | 木箱 6 + 冰层 4 + 收集（HARD） | 双障碍混合 |
