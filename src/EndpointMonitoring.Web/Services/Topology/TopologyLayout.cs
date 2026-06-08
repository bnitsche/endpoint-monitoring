using System.Globalization;

namespace EndpointMonitoring.Web.Services.Topology;

/// <summary>
/// Deterministic layered layout: nodes are grouped by column, stacked vertically and
/// each column is centered against the tallest column. All coordinates are in SVG user units.
/// </summary>
public static class TopologyLayout
{
    /// <summary>Minimum card width; auto-sizing never shrinks a card below this.</summary>
    public const double MinNodeW = 170;

    /// <summary>Node box height.</summary>
    public const double NodeH = 56;

    /// <summary>X offset where a card's text column begins (after the accent stripe, beacon and icon).</summary>
    public const double TextStartX = 62;

    /// <summary>Maximum number of characters shown for a sub-label before it is ellipsized.</summary>
    public const int SubLabelMax = 36;

    /// <summary>Empty horizontal gap between adjacent columns (room for the connecting edges and their labels).</summary>
    public const double ColGap = 150;

    /// <summary>Vertical distance between stacked node slots.</summary>
    public const double RowPitch = 84;

    /// <summary>Horizontal canvas margin.</summary>
    public const double MarginX = 40;

    /// <summary>Vertical canvas margin.</summary>
    public const double MarginY = 40;

    /// <summary>
    /// Estimates the card width needed to show the node's content without clipping. Text cannot be
    /// measured server-side, so width is approximated from character counts (label rendered fully,
    /// sub-label capped at <see cref="SubLabelMax"/>) plus the chip and padding.
    /// </summary>
    public static double MeasureNodeWidth(TopoNode node)
    {
        const double rightPad = 16;
        const double chipGap = 12;

        var labelW = node.Label.Length * 7.7;
        var chipW = node.Chip is null ? 0 : chipGap + node.Chip.Length * 7.0 + 6;
        var topRow = TextStartX + labelW + chipW + rightPad;

        var subLen = node.SubLabel is null ? 0 : Math.Min(node.SubLabel.Length, SubLabelMax);
        var subRow = subLen == 0 ? 0 : TextStartX + subLen * 6.1 + rightPad;

        return Math.Max(MinNodeW, Math.Max(topRow, subRow));
    }

    /// <summary>Assigns Width/X/Y to every node and computes the model's Width/Height.</summary>
    public static void Apply(TopologyModel model)
    {
        var columns = model.Nodes.GroupBy(n => n.Column).OrderBy(g => g.Key).ToList();
        var maxRows = columns.Count == 0 ? 0 : columns.Max(c => c.Count());
        var contentH = Math.Max(1, maxRows) * RowPitch;
        var maxColKey = columns.Count == 0 ? 0 : columns.Max(c => c.Key);

        // Each column is as wide as its widest card; every card in the column adopts that width so
        // their right edges line up and the connecting edges attach at a consistent x.
        var colWidth = new Dictionary<int, double>();
        foreach (var column in columns)
        {
            var width = column.Max(MeasureNodeWidth);
            colWidth[column.Key] = width;
            foreach (var node in column)
                node.Width = width;
        }

        // Column left edges accumulate the previous columns' widths plus the inter-column gap.
        var colX = new Dictionary<int, double>();
        var x = MarginX;
        for (var c = 0; c <= maxColKey; c++)
        {
            colX[c] = x;
            x += (colWidth.TryGetValue(c, out var w) ? w : MinNodeW) + ColGap;
        }

        foreach (var column in columns)
        {
            var nodes = column.ToList();
            var startY = MarginY + (contentH - nodes.Count * RowPitch) / 2;

            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].X = colX[column.Key];
                nodes[i].Y = startY + i * RowPitch + (RowPitch - NodeH) / 2;
            }
        }

        var lastWidth = colWidth.TryGetValue(maxColKey, out var lw) ? lw : MinNodeW;
        model.Width = colX[maxColKey] + lastWidth + MarginX;
        model.Height = MarginY * 2 + contentH;
    }

    /// <summary>Corner radius of the rounded right-angle elbow on inter-column edges.</summary>
    public const double EdgeRadius = 12;

    /// <summary>
    /// Orthogonal (right-angle) connector between the facing sides of two nodes, with rounded
    /// elbows: horizontally between different columns (left-to-right regardless of argument
    /// order, bending at the column gap), vertically within the same column. Straight lines
    /// keep their right-angle shape; the bend introduces no overlap with node boxes, leaving
    /// the inter-column gap clear for edge labels.
    /// </summary>
    public static string EdgePath(TopoNode from, TopoNode to)
    {
        // Same column: a straight vertical line, bottom-center to top-center.
        if (Math.Abs(from.X - to.X) < 1)
        {
            var (top, bottom) = from.Y <= to.Y ? (from, to) : (to, from);
            var x = top.X + top.Width / 2;
            var y1 = top.Y + NodeH;
            var y2 = bottom.Y;

            return string.Create(CultureInfo.InvariantCulture,
                $"M {x:0.#} {y1:0.#} L {x:0.#} {y2:0.#}");
        }

        // Different columns: right side of the left node to the left side of the right node.
        var (left, right) = from.X <= to.X ? (from, to) : (to, from);
        var lx = left.X + left.Width;
        var ly = left.Y + NodeH / 2;
        var rx = right.X;
        var ry = right.Y + NodeH / 2;
        var mx = (lx + rx) / 2;

        // Aligned rows: a single straight horizontal line, no elbow needed.
        if (Math.Abs(ly - ry) < 1)
            return string.Create(CultureInfo.InvariantCulture, $"M {lx:0.#} {ly:0.#} L {rx:0.#} {ry:0.#}");

        // Elbow: out from the left node, bend down/up at the gap midpoint, in to the right node.
        var dir = ry > ly ? 1 : -1;
        var r = Math.Min(EdgeRadius, Math.Min(Math.Abs(ry - ly) / 2, mx - lx));
        var c1 = ly + dir * r;
        var c2 = ry - dir * r;
        var t1 = mx - r;
        var t2 = mx + r;

        return string.Create(CultureInfo.InvariantCulture,
            $"M {lx:0.#} {ly:0.#} L {t1:0.#} {ly:0.#} Q {mx:0.#} {ly:0.#} {mx:0.#} {c1:0.#} " +
            $"L {mx:0.#} {c2:0.#} Q {mx:0.#} {ry:0.#} {t2:0.#} {ry:0.#} L {rx:0.#} {ry:0.#}");
    }

    /// <summary>
    /// Point on the edge used to anchor its label: the center of the vertical bend segment,
    /// which always lies in the clear gap between columns (never on top of a node box).
    /// </summary>
    public static (double X, double Y) EdgeMid(TopoNode from, TopoNode to)
    {
        if (Math.Abs(from.X - to.X) < 1)
        {
            var (top, bottom) = from.Y <= to.Y ? (from, to) : (to, from);
            return (top.X + top.Width / 2, (top.Y + NodeH + bottom.Y) / 2);
        }

        var (left, right) = from.X <= to.X ? (from, to) : (to, from);
        var lx = left.X + left.Width;
        var ly = left.Y + NodeH / 2;
        var rx = right.X;
        var ry = right.Y + NodeH / 2;

        return ((lx + rx) / 2, (ly + ry) / 2);
    }

    /// <summary>Formats an SVG coordinate with invariant culture (German locale would emit ',' and break paths).</summary>
    public static string Fmt(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
