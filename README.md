# Tanuki

Rhino 8 向け建築図面自動生成プラグイン。  
3D モデルから平面図・断面図・立面図・天井伏図を生成し、シートへの配置・PDF 出力までを一貫して提供する。

---

## 何を解決するのか

建築設計における「3D と図面の乖離」を防ぐ。

Rhino で 3D モデルを修正するたびに CAD ソフトで図面を手直しする、という典型的な二重管理の手間を排除する。Tanuki では図面はモデルから毎回算出される副産物であり、3D が変われば図面も `TanukiUpdateAll` 一発で追従する。

**成功指標**: 図面作成時間の短縮。機能数ではない。

---

## コンセプト

### 3D モデルが唯一の真実

図面データは 3D モデルから毎回算出し直す。図面オブジェクトを手で直すことは想定していない（直しても次の再生成で上書きされる）。

### 図面は副産物

生成された線・文字は通常の Rhino オブジェクト（`Curve` / `TextEntity`）として保存されるが、それらは常に「再生成可能な出力」である。消えても問題ない。

### Rhino ネイティブ

- 外部 DB・独自ファイル形式を持たない
- プロジェクトデータは `doc.Strings` に JSON で保存 → `.3dm` ファイル 1 つで完結
- 生成オブジェクトは Rhino の標準オブジェクト → 任意のツールで加工できる

### 再生成は安全・確実

既存図面を全削除してから再生成する（`DrawingPlacer.DeleteViewLayers` → 再配置）。差分更新は行わない。シンプルさと正確さを優先する。

---

## 主な機能

| 機能 | コマンド | 説明 |
|------|----------|------|
| 通り芯設定 | `TanukiSetupGrid` | 通り芯を追加・削除・均等配置。バブル記号付きで 3D 表示 |
| レベル設定 | `TanukiSetupLevel` | 階高（GL/1FL/RF 等）を設定し断面・立面に参照線を表示 |
| 平面図 | `TanukiFloorPlan` | 水平カット面との交差線を抽出して平面図を生成 |
| 天井伏図 | `TanukiRCP` | 平面図と同処理 + Y 軸ミラーで RCP を生成 |
| 断面図 | `TanukiSection` | 任意方向の垂直切断面 + 視線方向投影で断面図を生成 |
| 立面図 | `TanukiElevation` | 断面図と同処理で立面図を生成 |
| 寸法線 | （自動） | 通り芯間寸法（平面図）、レベル間寸法（断面/立面）を自動生成 |
| 全図面更新 | `TanukiUpdateAll` | 登録済み全図面を一括再生成 |
| 自動配置 | `TanukiAutoLayout` | 通り芯グリッドを基準に全図面を自動整列 |
| DXF 出力 | `TanukiExport` | 選択した図面レイヤーを Rhino 標準ダイアログで DXF/DWG 出力 |
| シート生成 | `TanukiSheet` | Rhino レイアウトにビューポートを自動作成 |
| タイトルブロック | `TanukiTitleBlock` | レイアウトのページ座標にタイトルブロックを配置 |
| PDF 出力 | `TanukiPDF` | 選択したレイアウトをアクティブにして印刷ダイアログを起動 |

全コマンドに `Tnk*` 短縮形が存在する（例: `TnkFloorPlan`）。

---

## アーキテクチャ概要

```
UI Layer       TanukiPanel / TanukiGridPanel / TanukiLevelPanel / TanukiSectionPanel
                    ↕ GridLinesChanged / ViewsChanged イベント
Command Layer  TanukiFloorPlan / TanukiSection / TanukiSetupGrid / ...
                    ↓
Generator Layer  ViewGenerator → LineClassifier → DrawingPlacer
                                              → CurveCleanup
                                              → DimensionGenerator
                 GridLineDrawer / LevelDrawer / MarkerDrawer
                    ↓
Data Layer     TanukiProject (doc.Strings に JSON 永続化)
               ├─ GridLine[]  ├─ Level[]  └─ ViewDef[]
```

詳細は [docs/architecture.md](docs/architecture.md) を参照。

---

## ビルド方法

### 必要環境

- .NET SDK 7.0 以降
- Rhino 8（`RhinoCommon 8.0.23304.9001` 以上）

### ビルド

```bash
git clone https://github.com/satw-jp/Tanuki.git
cd Tanuki
dotnet build
```

出力: `bin/Debug/net7.0/Tanuki.rhp`（Mac）/ `bin/Debug/net48/Tanuki.rhp`（Windows）

### Rhino へのインストール

1. Rhino を起動
2. `_PlugInManager` コマンドを実行
3. `Install…` から `Tanuki.rhp` を選択
4. Rhino を再起動

### リリースビルド & パッケージ

```bash
dotnet build --configuration Release
# GitHub Actions (release.yml) が v* タグ push 時に Yak パッケージを自動ビルド・リリース
```

---

## ディレクトリ構成

```
Tanuki/
├── Tanuki.csproj           # net7.0;net48 マルチターゲット
├── TanukiPlugin.cs          # PlugIn 本体 / イベント登録 / 移動追従
├── manifest.yml             # Yak パッケージメタデータ
│
├── Commands/                # Rhino コマンド（自動検出）
│   ├── AddViewCommand.cs    # TanukiFloorPlan / Section / Elevation / RCP
│   ├── AliasCommands.cs     # Tnk* 短縮形コマンド群
│   ├── AutoLayoutCommand.cs # TanukiAutoLayout
│   ├── ExportCommand.cs     # TanukiExport (DXF/DWG)
│   ├── PdfCommand.cs        # TanukiPDF
│   ├── PlaceViewCommand.cs  # TanukiPlaceView
│   ├── PropertiesCommand.cs # TanukiProperties
│   ├── SetupGridCommand.cs  # TanukiSetupGrid
│   ├── SetupLevelCommand.cs # TanukiSetupLevel
│   ├── SheetCommands.cs     # TanukiSheet / TanukiPrint
│   ├── ShowPanelCommands.cs # パネル開閉コマンド
│   └── TitleBlockCommand.cs # TanukiTitleBlock
│
├── Data/                    # データモデル（JSON シリアライズ対象）
│   ├── TanukiProject.cs     # ルート集約 / Load / Save / Migrate
│   ├── GridLine.cs          # 通り芯 1 本の定義
│   ├── Level.cs             # レベル（階高）1 段分
│   └── ViewDef.cs           # 図面 1 枚の定義 / GetCutPlane / GetViewDirection
│
├── Generators/              # 図面生成ロジック（Rhino 非依存部分を多く含む）
│   ├── ViewGenerator.cs     # 図面生成メインエントリ
│   ├── LineClassifier.cs    # 断面線 / 見え掛かり / 隠れ線の分類
│   ├── DrawingPlacer.cs     # レイヤー作成 / Rhino オブジェクト配置
│   ├── CurveCleanup.cs      # 重複線除去 / 可視優先フィルター
│   ├── DimensionGenerator.cs# 寸法線自動生成
│   ├── GridLineDrawer.cs    # 通り芯 3D 可視化 / 移動追従
│   ├── GridSymbolGenerator.cs# 通り芯バブル記号
│   ├── LevelDrawer.cs       # レベルフレーム 3D 可視化
│   └── MarkerDrawer.cs      # 断面/立面マーカー・インジケーター
│
├── UI/                      # Eto.Forms パネル
│   ├── TanukiPanel.cs       # メインツールバーパネル
│   ├── TanukiGridPanel.cs   # 通り芯管理パネル
│   ├── TanukiLevelPanel.cs  # レベル管理パネル
│   └── TanukiSectionPanel.cs# 図面一覧パネル
│
├── DESIGN.md                # 設計仕様書（詳細）
└── docs/
    ├── architecture.md      # アーキテクチャ詳解
    ├── coding-rules.md      # コーディング規約（AI・OSS 向けガイドライン）
    ├── roadmap.md           # ロードマップ / 実装状況
    └── known-issues.md      # 既知の技術的課題
```

---

## 開発方針

### シンプルさ優先

抽象化・汎化より動くコードを優先する。3 行の重複は許容する。不要な将来対応を作らない。

### 差分更新より再生成

図面の状態管理は複雑になりがちなので行わない。常に全削除 → 全再生成でよい。

### Rhino との親和性優先

Rhino のデータ構造・慣習に従う。独自フレームワークを導入しない。生成したオブジェクトは Rhino の標準ツールで操作できること。

### コマンドとパネルは等価

パネルのボタンがなければコマンドでも同じ操作ができる。UIでしか到達できない機能を作らない。

詳細な規約は [docs/coding-rules.md](docs/coding-rules.md) を参照。
