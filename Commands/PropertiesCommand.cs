using System;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Tanuki.Data;

namespace Tanuki.Commands
{
    /// <summary>
    /// 選択中のオブジェクトに関連するTanukiプロパティをコマンドラインに表示する
    /// </summary>
    public class TanukiProperties : Command
    {
        public static TanukiProperties Instance { get; private set; }
        public override string EnglishName => "TanukiProperties";
        public TanukiProperties() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            var selected = doc.Objects.GetSelectedObjects(false, false);

            bool found = false;
            foreach (var obj in selected)
            {
                // マーカーオブジェクト（断面・立面の切断線）
                var view = project.Views.FirstOrDefault(v => v.MarkerObjectId == obj.Id);
                if (view != null)
                {
                    RhinoApp.WriteLine($"── Tanuki View Marker ──");
                    RhinoApp.WriteLine($"  Name  : {view.Name}");
                    RhinoApp.WriteLine($"  Type  : {view.Type}");
                    if (view.Type == ViewType.FloorPlan || view.Type == ViewType.RCP)
                        RhinoApp.WriteLine($"  Height: {view.CutHeight:F0} mm");
                    else
                        RhinoApp.WriteLine($"  Cut   : ({view.CutStartX:F0},{view.CutStartY:F0}) → ({view.CutEndX:F0},{view.CutEndY:F0})");
                    RhinoApp.WriteLine($"  Placed: ({view.PlacedOffsetX:F0}, {view.PlacedOffsetY:F0})");
                    found = true;
                    continue;
                }

                // 通り芯マーカーの検索（名前ベース）
                if (obj.Name != null && obj.Name.StartsWith("[Tanuki Grid]"))
                {
                    string gridName = obj.Name.Replace("[Tanuki Grid]", "").Trim();
                    var gl = project.GridLines.FirstOrDefault(g => g.Name == gridName);
                    if (gl != null)
                    {
                        RhinoApp.WriteLine($"── Tanuki Grid Line ──");
                        RhinoApp.WriteLine($"  Name  : {gl.Name}");
                        RhinoApp.WriteLine($"  Origin: ({gl.OriginX:F0}, {gl.OriginY:F0})");
                        RhinoApp.WriteLine($"  Dir   : ({gl.DirectionX:F3}, {gl.DirectionY:F3})");
                        RhinoApp.WriteLine($"  Length: {gl.Length:F0} mm");
                        found = true;
                        continue;
                    }
                }

                // 生成済み図面レイヤーのオブジェクト
                var layer = doc.Layers[obj.Attributes.LayerIndex];
                if (layer != null && layer.FullPath.StartsWith("Tanuki::"))
                {
                    var parts = layer.FullPath.Split(new[] { "::" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        string viewName = parts.Length > 1 ? parts[1] : "";
                        string lineType = parts.Length > 2 ? parts[2] : "";
                        var v = project.Views.FirstOrDefault(x => x.Name == viewName);
                        RhinoApp.WriteLine($"── Tanuki Generated Curve ──");
                        RhinoApp.WriteLine($"  View    : {viewName}");
                        RhinoApp.WriteLine($"  LineType: {lineType}");
                        if (v != null) RhinoApp.WriteLine($"  Source  : {v.Type}");
                        found = true;
                    }
                }
            }

            if (!found)
            {
                // 選択なし → プロジェクト全体のサマリー
                RhinoApp.WriteLine($"── Tanuki Project Summary ──");
                RhinoApp.WriteLine($"  Grid lines: {project.GridLines.Count}");
                foreach (var gl in project.GridLines)
                    RhinoApp.WriteLine($"    {gl.Name}  ({gl.OriginX:F0},{gl.OriginY:F0})");
                RhinoApp.WriteLine($"  Levels: {project.Levels.Count}");
                foreach (var l in project.Levels)
                    RhinoApp.WriteLine($"    {l.Name}  Z={l.Elevation:F0}mm");
                RhinoApp.WriteLine($"  Views: {project.Views.Count}");
                foreach (var v in project.Views)
                    RhinoApp.WriteLine($"    {v.Name}  [{v.Type}]");
                RhinoApp.WriteLine($"  Layer mode: {project.LayerMode}");
            }

            return Result.Success;
        }
    }
}
