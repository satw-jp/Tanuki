using System;
using Rhino;
using Rhino.Geometry;
using Tanuki.Data;

namespace Tanuki.Generators
{
    /// <summary>
    /// ViewDefから図面を生成してRhinoに配置する
    /// </summary>
    public static class ViewGenerator
    {
        public static void Generate(RhinoDoc doc, ViewDef view, TanukiProject project, bool replaceExisting = true)
        {
            var offset = GetOffset(doc, view);

            switch (view.Type)
            {
                case ViewType.FloorPlan:
                case ViewType.RCP:
                    GenerateFloorPlan(doc, view, project, offset, replaceExisting);
                    break;
                case ViewType.Elevation:
                case ViewType.Section:
                    GenerateSectionOrElevation(doc, view, project, offset, replaceExisting);
                    break;
            }
        }

        public static void GenerateAll(RhinoDoc doc, TanukiProject project)
        {
            foreach (var view in project.Views)
                Generate(doc, view, project, replaceExisting: true);
        }

        // ---- Floor Plan / RCP ----

        private static void GenerateFloorPlan(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            bool reflected = view.Type == ViewType.RCP;
            var curves = LineClassifier.ClassifyFloorPlan(doc, view.CutHeight, reflected);

            // 通り芯を追加
            foreach (var gl in project.GridLines)
                curves.Add(new ClassifiedCurve
                {
                    Curve = gl.ToLine().ToNurbsCurve(),
                    LineType = LineType.Visible,
                    SourceLayerIndex = 0
                });

            DrawingPlacer.Place(doc, view.Name, curves, offset, project.LayerMode, replace);
        }

        // ---- Section / Elevation ----

        private static void GenerateSectionOrElevation(RhinoDoc doc, ViewDef view, TanukiProject project, Transform offset, bool replace)
        {
            var cutPlane   = view.GetCutPlane();
            var viewDir    = view.GetViewDirection();
            var curves     = LineClassifier.Classify(doc, cutPlane, viewDir);

            DrawingPlacer.Place(doc, view.Name, curves, offset, project.LayerMode, replace);
        }

        // ---- Layout Offset ----

        private static Transform GetOffset(RhinoDoc doc, ViewDef view)
        {
            // 保存済みオフセットがあればそれを使う
            if (view.PlacedOffsetX != 0 || view.PlacedOffsetY != 0)
                return Transform.Translation(new Vector3d(view.PlacedOffsetX, view.PlacedOffsetY, 0));

            // なければBBoxの下に自動配置
            var bbox = DrawingPlacer.GetModelBBox(doc);
            if (!bbox.IsValid) return Transform.Identity;

            double margin = 5000;
            double h = bbox.Max.Y - bbox.Min.Y;
            var v = new Vector3d(0, bbox.Min.Y - h - margin, -bbox.Min.Z);

            view.PlacedOffsetX = v.X;
            view.PlacedOffsetY = v.Y;

            return Transform.Translation(v);
        }
    }
}
