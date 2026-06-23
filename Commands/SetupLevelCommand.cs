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
            go.AddOption("追加");
            go.AddOption("全削除");
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            if (go.Option().Index == 2)
            {
                project.Levels.Clear();
                project.Save(doc);
                return Result.Success;
            }

            var gn = new GetString();
            gn.SetCommandPrompt("レベル名 (例: 1FL)");
            gn.SetDefaultString("1FL");
            gn.Get();
            if (gn.CommandResult() != Result.Success) return gn.CommandResult();

            var gh = new GetNumber();
            gh.SetCommandPrompt($"'{gn.StringResult()}' の高さ (mm)");
            gh.SetDefaultNumber(0);
            gh.Get();
            if (gh.CommandResult() != Result.Success) return gh.CommandResult();

            project.Levels.Add(new Level { Name = gn.StringResult().Replace("::", "_"), Elevation = gh.Number() });
            project.Save(doc);
            return Result.Success;
        }
    }
}
