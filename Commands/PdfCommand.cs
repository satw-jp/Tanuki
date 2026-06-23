using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;

namespace Tanuki.Commands
{
    /// <summary>
    /// 選択したレイアウトを印刷ダイアログで PDF 出力する
    /// </summary>
    public class TanukiPDF : Command
    {
        public static TanukiPDF Instance { get; private set; }
        public override string EnglishName => "TanukiPDF";
        public TanukiPDF() { Instance = this; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var pageViews = doc.Views.GetPageViews();
            if (pageViews == null || pageViews.Length == 0)
            {
                RhinoApp.WriteLine("[Tanuki] レイアウトがありません。TanukiSheet でレイアウトを作成してください。");
                return Result.Nothing;
            }

            // レイアウト選択
            var go = new GetOption();
            go.SetCommandPrompt("PDF に出力するレイアウトを選択");
            go.AddOption("全レイアウト");
            foreach (var pv in pageViews)
                go.AddOption(pv.PageName.Replace(" ", "_").Replace("-", "_").Replace(".", "_"));
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            int selIdx = go.Option().Index;

            if (selIdx == 1)
            {
                // 全レイアウト → 最初のレイアウトを選択（Rhinoが全ページを印刷できるように）
                doc.Views.ActiveView = pageViews[0];
                RhinoApp.WriteLine("[Tanuki] 全レイアウトをPDFに出力します。印刷ダイアログで「全レイアウト」を選択してください。");
            }
            else
            {
                var selectedView = pageViews[selIdx - 2];
                doc.Views.ActiveView = selectedView;
                RhinoApp.WriteLine($"[Tanuki] レイアウト '{selectedView.PageName}' を選択しました。");
            }

            // Rhino の印刷ダイアログを開く（出力先として PDF を選択）
            RhinoApp.RunScript("_Print", false);
            return Result.Success;
        }
    }
}
