# Tanuki — 設計仕様書

Rhino 3D 向けの建築図面自動生成プラグイン。  
3D モデルから平面図・断面図・立面図を生成し、シートに配置するまでの一貫したワークフローを提供する。

---

## 設計思想

### 「3D モデルが唯一の真実」

図面データは 3D モデルから毎回算出し直す。CAD ソフトでありがちな「図面側を手で直したら 3D と齟齬が出る」状況を避けるため、図面は **常に再生成可能な副産物** として扱う。

### Rhino ネイティブ

- 生成した線・文字はすべて通常の Rhino オブジェクト（Curve / TextEntity）
- 専用ファイル形式や外部 DB は持たない
- プロジェクトデータは `doc.Strings`（Rhino ドキュメント内）に JSON で保存

これにより、`.3dm` ファイル 1 つを渡せばプロジェクト設定ごと共有できる。

### コマンド＋パネルの二重インターフェース

コマンドラインからすべての操作が可能で、パネルはそのショートカット。どちらを使っても同じ結果になる。

### 「再生成は安全」

既存の図面オブジェクトをすべて削除してから再作成する（`DrawingPlacer.DeleteViewLayers` → 再配置）。差分更新は行わない。シンプルさと正確さを優先する。

---

## アーキテクチャ

```
┌─────────────────────────────────────────────┐
│  UI Layer  (Eto.Forms)                       │
│  TanukiPanel / TanukiGridPanel               │
│  TanukiLevelPanel / TanukiSectionPanel       │
└──────────────┬──────────────────────────────┘
               │ イベント通知
               │ TanukiPlugin.GridLinesChanged
               │ TanukiPlugin.ViewsChanged
┌──────────────▼──────────────────────────────┐
│  Command Layer  (RhinoCommon)                │
│  TanukiFloorPlan / TanukiSection / ...       │
│  TanukiSetupGrid / TanukiAutoLayout / ...    │
└──────────────┬──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  Generator Layer                             │
│  ViewGenerator                               │
│    └─ LineClassifier (断面線・投影エッジ分類) │
│    └─ DrawingPlacer  (レイヤー配置)          │
│    └─ GridSymbolGenerator (通り芯記号)       │
│  GridLineDrawer  (通り芯 3D 可視化)          │
│  LevelDrawer     (レベル 3D 可視化)          │
│  MarkerDrawer    (断面/立面マーカー)         │
└──────────────┬──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  Data Layer  (System.Text.Json)              │
│  TanukiProject (doc.Strings に永続化)        │
│    ├─ GridLine[]  通り芯データ               │
│    ├─ Level[]     レベル（階高）データ       │
│    └─ ViewDef[]   図面定義データ             │
└─────────────────────────────────────────────┘
```

---

## データモデル

### TanukiProject

`doc.Strings["TanukiProject"]` に JSON でシリアライズして保存。

```
TanukiProject
  ├─ SchemaVersion: int     (現在 2 — マイグレーション用)
  ├─ GridLines    : GridLine[]
  ├─ Levels       : Level[]
  ├─ Views        : ViewDef[]
  ├─ LayerMode    : LineType | OriginalLayer
  ├─ BubbleRadius : double  (通り芯バブル半径 mm、デフォルト 400)
  └─ ViewScale    : int     (縮尺分母、デフォルト 100)
```

`Load()` はデシリアライズ後に `Migrate(p)` を呼ぶ。`SchemaVersion < 2` の旧データは自動的に補完される（後述）。

### GridLine

通り芯 1 本の定義。

```
GridLine
  ├─ PersistentId : Guid    (作成時に固定。アンドゥ/リドゥをまたいでも不変)
  ├─ Name         : string  (表示名、例: "X1", "Y-A")
  ├─ OriginX/Y    : double  (始点 XY 座標 mm)
  ├─ DirectionX/Y : double  (単位方向ベクトル)
  ├─ Length       : double  (長さ mm)
  └─ LineObjectId : Guid    (Rhino オブジェクト ID — 移動追従に使用)
```

`LineObjectId` は Rhino の `ReplaceRhinoObject` イベントで旧 ID → 新 ID に追従させるための鍵。アンドゥ時には Rhino が新しい ObjectId を振り直すため、`LineObjectId` だけでは追跡できないことがある。その場合のフォールバックとして `PersistentId` を Rhino オブジェクトの UserString `"TanukiPersistentId"` にも書き込んでおき、2 段階で検索する。

### Level

レベル（階高）1 段分。

```
Level
  ├─ Name      : string  (例: "GL", "1FL", "2FL", "RF")
  └─ Elevation : double  (Z 座標 mm)
```

### ViewDef

図面 1 枚の定義。平面図・断面図・立面図・天井伏図を共通クラスで管理。

```
ViewDef
  ├─ Id            : Guid
  ├─ Name          : string    (表示名 — パネル/タイトルに表示。変更可)
  ├─ LayerKey      : string    (レイヤーパスキー — 作成時に固定。Name を変えても不変)
  ├─ Type          : FloorPlan | RCP | Section | Elevation
  ├─ CutHeight     : double   (平面図・天井伏図のカット高さ)
  ├─ CutStartX/Y   : double   (断面/立面の切断線始点)
  ├─ CutEndX/Y     : double   (断面/立面の切断線終点)
  ├─ ViewRight     : bool     (断面: 右手側を見るか)
  ├─ MarkerObjectId    : Guid       (モデル上の切断線オブジェクト ID)
  ├─ MarkerIndicatorIds: Guid[]     (ティック・矢印・ラベルの ID 群)
  ├─ PlacedOffsetX/Y  : double     (図面の配置オフセット mm)
  └─ HasPlacement     : bool       (true = 手動/自動で配置済み)
```

`GetLayerKey()` メソッドで取得し、`LayerKey` が空なら `Name.Replace("::", "_")` にフォールバック（旧データ互換）。これにより、ユーザーが `Name` を変更しても Rhino のレイヤー構造（`Tanuki::{LayerKey}::...`）は壊れない。リネームは `Name` だけを更新し、Rhino レイヤーは触らない。

---

## レイヤー構造

Rhino のレイヤー階層をネームスペースとして使用する。セパレータは `::` 。

```
Tanuki
  ├─ 通り芯
  │    ├─ 線        ← GridLine の Rhino オブジェクト（移動可能・ID 追跡）
  │    └─ 記号      ← バブル円 + テキスト（再生成のみ）
  ├─ レベル
  │    ├─ 線        ← 水平フレーム
  │    └─ 記号      ← ラベル
  ├─ Markers        ← 断面/立面の切断線マーカー + 視線インジケーター
  ├─ {LayerKey}     ← 生成済み図面（ViewDef.LayerKey ごと、Name を変えても維持）
  │    ├─ 断面線    ← 赤 / 切断面との交差線
  │    ├─ 見え掛かり← 黒 / 投影エッジ（面法線で自己隠蔽判定済み）
  │    ├─ 隠れ線    ← グレー / 裏面エッジ（面法線が viewDir と同方向の面に隣接）
  │    └─ タイトル  ← 図面名・縮尺テキスト
  └─ PrintFrames    ← 印刷範囲可視化（TanukiPrint）
```

> **セキュリティ注意**: ユーザーが入力したビュー名や通り芯名には `::` を含めることができないよう、すべての入力箇所で `.Replace("::", "_")` を通している。Rhino のレイヤーパス区切り文字が混入すると別レイヤーを誤って操作する可能性があるため。

---

## 図面生成パイプライン

### 平面図 / 天井伏図

```
TanukiFloorPlan コマンド
  → LineClassifier.ClassifyFloorPlan(cutHeight)
       Brep.CreateContourCurves(水平カット面)
  → GridSymbolGenerator.GenerateSymbols(通り芯バブル)
  → DrawingPlacer.Place(curves, offset)
  → GridSymbolGenerator.PlaceGridText(通り芯ラベル)
  → AddDrawingTitle
```

天井伏図（RCP）は平面図と同じ処理で、`Transform.Mirror` を Y 軸について適用してから出力する。

### 断面図 / 立面図

```
TanukiSection / TanukiElevation コマンド
  → ViewDef に CutStart/End/ViewRight を記録
  → MarkerDrawer.DrawIndicators(切断線 + 視線インジケーター)
  → ViewGenerator.GenerateSectionOrElevation
       1. projPlane = Plane(CutStart, viewDir)
          flatten = Transform.PlaneToPlane(projPlane, WorldXY)
       2. LineClassifier.Classify
            GetCutCurves    → 切断面との交差線（断面線）
            ClassifyEdges   → エッジを面法線で可視/隠れ線に分類して投影
            ※ bbox フィルター: 切断面より手前（視線逆方向）のオブジェクトはスキップ
       3. AddCrossingGridLines → 通り芯との交差垂直線 + バブル
       4. AddLevelLines        → レベル参照水平破線
       5. foreach curve: curve.Transform(flatten)  ← XY 平面に展開
       6. DrawingPlacer.Place(curves, offset)
       7. AddCrossingGridText  → 通り芯ラベル（flatten + offset）
       8. AddLevelLabels       → レベルラベル（flatten + offset）
       9. AddDrawingTitle
```

#### 投影の座標系

`projPlane = new Plane(CutStart, viewDir)` において:

- `ZAxis` = `viewDir`（水平、視線方向）
- `XAxis` = `WorldZ × viewDir`（水平、切断線方向）
- `YAxis` = `WorldZ`（鉛直上向き）

`Transform.PlaneToPlane(projPlane, WorldXY)` を適用すると:

| projPlane 上の意味 | XY 平面上での意味 |
|--------------------|-------------------|
| 水平距離（切断線に沿った位置） | X 座標 |
| 高さ（Z 値）       | Y 座標 |

これにより断面図・立面図は平面図と同じ XY 平面に乗り、シートに並べて配置できる。

---

## 配置オフセット計算（GetOffset）

図面を Rhino モデル空間のどこに置くかを決める。`HasPlacement = true` の場合は保存済みオフセットをそのまま使う。

**平面図 / 天井伏図**: `Translation(0, y, -bbox.Min.Z)`  
モデルの下方 Y 方向に積み重ねる。通り芯が X=0 で縦に揃う。

**断面図 / 立面図**: `Translation(x, 0, 0)`  
モデルの右側 X 方向に並べる。フラット化後は Y=0（平面図と同じ Z 高さ）。

`TanukiAutoLayout` コマンドは全図面を一括でこの方針で再配置する。

---

## 移動追従メカニズム

### 通り芯の移動

`TanukiPlugin.OnObjectReplaced` → `GridLineDrawer.TryUpdateFromObject`

1. `ReplaceRhinoObject` イベントで旧 ID を `GridLine.LineObjectId` と照合
2. 一致したら新しいジオメトリから Origin / Direction / Length を更新
3. `SyncSymbols`（バブル再生成）
4. 全平面図を `ViewGenerator.Generate` で再生成

### 断面マーカーの移動

`TanukiPlugin.OnObjectReplaced` → セクション分岐

1. `MarkerObjectId` と照合して対応する `ViewDef` を特定
2. 旧インジケーター（ティック・矢印・ラベル）を `MarkerDrawer.DeleteIndicators` で削除
3. 新位置に `MarkerDrawer.DrawIndicators` で再生成
4. `ViewDef` の CutStart/End を更新して `ViewGenerator.Generate` で断面図を再生成

### パネルへの通知

`TanukiPlugin.GridLinesChanged` / `TanukiPlugin.ViewsChanged` の 2 つの静的イベントで、モデル側の変更をパネルに伝える。パネルはこれを購読して `Refresh()` を呼ぶ。

---

## コマンド一覧

| コマンド | 短縮形 | 説明 |
|----------|--------|------|
| `TanukiSetupGrid` | `TnkSetupGrid` | 通り芯の追加・削除・均等配置 |
| `TanukiSetupLevel` | `TnkSetupLevel` | レベル（階高）の設定 |
| `TanukiFloorPlan` | `TnkFloorPlan` | 平面図生成 |
| `TanukiRCP` | `TnkRCP` | 天井伏図生成 |
| `TanukiSection` | `TnkSection` | 断面図生成（3 点で方向指定） |
| `TanukiElevation` | `TnkElevation` | 立面図生成 |
| `TanukiUpdateAll` | `TnkUpdateAll` | 全図面を一括再生成 |
| `TanukiAutoLayout` | `TnkAutoLayout` | 通り芯基準で全図面を自動再配置 |
| `TanukiPlaceView` | `TnkPlaceView` | 個別図面の配置位置を変更 |
| `TanukiProperties` | `TnkProperties` | 選択オブジェクトのTanukiプロパティ表示 |
| `TanukiExport` | `TnkExport` | DXF/DWG エクスポート（Rhino標準ダイアログ） |
| `TanukiSheet` | `TnkSheet` | Rhinoレイアウトにビューポートを作成 |
| `TanukiPrint` | `TnkPrint` | 印刷範囲フレームをモデル空間に配置 |
| `TanukiSectionPanel` | `TnkSectionPanel` | 図面パネルを開く |

パネルコマンド: `TanukiOpenPanel` / `TanukiGridPanel` / `TanukiLevelPanel` / `TanukiSectionPanel`

---

## パネル UI

### TanukiPanel（メインツールバー）

- 図面リスト: クリックで対象レイヤーをズーム
- 再生成 / 削除 / リネーム
- レイヤー表示トグル: 断面線 / 見え掛かり / 隠れ線
- レイヤーモード切替: 線種別 / 元レイヤー別

### TanukiGridPanel

- 通り芯の追加（2 点クリック / 直接入力）
- 均等配置（選択した通り芯を等間隔に並べ直す）
- バブル半径の変更

### TanukiLevelPanel

- レベルの追加・編集・削除・並び替え
- 3D ビューポートへの可視化同期

### TanukiSectionPanel

- 断面図・立面図の一覧表示
- 再生成 / ズーム / 削除 / リネーム

---

## LineClassifier の分類ロジック

**断面線（Cut）**: `Brep.CreateContourCurves(cutPlane)` / `Mesh.CreateContourCurves` — 切断面との交差曲線。

**見え掛かり・隠れ線（ClassifyEdges）**: 切断面より視線方向側にあるオブジェクトのエッジを投影・分類する。

1. **bboxフィルター**: オブジェクトのバウンディングボックスの全角が `(corner - cutOrigin) · viewDir < 0` なら（切断面の手前＝ビューワー側のみ）スキップ。
2. **Brep の面法線判定 (ClassifyBrepEdges)**:
   - 各 BrepFace の定義域中点で `face.NormalAt(u, v)` を取得（`OrientationIsReversed` を考慮）
   - `n · viewDir < 0` → 表面（front-facing）
   - 各エッジの隣接面（`edge.TrimIndices()` → `brep.Trims[ti].Face`）を確認
   - 隣接面に1つでも表面があれば → **見え掛かり（Visible）**
   - 全隣接面が裏面 → **隠れ線（Hidden）**
3. **Extrusion**: ToBrep() して Brep として処理。
4. **Mesh**: naked edges のみ → 見え掛かり（簡略処理）。
5. **Curve**: 常に見え掛かり。

Tanuki レイヤー（`Tanuki::*`）のオブジェクトは分類対象から除外し、図面が図面を参照しない。

---

## LayerMode（レイヤーモード）

| モード | レイヤー構造 |
|--------|-------------|
| `LineType`（デフォルト）| `Tanuki::{ViewName}::断面線` / `::見え掛かり` / `::隠れ線` |
| `OriginalLayer` | `Tanuki::{ViewName}::{元レイヤー名}::断面線` など |

`OriginalLayer` モードでは建物の部位（壁・床・柱）ごとに細かく管理できるが、レイヤー数が増える。

---

## TanukiSheet（Rhino レイアウト連携）

`doc.Views.AddPageView(name, W, H)` でページビュー（印刷用レイアウト）を作成し、`AddDetailView` でモデル空間の図面をビューポートとして貼り付ける。

ビューポートは Top（真上から）投影で固定。断面図・立面図は XY 平面にフラット化されているため、平面図と同じ `SetCameraDirection(0, 0, -1)` で正しく見える。

図面の範囲は `GetDrawingBbox`（`Tanuki::{ViewName}` レイヤーの全オブジェクトの BoundingBox）から自動計算し、スケールを設定する。

---

## ビルド設定

- ターゲット: `net7.0;net48`（Rhino 8 Mac/Windows 両対応）
- 主要依存: `RhinoCommon 8.x`、`Eto.Forms`（同梱）、`System.Text.Json`
- 出力拡張子: `.rhp`

C# 7.3 互換の書き方に注意（`net48` ターゲットのため）:
- `new()` 省略不可 → `new List<Guid>()`
- `using var` 不可
- ラムダ内の匿名型には `new System.Action(() => { ... })`

---

## スキーマバージョンとマイグレーション

`TanukiProject.SchemaVersion` で旧データを自動変換する。

| v1 → v2 | 処理内容 |
|---------|---------|
| `GridLine.PersistentId` が未設定 | `Guid.NewGuid()` を割り当て |
| `ViewDef.LayerKey` が未設定 | `Name.Replace("::", "_")` を設定 |

`Load()` 呼び出し時に `Migrate()` が自動実行されるため、旧 `.3dm` を開いても透過的に動作する。

---

## CurveCleanup（重複線除去）

`DrawingPlacer.Place` の先頭で自動実行される。

1. **重複除去**: 同一線種グループ内で始点・終点が一致（モデル許容差内）するカーブを除去
2. **可視優先**: 隠れ線と同位置に可視線がある場合、隠れ線を削除（可視が優先）

---

## DimensionGenerator（寸法線自動生成）

### 平面図：通り芯寸法チェーン（AddFloorPlanDimensions）

図面生成直後に自動実行。グリッド線を方向別に分類して寸法チェーンを生成する。

- **N-S方向グリッド**（`|DirectionY| > |DirectionX|`）: 上端に水平寸法チェーン
- **E-W方向グリッド**（`|DirectionX| > |DirectionY|`）: 左端に垂直寸法チェーン
- グリッドが3本以上の場合は間隔チェーン＋最外寸法の2段構成

### 断面/立面：レベル寸法チェーン（AddSectionLevelDimensions）

- レベルをZ座標で昇順ソート
- フラット化変換後の Y 座標（= 高さ）を使って垂直寸法チェーン
- 切断線始点の左側に配置（dimX = startFlat.X − 2500）
- レベルが3段以上なら全体寸法も追加

レイヤー: `Tanuki::{LayerKey}::寸法`（グレー）

---

## TanukiTitleBlock（タイトルブロック配置）

`ObjectAttributes.Space = ActiveSpace.PageSpace` + `ViewportId` を使い、レイアウトのページ座標系（mm単位）にタイトルブロックを直接配置する。

**タイトルブロック構成**（右下マージン 5mm）:
```
┌────────────────────────────────────┐
│ プロジェクト名  │ 番号 │ 縮尺 │ 日付│  ← 上行
├────────────────────────────────────┤
│ 図面名          │ 作図 │ 確認 │ 承認│  ← 中行
├────────────────────────────────────┤
│ 作成者               │ 備考        │  ← 下行
└────────────────────────────────────┘
```

レイヤー: `Tanuki::TitleBlock`

---

## TanukiPDF（PDF出力）

選択したレイアウト（`RhinoPageView`）をアクティブにして `_Print` を実行し、Rhino の印刷ダイアログを開く。ユーザーが出力先として PDF を選択する。

---

## 今後の拡張ポイント

- **オブジェクト間オクルージョン**: 現在の隠れ線は自己隠蔽のみ（面法線ベース）。他オブジェクトが手前にある場合の隠線処理は未実装（Z バッファ or レイキャスト相当の処理が必要）
- **部屋名タグ**: 平面図に室名テキスト（LDK・寝室など）を配置
- **DWGレイヤーマッピング**: `Tanuki::断面線` → `A-SECTION` 等の変換テーブル
- **複数ドキュメント対応**: 現在は `RhinoDoc.ActiveDoc` 依存のため、マルチドキュメント環境では注意
