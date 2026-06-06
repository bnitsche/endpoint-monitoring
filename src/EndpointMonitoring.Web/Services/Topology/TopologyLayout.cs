using System.Globalization;

namespace EndpointMonitoring.Web.Services.Topology;

/// <summary>
/// Deterministic layered layout: nodes are grouped by column, stacked vertically and
/// each column is centered against the tallest column. All coordinates are in SVG user units.
/// </summary>
public static class TopologyLayout
{
    /// <summary>Node box width.</summary>
    public const double NodeW = 200;

    /// <summary>Node box height.</summary>
    public const double NodeH = 56;

    /// <summary>Horizontal distance between column left edges.</summary>
    public const double ColGap = 280;

    /// <summary>Vertical distance between stacked node slots.</summary>
    public const double RowPitch = 84;

    /// <summary>Horizontal canvas margin.</summary>
    public const double MarginX = 40;

    /// <summary>Vertical canvas margin.</summary>
    public const double MarginY = 40;

    /// <summary>Assigns X/Y to every node and computes the model's Width/Height.</summary>
    public static void Apply(TopologyModel model)
    {
        var columns = model.Nodes.GroupBy(n => n.Column).OrderBy(g => g.Key).ToList();
        var maxRows = columns.Count == 0 ? 0 : columns.Max(c => c.Count());
        var contentH = Math.Max(1, maxRows) * RowPitch;

        foreach (var column in columns)
        {
            var nodes = column.ToList();
            var colX = MarginX + column.Key * ColGap;
            var colH = nodes.Count * RowPitch;
            var startY = MarginY + (contentH - colH) / 2;

            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].X = colX;
                nodes[i].Y = startY + i * RowPitch + (RowPitch - NodeH) / 2;
            }
        }

        var colCount = columns.Count == 0 ? 1 : columns.Max(c => c.Key) + 1;
        model.Width = MarginX * 2 + (colCount - 1) * ColGap + NodeW;
        model.Height = MarginY * 2 + contentH;
    }

    /// <summary>
    /// Cubic bezier path between the facing sides of two nodes: horizontally between different
    /// columns (left-to-right regardless of argument order), vertically within the same column.
    /// </summary>
    public static string EdgePath(TopoNode from, TopoNode to)
    {
        // Same column: connect bottom-center to top-center.
        if (Math.Abs(from.X - to.X) < 1)
        {
            var (top, bottom) = from.Y <= to.Y ? (from, to) : (to, from);
            var x = top.X + NodeW / 2;
            var y1 = top.Y + NodeH;
            var y2 = bottom.Y;
            var my = (y1 + y2) / 2;

            return string.Create(CultureInfo.InvariantCulture,
                $"M {x:0.#} {y1:0.#} C {x:0.#} {my:0.#}, {x:0.#} {my:0.#}, {x:0.#} {y2:0.#}");
        }

        // Different columns: connect right side of the left node to the left side of the right node.
        var (left, right) = from.X <= to.X ? (from, to) : (to, from);
        var lx = left.X + NodeW;
        var ly = left.Y + NodeH / 2;
        var rx = right.X;
        var ry = right.Y + NodeH / 2;
        var mx = (lx + rx) / 2;

        return string.Create(CultureInfo.InvariantCulture,
            $"M {lx:0.#} {ly:0.#} C {mx:0.#} {ly:0.#}, {mx:0.#} {ry:0.#}, {rx:0.#} {ry:0.#}");
    }

    /// <summary>Midpoint of the edge between the two nodes (used to place edge labels).</summary>
    public static (double X, double Y) EdgeMid(TopoNode from, TopoNode to)
    {
        if (Math.Abs(from.X - to.X) < 1)
        {
            var (top, bottom) = from.Y <= to.Y ? (from, to) : (to, from);
            return (top.X + NodeW / 2 + 8, (top.Y + NodeH + bottom.Y) / 2 + 4);
        }

        var (left, right) = from.X <= to.X ? (from, to) : (to, from);
        var lx = left.X + NodeW;
        var ly = left.Y + NodeH / 2;
        var rx = right.X;
        var ry = right.Y + NodeH / 2;

        return ((lx + rx) / 2, (ly + ry) / 2 - 6);
    }

    /// <summary>Formats an SVG coordinate with invariant culture (German locale would emit ',' and break paths).</summary>
    public static string Fmt(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
