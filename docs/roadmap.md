# Tanuki — ロードマップ

実装状況を正確に反映する。コードに存在しない機能は「計画中」以降に記載する。

---

## 完了済み (Implemented)

### コア図面生成

- [x] **平面図** (`TanukiFloorPlan`) — 水平カット面でのコンター抽出
- [x] **天井伏図 (RCP)** (`TanukiRCP`) — 平面図 + Y 軸ミラー
- [x] **断面図** (`TanukiSection`) — 任意方向切断 + 投影 + フラット化
- [x] **立面図** (`TanukiElevation`) — 断面図と同アルゴリズム
- [x] **全図面一括更新** (`TanukiUpdateAll`)

### 隠れ線処理

- [x] **面法線による可視/隠れ分類** — `LineClassifier.ClassifyBrepEdges`
  - 面の法線と視線方向の内積で表向き/裏向きを判定
  - エッジに隣接する面に1つでも表向きがあれば「見え掛かり」
  - 全隣接面が裏向きの場合のみ「隠れ線」
- [x] **Bbox フィルター** — カット面の視線側にあるオブジェクトをスキップ
- [x] **重複線除去** (`CurveCleanup`) — O(n²) 始終点比較、可視優先
- [x] **Extrusion → Brep 変換** で Extrusion オブジェクトにも対応
- [x] **Mesh エッジ** — 裸エッジを見え掛かりとして取得

### 通り芯

- [x] **通り芯設定** (`TanukiSetupGrid`) — 追加・削除・均等配置
- [x] **バブル記号** — 円 + テキスト (`GridSymbolGenerator`)
- [x] **3D 可視化** (`GridLineDrawer.SyncLines`)
- [x] **移動追従** — `ReplaceRhinoObject` イベントで座標を更新
- [x] **PersistentId** — Undo/Redo 後のフォールバック識別子

### レベル

- [x] **レベル設定** (`TanukiSetupLevel`) — 名前・高さ
- [x] **3D 参照フレーム** (`LevelDrawer.SyncAll`)
- [x] **断面/立面への参照破線・ラベル追加**

### 寸法線 (v0.3)

- [x] **平面図: 通り芯間寸法** (`DimensionGenerator.AddFloorPlanDimensions`)
  - N-S グリッド → 上端に水平寸法チェーン
  - E-W グリッド → 左端に垂直寸法チェーン
  - グリッド 3 本以上で外側総寸法も追加
- [x] **断面図/立面図: レベル間寸法** (`DimensionGenerator.AddSectionLevelDimensions`)
  - フラット化済み座標に垂直寸法チェーン

### シート・出力 (v0.4)

- [x] **レイアウト生成** (`TanukiSheet`) — Rhino レイアウトにビューポートを自動作成
- [x] **印刷枠** (`TanukiPrint`) — モデル空間に印刷枠を描画
- [x] **タイトルブロック** (`TanukiTitleBlock`) — レイアウト page space に 180mm×55mm ブロック
- [x] **PDF 出力** (`TanukiPDF`) — レイアウトを選択して Rhino 印刷ダイアログを起動

### 図面管理

- [x] **配置オフセット管理** (`TanukiPlaceView` / `TanukiAutoLayout`)
- [x] **自動配置** (`TanukiAutoLayout`) — 通り芯グリッド基準で全図面を整列
- [x] **DXF/DWG 出力** (`TanukiExport`)
- [x] **プロパティ表示** (`TanukiProperties`)
- [x] **図面リネーム** — Name 変更、LayerKey は不変

### 永続化

- [x] **`doc.Strings` への JSON 保存** (`TanukiProject.Save`)
- [x] **SchemaVersion + Migrate()** — v1→v2 のデータ移行
- [x] **レイヤーモード** — LineType / OriginalLayer の切り替え

### UI

- [x] **TanukiPanel** — ツールバー + 図面リスト + レイヤー表示トグル
- [x] **TanukiGridPanel** — 通り芯管理パネル
- [x] **TanukiLevelPanel** — レベル管理パネル
- [x] **TanukiSectionPanel** — 図面一覧パネル
- [x] **全コマンドの `Tnk*` エイリアス**

### CI/CD

- [x] **GitHub Actions** (`release.yml`) — `v*` タグで Yak パッケージ自動ビルド・リリース

---

## 検討中 / 未実装 (Planned / Considering)

以下はアーキテクチャ上追加可能だが、まだ実装されていない。

### 隠れ線品質向上

- [ ] **非矩形ジオメトリのエッジ分割** — 現在は `edge.DuplicateCurve()` でエッジ全体を投影。曲面ではビジブル/ヒドゥンの境界が中間にある場合があり、その場合の分割処理が未実装
- [ ] **クリッピング** — カット面の手前側のみを表示範囲とする（bbox フィルターは粗い近似）

### 寸法線の拡張

- [ ] **非直交グリッドへの対応** — `DimensionGenerator` は現在 `|DirectionY| vs |DirectionX|` の閾値で方向を判定しており、斜め通り芯には非対応
- [ ] **カスタム寸法オフセット** — 現在はハードコードの `gap` 値

### 図面管理

- [ ] **断面/立面での通り芯バブル自動生成** — 現在は平面図のみ実装済み（断面/立面への crossing grid text はある）
- [ ] **図面縮尺の自動計算** — 現在は固定値

---

## 実装しない (Not Planned)

以下はスコープ外とする。要望があっても現在の設計では対応しない。

| 機能 | 理由 |
|------|------|
| **オブジェクト間の隠れ線処理 (Inter-Object Occlusion)** | 実装が複雑で正確性が担保しにくい。`ClassifyBrepEdges` の自己隠れのみ対応 |
| **リアルタイム更新** (モデル変更のたびに即時再生成) | Undo スタックとの干渉・パフォーマンス問題。手動 `TanukiUpdateAll` で十分 |
| **BIM データ連携** (IFC, Revit 等) | 別ツールの責務 |
| **クラウド同期** | `.3dm` ファイルへの埋め込み方式と相容れない |
| **マルチドキュメント対応** | `RhinoDoc.ActiveDoc` 前提の設計。複数ドキュメントの同時編集は対象外 |
| **自動発行 (AutoPublish)** | PDF 自動出力。Rhino の印刷APIが不安定なため `RhinoApp.RunScript("_Print")` に委譲する現状を維持 |
| **独自ファイルフォーマット** | `.3dm` 内 `doc.Strings` に JSON 保存する方式を維持 |
| **ユニットテスト自動化** | Rhino ランタイムへの依存が強く ROI が低い |

---

## バージョン履歴

| バージョン | 内容 |
|-----------|------|
| v0.1 | コア図面生成（平面/断面/立面）、通り芯・レベル設定、再生成方式 |
| v0.2 | CurveCleanup（重複線除去）、隠れ線品質改善（面法線分類・bbox フィルター） |
| v0.3 | DimensionGenerator（通り芯間寸法・レベル間寸法） |
| v0.4 | TanukiTitleBlock・TanukiPDF・シート出力パイプライン |
