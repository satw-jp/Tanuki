# Tanuki — コーディング規約

> **AI・外部コントリビューター向け重要ガイドライン**  
> このプロジェクトはアーキテクチャに強い意図がある。以下の規約に違反する変更は、たとえ技術的に動作しても **禁止** とする。

---

## 1. アーキテクチャ規則

### A-1. 層間の依存は一方向のみ

```
UI → Command → Generator → Data
```

Generator が UI を参照してはならない。Data が Generator を参照してはならない。

**禁止:**
```csharp
// Generator から UI へのアクセス
TanukiPanel.Instance.RefreshViewList();       // NG
Rhino.UI.Panels.OpenPanel(TanukiPanel.PanelId); // NG
```

**正しい:**
```csharp
// Generator はイベント発火のみ。パネルが自分でリフレッシュする
TanukiPlugin.RaiseViewsChanged();
```

### A-2. 再生成方式を変えない

図面生成は **全削除 → 全再生成** 方式を採用する。差分更新の導入は禁止。

`DrawingPlacer.DeleteViewLayers()` を呼ばずに `Place()` を呼ぶコードを書かない。

**禁止:**
```csharp
// 「高速化」のために既存オブジェクトを更新しようとするコード
var existingCurve = FindExistingCurve(layerIdx, ...); // NG
existingCurve.SetUserString(...);                     // NG
```

**正しい:**
```csharp
DrawingPlacer.DeleteViewLayers(doc, view.GetLayerKey());
ViewGenerator.Generate(doc, view, project);
```

### A-3. Command クラスに図形処理ロジックを書かない

Command クラス（`Tanuki.Commands.*`）は UI からの入力収集と Generator 呼び出しのみ行う。図形の計算・レイヤー操作・データ変換をコマンド内に書いてはならない。

**禁止:**
```csharp
// AddViewCommand の RunCommand 内でカーブを計算するコード
var curves = LineClassifier.Classify(...);   // NG — ここは Generator の責務
var layer = doc.Layers.Find(...);            // NG
```

**正しい:**
```csharp
ViewGenerator.Generate(doc, view, project); // NG ではなく委譲する
```

### A-4. `TanukiPlugin` はグルーコードのみ

`TanukiPlugin.cs` に業務ロジックを書かない。イベント登録・委譲・静的イベント発火のみ許容する。

---

## 2. 永続化規則

### P-1. 永続化は `doc.Strings` のみ

外部ファイル・データベース・レジストリ・環境変数に書き込まない。データは必ず `TanukiProject.Save(doc)` 経由で `doc.Strings["TanukiProject"]` に保存する。

**禁止:**
```csharp
File.WriteAllText("tanuki.json", json);       // NG
Registry.SetValue(...);                       // NG
Environment.SetEnvironmentVariable(...);      // NG
```

### P-2. データモデルの変更には SchemaVersion を上げる

`TanukiProject`・`GridLine`・`Level`・`ViewDef` に **新しいフィールドを追加する** 場合は必ず:

1. `TanukiProject.SchemaVersion` を +1 する
2. `TanukiProject.Migrate()` に補完ロジックを追加する（旧バージョンのデシリアライズが壊れない）

**禁止:**
```csharp
// SchemaVersion を上げずに新フィールドを追加する
public string NewField { get; set; }   // NG（マイグレーションなし）
```

### P-3. `LayerKey` は作成時に固定

`ViewDef.LayerKey` は図面作成時に `Name` から派生して固定する。以後変更しない。Name のリネームは `LayerKey` に影響しない。

```csharp
// ViewDef 作成時:
LayerKey = name.Replace("::", "_");  // ← これ以後変更しない
// リネーム後:
view.Name = newName;   // OK
// view.LayerKey = ...  // NG — レイヤーが孤立する
```

---

## 3. Rhino 規則

### R-1. ユーザー入力は必ずサニタイズしてからレイヤーパスに使う

```csharp
// 全ユーザー入力文字列に必須
string safe = userInput.Replace("::", "_");
```

`::` は Rhino のレイヤーパスセパレータ。サニタイズなしで使うと意図しないレイヤーを参照・破壊する。

**禁止:**
```csharp
string layerPath = $"Tanuki::{userInputViewName}";         // NG
doc.Layers.FindByFullPath(layerPath, ...);                  // NG（サニタイズなし）
```

### R-2. Rhino オブジェクトの削除はレイヤー単位で行う

個々の Guid を追跡してオブジェクトを削除しようとしない。レイヤーごと `DrawingPlacer.DeleteViewLayers()` で一括削除する。

```csharp
// 正しい削除方法
DrawingPlacer.DeleteViewLayers(doc, view.GetLayerKey());
```

### R-3. `RhinoApp.RunScript` は UI スレッドから呼ぶ

コールバック・イベントハンドラ内から直接 `RunScript` を呼ばない。

```csharp
// 正しい
RhinoApp.InvokeOnUiThread(new System.Action(() => RhinoApp.RunScript("...", false)));
```

### R-4. `doc.Objects.AddXxx` の戻り値は捨てない

`AddLine` / `AddCurve` / `AddTextEntity` の戻り値 (`Guid`) は必要に応じて `GridLine.LineObjectId` 等に保存する。捨てると後でオブジェクトを追跡できなくなる。

---

## 4. 互換性規則

### C-1. `net48` では C# 7.3 の範囲内で書く

Tanuki は `net7.0;net48` のマルチターゲット。C# 8+ の機能のうち `net48` で使えないものがある。

| 禁止（net48 非対応） | 代替 |
|---------------------|------|
| `new()` ターゲット型推論 | `new ClassName()` と明示 |
| `using var` | `var` または `using (var x = ...) { }` |
| インデックス演算子 `^1` | `list[list.Count - 1]` |
| Switch 式 | Switch ステートメント |
| `?? =` null 合体代入 | `if (x == null) x = ...` |

### C-2. `new System.Action(() => { })` でラムダをラップ

Eto.Forms / Rhino イベント購読でコンパイルエラーになる場合はラムダを明示的にラップする。

```csharp
RhinoApp.InvokeOnUiThread(new System.Action(() => { ... }));
```

### C-3. `System.Text.Json` のみを使う

`Newtonsoft.Json` を追加しない。`System.Text.Json` で対応できない複雑なシリアライズが必要になった場合は、データモデルを単純化することを検討する。

---

## 5. UI 規則

### U-1. パネルのボタンはコマンドを呼ぶだけ

パネルのイベントハンドラは `RhinoApp.RunScript` または `RhinoApp.InvokeOnUiThread` で Tanuki コマンドを呼ぶだけにする。パネル内に図形生成・データ変換ロジックを書かない（例外: `RefreshViewList`・`ZoomToViewLayer` 等 UI 状態管理は許容）。

### U-2. 全コマンドに `Tnk*` エイリアスを用意する

新しい `Tanuki*` コマンドを追加したら、同時に `AliasCommands.cs` に対応する `Tnk*` 1 行クラスを追加する。

```csharp
// AliasCommands.cs に 1 行追加:
public class TnkNewCommand : Command { ... RunScript("TanukiNewCommand", ...) }
```

### U-3. パネルは `TanukiPanel.cs` に新しいボタンを追加してから追加する

UIは `TanukiPanel` → `TanukiGridPanel` / `TanukiLevelPanel` / `TanukiSectionPanel` の階層になっている。メイン機能はツールバーに追加する（グループ分けを維持）。

---

## 6. パフォーマンス規則

### PF-1. `CurveCleanup` は `DrawingPlacer.Place` 内で呼ぶ

重複除去は `DrawingPlacer.Place()` の先頭で `CurveCleanup.Process()` が呼ばれる。呼び出し元（ViewGenerator）からさらに呼ばない。二重除去は無害だが無駄。

### PF-2. `doc.Objects.Find` をループ内で呼ばない

`doc.Objects.Find(guid)` は O(n) 検索。ループ内での呼び出しは O(n²) になる。代わりにレイヤー名でフィルタするか、`GridLine.LineObjectId` で事前に引いておく。

### PF-3. `CurveCleanup.Process` は O(n²) — 入力を制限する

現在の重複除去は O(n²) なので、大量オブジェクト（目安: 1000 本超）を持つモデルでは遅くなる。意図的にソートやハッシュ化で改善する前に、まず入力をフィルタリングして減らすことを検討する。

---

## 7. 禁止変更リスト

以下の変更は、それが「改善」に見えても行ってはならない。背景を理解した上での意図的な決定であるため。

| 禁止事項 | 理由 |
|---------|------|
| `doc.Strings` から外部ファイルへの永続化移行 | `.3dm` 1 ファイルで完結する設計が前提 |
| 差分更新の導入（オブジェクト ID 追跡） | Rhino の Undo と干渉し状態不整合を起こす |
| `ViewDef.LayerKey` のリネーム許可 | Rhino レイヤーが孤立する |
| Generator クラスから Eto.Forms への依存追加 | 層間依存が逆転する |
| `System.Text.Json` → `Newtonsoft.Json` 置換 | 依存追加は不要、複雑化を避ける |
| コマンドクラスへの図形計算ロジック追加 | Command は薄いグルーであるべき |
| ユーザー入力のレイヤーパスへの直接使用 | `::` インジェクションによるレイヤー破壊 |
| `InterObjectOcclusion` の簡易実装追加 | 不完全な実装は誤った隠れ線を生む。known-issues.md 参照 |
| `TanukiPanel` へのビジネスロジック追加 | テスト・再利用不可能なコードになる |
| C# 8+ 機能の net48 対象コードへの使用 | `net48` ビルドが壊れる |

---

## 8. テスト方針

現時点でユニットテストプロジェクトは存在しない（`Tanuki.csproj` 参照）。

Rhino ランタイムへの強い依存があるため、ユニットテストより以下を優先する:

1. `dotnet build` でビルドエラーがないこと
2. Rhino 8 で実際にコマンドを実行して動作確認すること
3. 隠れ線・重複除去の結果を目視確認すること

テストを追加する場合は `Tanuki.Tests` プロジェクトを別に作成する。既存 `.csproj` にテストを混入しない。

---

## 9. コメントポリシー

**デフォルトではコメントを書かない。**

書いてよいコメント:
- 隠れた制約（なぜその値なのか）
- 微妙な不変条件（面法線の符号反転の理由など）
- 特定バグへのワークアラウンド

書いてはならないコメント:
- コードが何をしているかの説明（メソッド名・変数名で伝わるべき）
- 呼び出し元を示す「〜から使用される」系コメント
- TODO・FIXME（DESIGN.md か known-issues.md に書く）
