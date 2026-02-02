using System.Linq;
using Pinta.Core;
using Gtk;

namespace Pinta.Gui.Widgets;

public sealed class ToolBoxWidget : Gtk.Grid
{
    private readonly ToolManager tools;
    private int nextIndex = 0; // tracks insertion order
    private const int Columns = 2;

    public ToolBoxWidget(ToolManager tools)
    {
        this.tools = tools;

        ColumnSpacing = 2;
        RowSpacing = 2;
        MarginStart = 3;
        MarginEnd = 3;
        // optional: align top-left
        Halign = Align.Center;
        Valign = Align.Start;
    }

    public void AddItem(BaseTool tool)
    {
        int row = nextIndex / Columns;
        int col = nextIndex % Columns;

        Attach(tool.ToolItem, col, row, 1, 1);

        // optional: force square buttons
        tool.ToolItem.WidthRequest = tool.ToolItem.HeightRequest;

        nextIndex++;

        Show();
    }

    public void RemoveItem(BaseTool tool)
    {
        Remove(tool.ToolItem);
    }
}
