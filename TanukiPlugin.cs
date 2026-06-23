using Rhino;
using Rhino.PlugIns;
using Tanuki.Data;
using Tanuki.Generators;
using Tanuki.UI;

namespace Tanuki
{
    public class TanukiPlugin : PlugIn
    {
        public static TanukiPlugin Instance { get; private set; }
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        public static event System.EventHandler GridLinesChanged;
        public static event System.EventHandler ViewsChanged;
        public static event System.EventHandler<System.Guid> MarkerObjectSelected;
        public static event System.EventHandler<System.Guid> GridLineObjectSelected;

        internal static void RaiseGridLinesChanged()
            => GridLinesChanged?.Invoke(null, System.EventArgs.Empty);

        internal static void RaiseViewsChanged()
            => ViewsChanged?.Invoke(null, System.EventArgs.Empty);

        internal static void RaiseMarkerObjectSelected(System.Guid id)
            => MarkerObjectSelected?.Invoke(null, id);

        internal static void RaiseGridLineObjectSelected(System.Guid id)
            => GridLineObjectSelected?.Invoke(null, id);

        public TanukiPlugin()
        {
            Instance = this;
        }

        private void TryRegister(System.Type panelType, string name)
        {
            try
            {
                Rhino.UI.Panels.RegisterPanel(this, panelType, name, (System.Drawing.Icon)null);
            }
            catch (System.Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] パネル登録失敗 {name}: {ex.Message}");
            }
        }

        private void OnObjectReplaced(object sender, Rhino.DocObjects.RhinoReplaceObjectEventArgs args)
        {
            try { OnObjectReplacedCore(args); } catch { }
        }

        private void OnObjectReplacedCore(Rhino.DocObjects.RhinoReplaceObjectEventArgs args)
        {
            var doc = args.Document;
            if (doc == null) return;
            var project = TanukiProject.Load(doc);
            var oldId   = args.OldRhinoObject.Id;
            var newId   = args.NewRhinoObject.Id;

            // ── 断面/立面マーカーの移動追従 ──
            foreach (var view in project.Views)
            {
                if (view.MarkerObjectId == System.Guid.Empty) continue;
                if (view.MarkerObjectId != oldId) continue;

                if (args.NewRhinoObject.Geometry is Rhino.Geometry.Curve curve)
                {
                    var oldIndicatorIds = new System.Collections.Generic.List<System.Guid>(
                        view.MarkerIndicatorIds ?? new System.Collections.Generic.List<System.Guid>());
                    view.CutStartX      = curve.PointAtStart.X;
                    view.CutStartY      = curve.PointAtStart.Y;
                    view.CutEndX        = curve.PointAtEnd.X;
                    view.CutEndY        = curve.PointAtEnd.Y;
                    view.MarkerObjectId = newId;
                    var newLine    = new Rhino.Geometry.Line(curve.PointAtStart, curve.PointAtEnd);
                    bool viewRight = view.ViewRight;
                    string vName   = view.Name;
                    var v = view;
                    var p = project;
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                    {
                        MarkerDrawer.DeleteIndicators(doc, oldIndicatorIds);
                        int layerIdx = MarkerDrawer.EnsureMarkersLayer(doc);
                        v.MarkerIndicatorIds = MarkerDrawer.DrawIndicators(doc, newLine, vName, viewRight, layerIdx, System.Drawing.Color.Magenta);
                        p.Save(doc);
                        ViewGenerator.Generate(doc, v, p);
                        RaiseViewsChanged();
                    }));
                }
                return;
            }

            // ── 通り芯の移動追従 ──
            if (args.NewRhinoObject.Geometry is Rhino.Geometry.Curve gridCurve)
            {
                bool updated = Tanuki.Generators.GridLineDrawer.TryUpdateFromObject(
                    doc, oldId, newId, gridCurve, project.GridLines);

                if (updated)
                {
                    project.Save(doc);
                    var snapProject = project;
                    RhinoApp.InvokeOnUiThread(new System.Action(() =>
                    {
                        Tanuki.Generators.GridLineDrawer.SyncSymbols(doc, snapProject.GridLines, snapProject.BubbleRadius);
                        foreach (var v in snapProject.Views)
                        {
                            if (v.Type == Data.ViewType.FloorPlan || v.Type == Data.ViewType.RCP)
                                Generators.ViewGenerator.Generate(doc, v, snapProject);
                        }
                        RaiseGridLinesChanged();
                    }));
                }
            }
        }

        private void OnRhinoSelectObjects(object sender, Rhino.DocObjects.RhinoObjectSelectionEventArgs e)
        {
            if (!e.Selected) return;
            var doc = e.Document;
            if (doc == null) return;
            try
            {
                var project = TanukiProject.Load(doc);
                foreach (var obj in e.RhinoObjects)
                {
                    var id = obj.Id;
                    foreach (var gl in project.GridLines)
                        if (gl.LineObjectId == id) { RaiseGridLineObjectSelected(id); return; }
                    foreach (var view in project.Views)
                        if (view.MarkerObjectId == id) { RaiseMarkerObjectSelected(id); return; }
                }
            }
            catch { }
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            TryRegister(typeof(TanukiPanel),        "Tanuki");
            TryRegister(typeof(TanukiGridPanel),    "T:Grid");
            TryRegister(typeof(TanukiLevelPanel),   "T:Level");
            TryRegister(typeof(TanukiSectionPanel), "T:Views");
            RhinoDoc.ReplaceRhinoObject += OnObjectReplaced;
            RhinoDoc.SelectObjects      += OnRhinoSelectObjects;
            RhinoApp.Initialized        += OnRhinoInitialized;
            return LoadReturnCode.Success;
        }

        private bool _toolbarInstalled = false;

        private void OnRhinoInitialized(object sender, System.EventArgs e)
        {
            RhinoApp.Initialized -= OnRhinoInitialized;
            RhinoApp.WriteLine("[Tanuki] OnRhinoInitialized: パネルをオープン中");
            Rhino.UI.Panels.OpenPanel(TanukiPanel.PanelId);
            Rhino.UI.Panels.OpenPanel(TanukiGridPanel.PanelId);
            Rhino.UI.Panels.OpenPanel(TanukiLevelPanel.PanelId);
            Rhino.UI.Panels.OpenPanel(TanukiSectionPanel.PanelId);
            RhinoApp.WriteLine("[Tanuki] OnRhinoInitialized: Idle イベントを登録");
            RhinoApp.Idle += OnFirstIdle;
        }

        private void OnFirstIdle(object sender, System.EventArgs e)
        {
            if (_toolbarInstalled) { RhinoApp.Idle -= OnFirstIdle; return; }
            _toolbarInstalled = true;
            RhinoApp.Idle -= OnFirstIdle;
            RhinoApp.WriteLine("[Tanuki] OnFirstIdle: ツールバーインストール開始");
            InstallToolbar();
        }

        private void InstallToolbar()
        {
            try
            {
                // ① 埋め込みリソースを確認
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var names = assembly.GetManifestResourceNames();
                RhinoApp.WriteLine($"[Tanuki] 埋め込みリソース一覧 ({names.Length} 件):");
                foreach (var n in names) RhinoApp.WriteLine($"  {n}");

                using (var stream = assembly.GetManifestResourceStream("Tanuki.EmbeddedResources.Tanuki.rui"))
                {
                    if (stream == null)
                    {
                        RhinoApp.WriteLine("[Tanuki] ❌ Tanuki.EmbeddedResources.Tanuki.rui が見つかりません");
                        return;
                    }
                    RhinoApp.WriteLine($"[Tanuki] ✓ リソースを取得 (size={stream.Length} bytes)");

                    // ② 書き込み先を確認
                    string uiDir  = GetRhinoUiDir();
                    string ruiPath = System.IO.Path.Combine(uiDir, "Tanuki.rui");
                    RhinoApp.WriteLine($"[Tanuki] UIDir: {uiDir}");
                    RhinoApp.WriteLine($"[Tanuki] ruiPath: {ruiPath}");

                    System.IO.Directory.CreateDirectory(uiDir);
                    RhinoApp.WriteLine($"[Tanuki] ✓ ディレクトリ確認完了");

                    // ③ ファイルを書き込む
                    using (var fileStream = System.IO.File.Create(ruiPath))
                        stream.CopyTo(fileStream);
                    long written = new System.IO.FileInfo(ruiPath).Length;
                    RhinoApp.WriteLine($"[Tanuki] ✓ .rui ファイル書き込み完了 ({written} bytes): {ruiPath}");

                    // ④ RunScript でロード
                    string cmd = $"_-Toolbar _File _Open \"{ruiPath}\" _Enter";
                    RhinoApp.WriteLine($"[Tanuki] RunScript: {cmd}");
                    bool ok = RhinoApp.RunScript(cmd, false);
                    RhinoApp.WriteLine($"[Tanuki] RunScript 結果: {(ok ? "✓ 成功" : "❌ 失敗")}");

                    if (!ok)
                    {
                        RhinoApp.WriteLine(
                            "[Tanuki] 手動手順: オプション > 外観 > ツールバー > ファイル > 開く で " +
                            ruiPath + " を選択してください");
                    }
                }
            }
            catch (System.Exception ex)
            {
                RhinoApp.WriteLine($"[Tanuki] ❌ InstallToolbar 例外: {ex.GetType().Name}: {ex.Message}");
                RhinoApp.WriteLine($"[Tanuki] StackTrace: {ex.StackTrace}");
            }
        }

        private static string GetRhinoUiDir()
        {
            if (Rhino.Runtime.HostUtils.RunningOnWindows)
            {
                return System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "McNeel", "Rhinoceros", "8.0", "UI");
            }
            else
            {
                return System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                    "Library", "Application Support", "McNeel", "Rhinoceros", "8", "UI");
            }
        }
    }
}
