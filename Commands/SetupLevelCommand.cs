using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Tanuki.Data;

namespace Tanuki.Commands
{
    public class TanukiSetupLevel : Command
    {
        public static TanukiSetupLevel Instance { get; private set; }
        public override string EnglishName => "TanukiSetupLevel";
        public TanukiSetupLevel() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var project = TanukiProject.Load(doc);

            var go = new GetOption();
            go.SetCommandPrompt("レベルの操作を選択");
            int addIdx   = go.AddOption("追加");
            int listIdx  = go.AddOption("一覧");
            int clearIdx = go.AddOption("全削除");
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            if (go.Option().Index == listIdx)
            {
                if (project.Levels.Count == 0) { RhinoApp.WriteLine("レベルなし"); return Result.Success; }
                foreach (var l in project.Levels)
                    RhinoApp.WriteLine($"  {l.Name}  Z={l.Elevation:F0}mm");
                return Result.Success;
            }

            if (go.Option().Index == clearIdx)
            {
                project.Levels.Clear();
                project.Save(doc);
                RhinoApp.WriteLine("レベルをすべて削除しました");
                return Result.Success;
            }

            // 追加
            var gn = new GetString();
            gn.SetCommandPrompt("レベル名 (例: GL, 1FL, 2FL, RF)");
            gn.SetDefaultString("1FL");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();

            var gh = new GetNumber();
            gh.SetCommandPrompt($"'{gn.StringResult()}' の高さ (mm)");
            gh.SetDefaultNumber(0);
            gh.Get();
            if (gh.CommandResult() != Result.Success) return gh.CommandResult();

            project.Levels.Add(new Level { Name = gn.StringResult(), Elevation = gh.Number() });
            project.Save(doc);
            RhinoApp.WriteLine($"レベル '{gn.StringResult()}' ({gh.Number():F0}mm) を追加しました");
            return Result.Success;
        }
    }
}
