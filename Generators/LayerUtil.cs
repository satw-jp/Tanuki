using System.Drawing;
using Rhino;
using Rhino.DocObjects;

namespace Tanuki.Generators
{
    public static class LayerUtil
    {
        public static string Safe(string name) => name.Replace("::", "_");

        public static int GetOrCreate(RhinoDoc doc, string name, int parentIdx, Color color)
        {
            string fullPath = parentIdx < 0 ? name
                            : doc.Layers[parentIdx].FullPath + "::" + name;
            int idx = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (idx != RhinoMath.UnsetIntIndex) return idx;
            var layer = new Layer { Name = name, Color = color };
            if (parentIdx >= 0) layer.ParentLayerId = doc.Layers[parentIdx].Id;
            return doc.Layers.Add(layer);
        }

        public static void ForEachObject(RhinoDoc doc, int layerIdx, System.Action<Rhino.DocObjects.RhinoObject> action)
        {
            var objs = doc.Objects.FindByLayer(doc.Layers[layerIdx]);
            if (objs != null) foreach (var o in objs) action(o);
            var children = doc.Layers[layerIdx].GetChildren();
            if (children != null)
                foreach (var child in children)
                    ForEachObject(doc, child.Index, action);
        }
    }
}
