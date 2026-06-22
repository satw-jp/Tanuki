using Rhino;
using Rhino.Commands;
using Tanuki.Data;
using Tanuki.Generators;

namespace Tanuki.Commands
{
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
    public class TnkProperties  : Command { public static TnkProperties  Instance { get; private set; } public override string EnglishName => "TnkProperties";  public TnkProperties()  { Instance = this; } protected override Result RunCommand(RhinoDoc doc, RunMode mode) => RhinoApp.RunScript("TanukiProperties",  false) ? Result.Success : Result.Failure; }
}
