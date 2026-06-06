namespace EndpointMonitoring.Web.Services.Topology;

/// <summary>Visual status of a topology node, mapped to the MudBlazor palette.</summary>
public enum NodeStatus
{
    /// <summary>No particular status (secondary text color).</summary>
    Neutral,

    /// <summary>Healthy / connected (success color).</summary>
    Success,

    /// <summary>Degraded, reconnecting or no data yet (warning color).</summary>
    Warning,

    /// <summary>Failing / disconnected (error color).</summary>
    Error
}

/// <summary>What kind of system component a node represents.</summary>
public enum NodeKind
{
    /// <summary>A connected browser session (Blazor circuit).</summary>
    Frontend,

    /// <summary>The Blazor web application itself.</summary>
    WebApp,

    /// <summary>The shared SQLite database.</summary>
    Database,

    /// <summary>The background monitoring worker service.</summary>
    MonitoringService,

    /// <summary>A monitored endpoint.</summary>
    Endpoint
}

/// <summary>State of a topology edge (Up/Down colors the line green/red, Normal keeps it neutral).</summary>
public enum EdgeState
{
    /// <summary>Plain connection line.</summary>
    Normal,

    /// <summary>Link is healthy (success color).</summary>
    Up,

    /// <summary>Link is down or failing (error color, dashed).</summary>
    Down
}

/// <summary>A node in the system topology graph. X/Y are assigned by <see cref="TopologyLayout.Apply"/>.</summary>
public sealed class TopoNode
{
    /// <summary>Unique node id, referenced by <see cref="TopoEdge.FromId"/>/<see cref="TopoEdge.ToId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>What system component this node represents.</summary>
    public required NodeKind Kind { get; init; }

    /// <summary>Primary caption.</summary>
    public required string Label { get; init; }

    /// <summary>Secondary caption below the label (e.g. IP, version, response time).</summary>
    public string? SubLabel { get; init; }

    /// <summary>Hover tooltip; falls back to <see cref="Label"/> when null.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Visual status driving border and status-dot color.</summary>
    public NodeStatus Status { get; init; } = NodeStatus.Neutral;

    /// <summary>SVG path markup (e.g. a MudBlazor <c>Icons.Material.Filled.*</c> constant) rendered inside the node.</summary>
    public string? IconSvg { get; init; }

    /// <summary>Small text chip in the top-right corner (e.g. the user role or provider type).</summary>
    public string? Chip { get; init; }

    /// <summary>Whether to show a warning badge (e.g. unacknowledged alert).</summary>
    public bool Badge { get; init; }

    /// <summary>Layout column: 0 = frontends, 1 = web app/db, 2 = monitoring service, 3 = endpoints.</summary>
    public int Column { get; set; }

    /// <summary>Left edge in SVG user units, assigned by the layout.</summary>
    public double X { get; set; }

    /// <summary>Top edge in SVG user units, assigned by the layout.</summary>
    public double Y { get; set; }
}

/// <summary>A directed edge between two topology nodes, drawn as a cubic bezier.</summary>
public sealed class TopoEdge
{
    /// <summary>Unique edge id (used to target pulse animations).</summary>
    public required string Id { get; init; }

    /// <summary>Id of the source node.</summary>
    public required string FromId { get; init; }

    /// <summary>Id of the target node.</summary>
    public required string ToId { get; init; }

    /// <summary>Optional small label at the edge midpoint (e.g. response time and interval).</summary>
    public string? Label { get; init; }

    /// <summary>Link state driving the edge color.</summary>
    public EdgeState State { get; init; } = EdgeState.Normal;

    /// <summary>Transiently set while a pulse animation runs along this edge.</summary>
    public bool Pulsing { get; set; }
}

/// <summary>The complete topology graph with computed canvas dimensions.</summary>
public sealed class TopologyModel
{
    /// <summary>All nodes of the graph.</summary>
    public List<TopoNode> Nodes { get; init; } = [];

    /// <summary>All edges of the graph.</summary>
    public List<TopoEdge> Edges { get; init; } = [];

    /// <summary>Canvas width in SVG user units, computed by <see cref="TopologyLayout.Apply"/>.</summary>
    public double Width { get; set; }

    /// <summary>Canvas height in SVG user units, computed by <see cref="TopologyLayout.Apply"/>.</summary>
    public double Height { get; set; }

    /// <summary>Finds a node by id, or null.</summary>
    public TopoNode? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
}
