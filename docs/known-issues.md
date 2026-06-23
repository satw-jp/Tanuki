# Tanuki — 既知の技術的課題

実際に確認または設計上認識されている制約・未解決問題。回避策があるものは併記する。

---

## KI-001: オブジェクト間の隠れ線処理が未実装

**現象**: 手前の壁の後ろにある柱・家具などは、断面図/立面図で「見え掛かり線」として表示される。実際には手前の壁で隠れているべきだが、隠れ線として分類されない。

**根本原因**: `LineClassifier.ClassifyBrepEdges` はオブジェクト単体の自己隠れ（面法線による分類）のみ実装している。複数オブジェクト間の相互遮蔽（投影後のポリゴンクリッピング）は未実装。

**技術的背景**: オブジェクト間の遮蔽を正確に計算するには、投影後の 2D ポリゴン同士の差分（Sutherland–Hodgman アルゴリズム等）が必要で、実装コストが高い上に曲面では厳密に扱えない。

**回避策**:
- Rhino のシェーデッドビューで視覚確認し、必要な隠れ線を手動で追加する
- 隠れ線レイヤーを非表示にして見え掛かりのみ使用する（`TanukiPanel` の表示トグル）

**ステータス**: Not planned（`docs/roadmap.md` 参照）

---

## KI-002: Undo/Redo 後に LineObjectId が失効する

**現象**: Rhino の Undo を行うと、`GridLine.LineObjectId` が指すオブジェクトが再生成され、ID が変わる。その後に通り芯を移動しても `TryUpdateFromObject` が `LineObjectId` での検索で失敗することがある。

**根本原因**: Rhino の Undo はオブジェクトを新しい Guid で復元する。`LineObjectId` は Undo 前の Guid を保持しており、ポインタとして失効する。

**緩和策**: `GridLine.PersistentId` と Rhino UserString `"TanukiPersistentId"` の二重保存により、`LineObjectId` の失敗後に全オブジェクトスキャンでフォールバック検索する（`GridLineDrawer.TryUpdateFromObject` 参照）。これで多くのケースは回復する。

**残る問題**: Undo を複数回繰り返した後や、大量の通り芯がある場合にフォールバックスキャンが O(n) になる。

**ステータス**: Partial mitigation（完全な解決は未対応）

---

## KI-003: マルチドキュメント非対応

**現象**: Rhino に複数ドキュメントを同時に開いた場合、パネル・コマンドは `RhinoDoc.ActiveDoc` のみを参照する。非アクティブなドキュメントのデータは操作できない。

**根本原因**: 設計上の意図的な選択（シンプルさ優先）。`TanukiProject.Load(doc)` は `doc` を受け取るが、パネルのイベントハンドラが `RhinoDoc.ActiveDoc` を使うため、非アクティブドキュメントには作用しない。

**回避策**: 作業するドキュメントをアクティブにしてからコマンドを実行する。

**ステータス**: By design（`docs/roadmap.md` の「実装しない」参照）

---

## KI-004: レイヤー数の増加

**現象**: 図面数 × (断面線 + 見え掛かり + 隠れ線 + 寸法 + タイトル) のレイヤーが作成される。`OriginalLayer` モードでは元レイヤー分だけさらに増える。大きなプロジェクト（20 図面以上）では Rhino のレイヤーパネルが重くなる場合がある。

**根本原因**: Tanuki はレイヤーを名前空間として使用しており、レイヤー数は図面数に比例して増加する設計。

**回避策**:
- 不要な図面を削除する（`TanukiPanel` の削除ボタンで `DeleteViewLayers` が走る）
- `LineType` モードを使用する（`OriginalLayer` モードより少ない）

**ステータス**: Known limitation

---

## KI-005: `TanukiAutoLayout` の制限

**現象**: `TanukiAutoLayout` は通り芯の X/Y 最大・最小値から図面の推定サイズを計算して自動配置するが、モデルの実際の形状が通り芯を大幅にはみ出す場合に図面が重なることがある。

**根本原因**: 自動配置は通り芯バウンディングボックスを近似的な図面サイズとして使用しており、通り芯外の形状（オーバーハング・屋根形状など）を考慮しない。

**回避策**: `TanukiPlaceView` で手動オフセットを指定する。`HasPlacement = true` になると次回 `TanukiAutoLayout` でも上書きされない（TODO: 上書きの挙動を確認）。

**ステータス**: Known limitation

---

## KI-006: `DimensionGenerator` は直交グリッドのみ対応

**現象**: 斜め（45度など）の通り芯が含まれる場合、`DimensionGenerator.AddFloorPlanDimensions` がその通り芯を正しく寸法チェーンに組み込まない。

**根本原因**: 方向判定に `Math.Abs(dir.Y) > Math.Abs(dir.X)` という閾値を使用している。`0.7071...` 以上の Y 成分を持つ場合は N-S グリッドとして、そうでない場合は E-W グリッドとして分類するため、45度近辺では誤分類が起きる。

**回避策**: 平面図に含まれる通り芯を直交（南北・東西）に整理した場合のみ寸法自動生成を使用する。それ以外は Rhino のネイティブ寸法ツールを使う。

**ステータス**: Planned（`docs/roadmap.md` 参照）

---

## KI-007: `CurveCleanup.Process` が O(n²)

**現象**: 重複線除去はカーブ数 n に対して O(n²) の計算量。断面対象のオブジェクトが多い（目安: 投影後 1000 本超）場合に顕著に遅くなる。

**根本原因**: `IsSameSegment` を全ペアで比較している。始終点のハッシュ化や空間インデックスを使えば O(n log n) 程度に改善できるが、現状の規模では問題が顕在化していない。

**回避策**:
- 切断対象レイヤーをフィルタリングしてオブジェクト数を減らす
- `TanukiProperties` で断面対象を限定する（TODO: 現時点の Properties コマンドの実装を確認）

**ステータス**: Known limitation（大規模モデルで問題が起きた時点で対応）

---

## KI-008: PDF 出力は Rhino の印刷ダイアログに依存

**現象**: `TanukiPDF` コマンドは Rhino の `_Print` コマンドを呼び出すだけであり、PDF 出力設定（用紙サイズ・プリンター選択等）はダイアログで手動設定が必要。

**根本原因**: RhinoCommon の PrintInfo API は Rhino バージョン間で不安定であり、ヘッドレス PDF 出力が信頼性を持って動作しない。`RunScript("_Print")` で Rhino 標準ダイアログに委譲するのが最も安定した方法。

**回避策**: `TanukiPDF` でレイアウトを選択後、ダイアログで設定を行う。設定は Rhino 側でプリセット保存できる。

**ステータス**: By design（`docs/roadmap.md` の「AutoPublish: 実装しない」参照）

---

## KI-009: `Extrusion` オブジェクトの隠れ線精度

**現象**: Rhino の `Extrusion` オブジェクト（Solid な押し出し）は `ClassifyEdges` 内で `ToBrep()` で変換されてから処理される。変換により本来のエッジ情報が変わる場合があり、薄い壁などで余分な内部エッジが現れることがある。

**根本原因**: `Extrusion.ToBrep()` は軽量 Extrusion を完全な Brep に展開するため、論理的には同じだが数値的に微妙に異なるエッジが生成される場合がある。

**回避策**: 問題のある Extrusion を `ExplodeBlock` や `_ConvertExtrusion` で Brep に変換してからモデルを保存する。

**ステータス**: Known limitation

---

## KI-010: `doc.Strings` の JSON サイズ上限

**現象**: 通り芯数・図面数が非常に多い場合（目安: 数百件）、`doc.Strings["TanukiProject"]` の JSON 文字列が大きくなる。Rhino が `doc.Strings` に設定している上限（存在する場合）を超えると保存・読み込みが失敗する可能性がある。

**根本原因**: 現時点で上限の明確な値は未確認（TODO）。

**現状**: 一般的な建築プロジェクト（通り芯 10〜50 本、図面 10〜30 枚）では問題が発生していない。

**ステータス**: Unconfirmed / Low priority
