using Rhino;
using Rhino.Commands;
using Tanuki.UI;

namespace Tanuki.Commands
{
    public class TanukiPanel_Show : Command
    {
        public static TanukiPanel_Show Instance { get; private set; }
        public override string EnglishName => "TanukiPanel";
        public TanukiPanel_Show() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Rhino.UI.Panels.OpenPanel(TanukiPanel.PanelId);
            return Result.Success;
        }
    }

    public class TanukiGridPanel_Show : Command
    {
        public static TanukiGridPanel_Show Instance { get; private set; }
        public override string EnglishName => "TanukiGridPanel";
        public TanukiGridPanel_Show() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Rhino.UI.Panels.OpenPanel(TanukiGridPanel.PanelId);
            return Result.Success;
        }
    }

    public class TanukiLevelPanel_Show : Command
    {
        public static TanukiLevelPanel_Show Instance { get; private set; }
        public override string EnglishName => "TanukiLevelPanel";
        public TanukiLevelPanel_Show() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Rhino.UI.Panels.OpenPanel(TanukiLevelPanel.PanelId);
            return Result.Success;
        }
    }

    public class TanukiSectionPanel_Show : Command
    {
        public static TanukiSectionPanel_Show Instance { get; private set; }
        public override string EnglishName => "TanukiSectionPanel";
        public TanukiSectionPanel_Show() { Instance = this; }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Rhino.UI.Panels.OpenPanel(TanukiSectionPanel.PanelId);
            return Result.Success;
        }
    }
}
