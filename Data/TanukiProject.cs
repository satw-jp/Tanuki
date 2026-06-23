using System;
using System.Collections.Generic;
using System.Text.Json;
using Rhino;

namespace Tanuki.Data
{
    /// <summary>
    /// プロジェクト全体のデータをdoc.Stringsに永続化する
    /// </summary>
    public class TanukiProject
    {
        private const string DocKey = "TanukiProject";
        private const int    CurrentSchemaVersion = 4;

        public int               SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<GridLine>    GridLines    { get; set; } = new List<GridLine>();
        public List<Level>       Levels       { get; set; } = new List<Level>();
        public List<ViewDef>     Views        { get; set; } = new List<ViewDef>();
        public LayerMode         LayerMode    { get; set; } = LayerMode.LineType;
        public double            BubbleRadius    { get; set; } = 400;
        public double            LabelTextHeight { get; set; } = 500;
        public int               ViewScale       { get; set; } = 100;
        // 断面/立面パフォーマンス既定（新規ビューが継承。既存プロジェクトは欠損時 true/0 = 従来動作）
        public bool              DefaultIncludeMeshes { get; set; } = true;
        public double            DefaultViewDepth     { get; set; } = 0;
        public ViewDisplayMode   DefaultDisplayMode       { get; set; } = ViewDisplayMode.Technical;
        public PresentationStyle DefaultPresentationStyle { get; set; } = PresentationStyle.SolidColor;
        // C-1 Sheet システム（UI未実装。データ定義のみ）
        public List<SheetDef>    Sheets                   { get; set; } = new List<SheetDef>();

        // ---- 永続化 ----

        public static TanukiProject Load(RhinoDoc doc)
        {
            var json = doc.Strings.GetValue(DocKey);
            if (string.IsNullOrEmpty(json)) return new TanukiProject();
            try
            {
                var p = JsonSerializer.Deserialize<TanukiProject>(json) ?? new TanukiProject();
                Migrate(p);
                return p;
            }
            catch { return new TanukiProject(); }
        }

        public void Save(RhinoDoc doc)
        {
            doc.Strings.SetString(DocKey, JsonSerializer.Serialize(this));
        }

        // ---- スキーマ移行 ----

        private static void Migrate(TanukiProject p)
        {
            // v1 → v2: GridLine に PersistentId、ViewDef に LayerKey を追加
            if (p.SchemaVersion < 2)
            {
                foreach (var gl in p.GridLines)
                    if (gl.PersistentId == Guid.Empty)
                        gl.PersistentId = Guid.NewGuid();

                foreach (var v in p.Views)
                    if (string.IsNullOrEmpty(v.LayerKey))
                        v.LayerKey = v.Name.Replace("::", "_");
            }

            // v2 → v3: DisplayMode / PresentationStyle を追加
            if (p.SchemaVersion < 3)
            {
            }

            // v3 → v4: LayerMode と LabelTextHeight をビュー単位に移行
            if (p.SchemaVersion < 4)
            {
                foreach (var v in p.Views)
                {
                    v.LayerMode       = p.LayerMode;
                    v.LabelTextHeight = p.LabelTextHeight;
                }
            }

            p.SchemaVersion = CurrentSchemaVersion;
        }
    }

    public enum LayerMode { LineType, OriginalLayer }
}
