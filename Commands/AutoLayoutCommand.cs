using System;
using Rhino;
using Rhino.Commands;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
    /// <summary>
    /// 全図面を通り芯アライメントに最適化した位置に再配置する。
    /// 平面図: X=0 固定、Y 方向に積み重ね（通り芯が縦に揃う）
    /// 断面/立面: 平面図の右側、X 方向に並べる
    /// </summary>
    public class TanukiAutoLayout : Command
    {
        public static TanukiAutoLayout Instance { get; private set; }
        public override string EnglishName => "TanukiAutoLayout";
        public TanukiAutoLayout() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);
            if (project.Views.Count == 0)
            {
                RhinoApp.WriteLine("[Tanuki] 図面がありません。先に図面を生成してください。");
                return Result.Nothing;
            }

            var bbox = DrawingPlacer.GetModelBBox(doc);
            if (!bbox.IsValid)
            {
                RhinoApp.WriteLine("[Tanuki] モデルのバウンディングボックスが取得できません。");
                return Result.Failure;
            }

            double margin = 5000;
            double modelW = bbox.Max.X - bbox.Min.X;
            double modelH = bbox.Max.Y - bbox.Min.Y;

            // ── 平面図 / 天井伏図: X=0 固定、Y 方向に積み重ね ──────────────
            double fpY = bbox.Min.Y - modelH - margin;
            foreach (var view in project.Views)
            {
                if (view.Type != ViewType.FloorPlan && view.Type != ViewType.RCP) continue;
                view.PlacedOffsetX = 0;
                view.PlacedOffsetY = fpY;
                view.HasPlacement  = true;
                fpY -= (modelH + margin);
            }

            // ── 断面 / 立面: 平面図の右側、X 方向に並べる ─────────────────
            double secX = bbox.Max.X + margin;
            foreach (var view in project.Views)
            {
                if (view.Type != ViewType.Section && view.Type != ViewType.Elevation) continue;
                double cutLen = Math.Sqrt(
                    Math.Pow(view.CutEndX - view.CutStartX, 2) +
                    Math.Pow(view.CutEndY - view.CutStartY, 2));
                view.PlacedOffsetX = secX;
                view.PlacedOffsetY = 0;
                view.HasPlacement  = true;
                secX += Math.Max(cutLen, modelW) + margin;
            }

            project.Save(doc);
            ViewGenerator.GenerateAll(doc, project);

            RhinoApp.WriteLine($"[Tanuki] {project.Views.Count} 図面を通り芯基準で再配置しました。");
            return Result.Success;
        }
    }
}
