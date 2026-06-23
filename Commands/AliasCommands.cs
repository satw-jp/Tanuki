using System;
using System.IO;
using System.Reflection;
using Rhino;
using Rhino.Commands;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
    public class TanukiVersion : Command
    {
        public static TanukiVersion Instance { get; private set; }
        public override string EnglishName => "TanukiVersion";
        public TanukiVersion() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            RhinoApp.WriteLine($"Tanuki v{ver.Major}.{ver.Minor}.{ver.Build}");
            return Result.Success;
        }
    }
    // ── tnk 短縮コマンド群 ─────────────────────────────────────────────────
    // 各クラスは対応する Tanuki* コマンドをそのまま委譲する

    public class TnkSetupGrid   : Command { public static TnkSetupGrid   Instance { get; private set; } public override string EnglishName => "TnkSetupGrid";   public TnkSetupGrid()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiSetupGrid",   false) ? Result.Success : Result.Failure; }
    public class TnkSetupLevel  : Command { public static TnkSetupLevel  Instance { get; private set; } public override string EnglishName => "TnkSetupLevel";  public TnkSetupLevel()  { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiSetupLevel",  false) ? Result.Success : Result.Failure; }
    public class TnkFloorPlan   : Command { public static TnkFloorPlan   Instance { get; private set; } public override string EnglishName => "TnkFloorPlan";   public TnkFloorPlan()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiFloorPlan",   false) ? Result.Success : Result.Failure; }
    public class TnkRCP         : Command { public static TnkRCP         Instance { get; private set; } public override string EnglishName => "TnkRCP";         public TnkRCP()         { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiRCP",         false) ? Result.Success : Result.Failure; }
    public class TnkSection     : Command { public static TnkSection     Instance { get; private set; } public override string EnglishName => "TnkSection";     public TnkSection()     { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiSection",     false) ? Result.Success : Result.Failure; }
    public class TnkElevation   : Command { public static TnkElevation   Instance { get; private set; } public override string EnglishName => "TnkElevation";   public TnkElevation()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiElevation",   false) ? Result.Success : Result.Failure; }
    public class TnkUpdateAll   : Command { public static TnkUpdateAll   Instance { get; private set; } public override string EnglishName => "TnkUpdateAll";   public TnkUpdateAll()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiUpdateAll",   false) ? Result.Success : Result.Failure; }
    public class TnkSheet       : Command { public static TnkSheet       Instance { get; private set; } public override string EnglishName => "TnkSheet";       public TnkSheet()       { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiSheet",       false) ? Result.Success : Result.Failure; }
    public class TnkPrint       : Command { public static TnkPrint       Instance { get; private set; } public override string EnglishName => "TnkPrint";       public TnkPrint()       { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiPrint",       false) ? Result.Success : Result.Failure; }
    public class TnkPlaceView   : Command { public static TnkPlaceView   Instance { get; private set; } public override string EnglishName => "TnkPlaceView";   public TnkPlaceView()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiPlaceView",   false) ? Result.Success : Result.Failure; }
    public class TnkProperties   : Command { public static TnkProperties   Instance { get; private set; } public override string EnglishName => "TnkProperties";   public TnkProperties()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiProperties",   false) ? Result.Success : Result.Failure; }
    public class TnkExport       : Command { public static TnkExport       Instance { get; private set; } public override string EnglishName => "TnkExport";       public TnkExport()       { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiExport",       false) ? Result.Success : Result.Failure; }
    public class TnkSectionPanel : Command { public static TnkSectionPanel Instance { get; private set; } public override string EnglishName => "TnkSectionPanel"; public TnkSectionPanel() { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiSectionPanel", false) ? Result.Success : Result.Failure; }
    public class TnkAutoLayout   : Command { public static TnkAutoLayout   Instance { get; private set; } public override string EnglishName => "TnkAutoLayout";   public TnkAutoLayout()   { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiAutoLayout",   false) ? Result.Success : Result.Failure; }
    public class TnkPDF         : Command { public static TnkPDF         Instance { get; private set; } public override string EnglishName => "TnkPDF";         public TnkPDF()         { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiPDF",         false) ? Result.Success : Result.Failure; }
    public class TnkTitleBlock  : Command { public static TnkTitleBlock  Instance { get; private set; } public override string EnglishName => "TnkTitleBlock";  public TnkTitleBlock()  { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiTitleBlock",  false) ? Result.Success : Result.Failure; }
    public class TnkVersion     : Command { public static TnkVersion     Instance { get; private set; } public override string EnglishName => "TnkVersion";     public TnkVersion()     { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiVersion",     false) ? Result.Success : Result.Failure; }

    /// <summary>
    /// 現在ファイルを保存してRhinoを終了する。dev-reload.ps1 と組み合わせて使う。
    /// </summary>
    public class TanukiDevReload : Command
    {
        public static TanukiDevReload Instance { get; private set; }
        public override string EnglishName => "TanukiDevReload";
        public TanukiDevReload() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 現在ファイルパスを一時ファイルに書き出す（dev-reload.ps1 が読む）
            string path = doc != null ? doc.Path : "";
            string tmp = Path.Combine(Path.GetTempPath(), "tanuki_reload.txt");
            File.WriteAllText(tmp, path);

            // 保存してから、コマンド終了後に終了
            RhinoApp.RunScript("_Save", false);
            System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
                RhinoApp.InvokeOnUiThread(new Action(() =>
                    RhinoApp.RunScript("_-Exit", false))));
            return Result.Success;
        }
    }
}
