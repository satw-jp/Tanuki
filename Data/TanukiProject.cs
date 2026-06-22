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

        public List<GridLine>    GridLines  { get; set; } = new List<GridLine>();
        public List<Level>       Levels     { get; set; } = new List<Level>();
        public List<ViewDef>     Views      { get; set; } = new List<ViewDef>();
        public LayerMode         LayerMode  { get; set; } = LayerMode.LineType;

        // ---- 永続化 ----

        public static TanukiProject Load(RhinoDoc doc)
        {
            var json = doc.Strings.GetValue(DocKey);
            if (string.IsNullOrEmpty(json)) return new TanukiProject();
            try { return JsonSerializer.Deserialize<TanukiProject>(json) ?? new TanukiProject(); }
            catch { return new TanukiProject(); }
        }

        public void Save(RhinoDoc doc)
        {
            doc.Strings.SetString(DocKey, JsonSerializer.Serialize(this));
        }
    }

    public enum LayerMode { LineType, OriginalLayer }
}
