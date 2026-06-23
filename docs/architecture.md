# Tanuki — アーキテクチャ詳解

## システム構成

Tanuki は 4 つの層で構成される。層間の依存は上から下への一方向のみ。

```
┌─────────────────────────────────────────────────────┐
│  UI Layer  (Eto.Forms)                               │
│  TanukiPanel / TanukiGridPanel                       │
│  TanukiLevelPanel / TanukiSectionPanel               │
└───────────────────────┬─────────────────────────────┘
           ↑ 静的イベント │ GridLinesChanged / ViewsChanged
           │             ↓
┌─────────────────────────────────────────────────────┐
│  Command Layer  (RhinoCommon.Command 継承)            │
│  AddViewCommand / SetupGridCommand / SheetCommands … │
└───────────────────────┬─────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────┐
│  Generator Layer                                     │
│  ViewGenerator  ←  LineClassifier                    │
│                 ←  DrawingPlacer ← CurveCleanup      │
│                 ←  DimensionGenerator                │
│                 ←  GridSymbolGenerator               │
│  GridLineDrawer / LevelDrawer / MarkerDrawer         │
└───────────────────────┬─────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────┐
│  Data Layer  (System.Text.Json)                      │
│  TanukiProject  →  doc.Strings["TanukiProject"]      │
│    GridLine[] / Level[] / ViewDef[]                  │
└─────────────────────────────────────────────────────┘
```

---

## データフロー — モデル変更から図面出力まで

```
ユーザーが Rhino でモデルを編集
          ↓
[A] 通り芯を移動した場合
  ReplaceRhinoObject イベント
    → TanukiPlugin.OnObjectReplaced
    → GridLineDrawer.TryUpdateFromObject  ← LineObjectId で高速検索
                                          ← PersistentId (UserString) でフォールバック
    → GridLine.Origin / Direction / Length を更新
    → GridLineDrawer.SyncSymbols（バブル再生成）
    → ViewGenerator.Generate（全平面図を再生成）
    → TanukiPlugin.RaiseGridLinesChanged() → UI パネルが Refresh()

[B] 断面マーカーを移動した場合
  ReplaceRhinoObject イベント
    → TanukiPlugin.OnObjectReplaced
    → MarkerObjectId と照合して ViewDef を特定
    → MarkerDrawer.DeleteIndicators（旧インジケーター削除）
    → MarkerDrawer.DrawIndicators（新位置に再描画）
    → ViewDef.CutStart/End を更新 → TanukiProject.Save
    → ViewGenerator.Generate（断面図を再生成）

[C] ユーザーがコマンドで再生成
  TanukiFloorPlan / TanukiSection / TanukiUpdateAll
    → ViewGenerator.Generate(doc, view, project)
    → GetOffset (配置位置を決定)
    → GenerateFloorPlan or GenerateSectionOrElevation
    → DrawingPlacer.Place (CurveCleanup → Rhino オブジェクト追加)
    → DimensionGenerator.AddFloorPlanDimensions / AddSectionLevelDimensions
    → AddDrawingTitle
    → TanukiProject.Save
    → TanukiPlugin.RaiseViewsChanged()
```

---

## オブジェクトライフサイクル

### GridLine

```
作成 (TanukiSetupGrid)
  PersistentId = Guid.NewGuid()  ← 永続
  LineObjectId = Guid.Empty
    ↓
GridLineDrawer.SyncLines
  LineObjectId = doc.Objects.AddLine(...)  ← Rhino に書き込み
  rhObj.SetUserString("TanukiPersistentId", ...)  ← 二重保存
    ↓
移動 (ReplaceRhinoObject)
  LineObjectId を新 ID で上書き
  Origin / Direction / Length を更新
  PersistentId は変わらない  ← undo/redo 後のフォールバックとして機能
    ↓
削除 (TanukiSetupGrid → Remove)
  Rhino オブジェクト削除
  GridLine を project.GridLines から除去
  TanukiProject.Save
```

### Level

```
作成 (TanukiSetupLevel)
  Name / Elevation を入力
  project.Levels に追加 → Save
    ↓
LevelDrawer.SyncAll
  「Tanuki::レベル::線」レイヤーに水平フレームを描画
  「Tanuki::レベル::記号」レイヤーにラベルを描画
    ↓
図面生成時
  ViewGenerator.AddLevelLines  → 断面/立面に参照破線を追加
  ViewGenerator.AddLevelLabels → レベル名・高さラベルを追加
  DimensionGenerator.AddSectionLevelDimensions → レベル間寸法
```

### ViewDef

```
作成 (TanukiFloorPlan / TanukiSection / ...)
  Id = Guid.NewGuid()
  Name = ユーザー入力
  LayerKey = Name  ← 作成時に固定。以後変更不可
  HasPlacement = false
    ↓
ViewGenerator.Generate
  GetOffset → HasPlacement = true, PlacedOffsetX/Y を保存
  図面オブジェクトを「Tanuki::{LayerKey}::*」レイヤーに配置
    ↓
リネーム (パネル / TanukiSectionPanel)
  Name を変更 → Save
  LayerKey は変更しない → Rhino レイヤーはそのまま維持
    ↓
削除 (パネル / TanukiSectionPanel)
  DrawingPlacer.DeleteViewLayers(doc, view.GetLayerKey())
  project.Views から除去 → Save
```

---

## レイヤー構造

Rhino のレイヤー階層をネームスペースとして使用する。セパレータ `::` は Rhino の慣例に従う。

```
Tanuki
  ├─ 通り芯
  │    ├─ 線          Color: #0080FF  ← GridLine の Rhino 実体（移動追従対象）
  │    └─ 記号        Color: #0080FF  ← バブル円 + テキスト（再生成専用）
  ├─ レベル
  │    ├─ 線          Color: DimGray
  │    └─ 記号        Color: DimGray
  ├─ Markers          Color: Magenta  ← 切断線マーカー + 視線インジケーター
  ├─ {LayerKey}                       ← ViewDef.LayerKey（作成時固定）
  │    ├─ 断面線      Color: Red      ← 切断面との交差線
  │    ├─ 見え掛かり  Color: Black    ← 表向き面に隣接するエッジ
  │    ├─ 隠れ線      Color: Gray     ← 裏向き面のみに隣接するエッジ
  │    ├─ 寸法        Color: DimGray  ← 通り芯・レベル寸法チェーン
  │    └─ タイトル    Color: DimGray  ← 図面名・縮尺テキスト
  ├─ PrintFrames      Color: Blue     ← TanukiPrint の印刷枠
  └─ TitleBlock       Color: Black    ← TanukiTitleBlock（レイアウト page space）
```

**セキュリティ規則**: ユーザー入力の文字列はすべて `.Replace("::", "_")` を通してからレイヤーパスに使う。`::` を含む入力はレイヤー階層を逸脱する可能性がある。

**OriginalLayer モード**: `LayerMode.OriginalLayer` では `{LayerKey}::{元レイヤー名}::断面線` のように元の Rhino レイヤー名をさらに挟む。部位別管理が可能だがレイヤー数が増える。

---

## 図面生成パイプライン

### 平面図 / 天井伏図

```
LineClassifier.ClassifyFloorPlan(doc, cutHeight, reflected)
  → Brep.CreateContourCurves(水平カット面) [断面線のみ]
  → reflected=true の場合: Y 軸ミラー Transform を適用

GridSymbolGenerator.GenerateSymbols(gridLines, bubbleRadius)
  → 通り芯バブル円 + テキストカーブを生成 [LineType.Visible]

DrawingPlacer.Place(doc, layerKey, curves, offset, layerMode)
  → CurveCleanup.Process(curves, tol)   ← 重複除去・可視優先
  → Tanuki::{LayerKey}::* レイヤーにオブジェクト追加

GridSymbolGenerator.PlaceGridText(doc, gridLines, layerIdx, offset, bubbleRadius)
  → バブル内のテキストを追加

DimensionGenerator.AddFloorPlanDimensions(doc, view, project, offset)
  → N-S グリッド: 上端に水平寸法チェーン
  → E-W グリッド: 左端に垂直寸法チェーン

AddDrawingTitle → 図面名 + 縮尺テキスト
```

### 断面図 / 立面図

```
projPlane = new Plane(CutStart, viewDir)
flatten   = Transform.PlaneToPlane(projPlane, Plane.WorldXY)

LineClassifier.Classify(doc, cutPlane, viewDir)
  → GetCutCurves   [断面線]: Brep.CreateContourCurves(cutPlane)
  → ClassifyEdges  [見え掛かり/隠れ線]:
       bboxフィルター: (corner - cutOrigin)·viewDir < 0 のオブジェクトをスキップ
       ClassifyBrepEdges:
         face.NormalAt(u,v) * (OrientationIsReversed ? -1 : 1)
         n · viewDir < 0 → front-facing
         edge.TrimIndices() → 隣接面に front-facing があれば Visible, なければ Hidden
       Extrusion → ToBrep() して再帰
       Mesh → GetNakedEdges() → Visible
       Curve → Visible

AddCrossingGridLines → 通り芯との交差垂直線 + バブル（projPlane 上）
AddLevelLines        → レベル参照水平破線（projPlane 上）

foreach curve: curve.Transform(flatten)   ← 鉛直面 → XY 平面に展開
  projPlane.XAxis → World X (水平距離)
  projPlane.YAxis (= WorldZ) → World Y (高さ)

DrawingPlacer.Place(doc, layerKey, curves, offset, layerMode)
  → CurveCleanup.Process(curves, tol)

AddCrossingGridText / AddLevelLabels → flatten + offset 済みの座標にテキスト追加

DimensionGenerator.AddSectionLevelDimensions(doc, view, project, offset, flatten)
  → レベル間垂直寸法チェーン（切断線始点の左側）

AddDrawingTitle
```

---

## 配置オフセット（GetOffset）

図面をモデル空間のどこに配置するかを決める戦略。

| 図面タイプ | 自動配置先 | 理由 |
|-----------|-----------|------|
| FloorPlan / RCP | X=0 固定、Y 方向に積み重ね | 通り芯 X 位置が平面図間で縦に揃う |
| Section / Elevation | モデル右側の X 方向に並列 | フラット化後は全部 Y=0 なので横に並べやすい |

`HasPlacement = true` の場合は保存済みオフセットを使う（`TanukiPlaceView` / `TanukiAutoLayout` で上書き可能）。

---

## 永続化方式

### なぜ doc.Strings を使うのか

`doc.Strings` は Rhino の `.3dm` ファイルに埋め込まれるキーバリューストア。

- ファイルを保存すればデータも保存される（追加操作不要）
- `.3dm` 1 ファイルを渡すだけでプロジェクト設定も共有できる
- バックアップ・バージョン管理が既存の `.3dm` フローに乗る

外部 DB や設定ファイルを導入しないことで、「どこに何が保存されているか」の複雑さを排除する。

### なぜ再生成方式なのか

差分更新（既存オブジェクトを追跡して変更箇所だけ更新）は次の問題を抱える:

1. **状態同期の複雑さ**: どのオブジェクトが「最新」かを常に追跡する必要がある
2. **エッジケース増加**: 部分更新は「一部古い状態が残る」バグを生みやすい
3. **Rhino の Undo と干渉**: Rhino の Undo スタックと独自の差分追跡が衝突する

全削除 → 全再生成は遅いように見えるが、実際の建築モデルでは数秒以内に完了する。シンプルさと正確さを得るための合理的なトレードオフ。

---

## スキーマバージョンとマイグレーション

`TanukiProject.SchemaVersion`（現在: 2）でデータ移行を管理する。

`TanukiProject.Load()` はデシリアライズ後に `Migrate()` を呼ぶため、旧バージョンの `.3dm` を開いても透過的に補完される。

| バージョン | 変更内容 |
|-----------|---------|
| v1 (初期) | GridLine, Level, ViewDef の基本構造 |
| v2 | GridLine.PersistentId 追加 / ViewDef.LayerKey 追加 |

新しいフィールドを追加する場合は SchemaVersion を上げ、`Migrate()` に補完ロジックを追加する。
