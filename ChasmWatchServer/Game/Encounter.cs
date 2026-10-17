/// <summary>
/// Shared turn order for a single world cell, modelled as a timeline. Every
/// unit in the cell sits on a number line; whoever has the lowest counter acts
/// next. Each turn a unit has an <see cref="Allowance"/> to spend on actions;
/// every cost is added to both this turn's spending and its counter, pushing
/// them later. Once the allowance is used up the turn ends on its own.
///
/// Player turns are driven by the hub; enemy turns are driven by the tile's AI
/// (<see cref="WorldTile.RunEnemyTurns"/>). An <see cref="Encounter"/> only
/// exists while the cell still contains at least one enemy;
/// <see cref="GameState"/> creates and discards it around that condition.
/// Peacefulness is irrelevant here.
/// </summary>
public sealed class Encounter
{
    /// <summary>Cost in counter units for a generic interaction (quest item or attack).</summary>
    public const int InteractCost = 5;

    /// <summary>Cost for stepping through a world exit.</summary>
    public const int TraverseCost = 1;

    /// <summary>Cost a unit pays to voluntarily skip their turn (free once half the allowance is spent).</summary>
    public const int PassCost = 10;

    /// <summary>
    /// How much a unit may spend during a single turn. Reaching it ends the
    /// turn automatically, though the action that hits it may overshoot.
    /// </summary>
    public const int Allowance = 20;

    /// <summary>
    /// Once a unit has spent this much of its allowance, passing is free.
    /// Below it, a pass still advances the counter by <see cref="PassCost"/>,
    /// so every turn moves a unit at least this far along the timeline.
    /// </summary>
    public const int FreePassThreshold = 10;

    private sealed class TurnSlot
    {
        public required Unit Unit { get; init; }

        /// <summary>Position on the timeline; grows as the unit acts.</summary>
        public int Counter { get; set; }

        /// <summary>How much of the current turn's allowance has been used.</summary>
        public int Spent { get; set; }

        /// <summary>The unit's original D20 roll; a stable tie-breaker.</summary>
        public int InitialRoll { get; init; }

        /// <summary>Re-roll used only to order units that shared an initial roll.</summary>
        public int TieBreak { get; set; }

        /// <summary>Enrollment order; final deterministic fallback.</summary>
        public int Sequence { get; init; }

        public bool IsEnemy => Unit is Enemy;
    }

    private readonly object _lock = new();
    private readonly List<TurnSlot> _order = new();
    private int _sequence;

    /// <summary>The slot whose turn is in progress, or null when no turn is open.</summary>
    private TurnSlot? _active;

    public bool HasEnemies
    {
        get { lock (_lock) return _order.Any(s => s.IsEnemy); }
    }

    public bool HasPlayers
    {
        get { lock (_lock) return _order.Any(s => !s.IsEnemy); }
    }

    /// <summary>The unit whose turn is open, if any. Opens the next turn lazily.</summary>
    public Unit? ActiveUnit
    {
        get { lock (_lock) { EnsureActiveLocked(); return _active?.Unit; } }
    }

    /// <summary>True while <paramref name="unit"/> is the one whose turn is open.</summary>
    public bool IsActive(Unit unit)
    {
        lock (_lock)
        {
            EnsureActiveLocked();
            return _active != null && ReferenceEquals(_active.Unit, unit);
        }
    }

    /// <summary>
    /// Seats every unit currently in the cell and rolls initiative. Each rolls
    /// a D20; ties are broken by a re-roll that only orders the tied units —
    /// their counters (and so their place versus everyone else) are unchanged.
    /// Whoever ends up first becomes the open turn; if that is an enemy, the
    /// tile's AI resolves it before any player may act.
    /// </summary>
    public void Start(IEnumerable<Unit> units)
    {
        lock (_lock)
        {
            foreach (var unit in units)
            {
                int roll = RollD20();
                AddSlotLocked(unit, counter: roll, initialRoll: roll);
            }
            BreakInitialTiesLocked();
            EnsureActiveLocked();
        }
    }

    /// <summary>
    /// Adds a late-joining unit. Their counter is set to
    /// <c>cursor - D20</c> so they always act first. Foreign-friendly: a unit
    /// already seated is ignored.
    /// </summary>
    public void EnrollLate(Unit unit)
    {
        lock (_lock)
        {
            if (FindLocked(unit) != null) return;
            int counter = CursorLocked() - RollD20();
            AddSlotLocked(unit, counter: counter, initialRoll: counter);
            EnsureActiveLocked();
        }
    }

    /// <summary>Removes a unit (killed, left the cell, ...).</summary>
    public bool Remove(Unit unit)
    {
        lock (_lock)
        {
            int idx = _order.FindIndex(s => ReferenceEquals(s.Unit, unit));
            if (idx < 0) return false;
            var slot = _order[idx];
            _order.RemoveAt(idx);
            if (ReferenceEquals(_active, slot)) _active = null;
            return true;
        }
    }

    /// <summary>
    /// True when <paramref name="unit"/> may act right now. Pending enemy turns
    /// are resolved by the hub before a player is allowed to act, so this only
    /// has to compare against the currently open turn.
    /// </summary>
    public bool TryValidateTurn(Unit unit, out string? error)
    {
        lock (_lock)
        {
            EnsureActiveLocked();
            var slot = FindLocked(unit);
            if (slot == null)
            {
                error = "You are not part of this encounter.";
                return false;
            }
            if (_active == null || !ReferenceEquals(_active.Unit, unit))
            {
                string name = _active?.Unit?.Name ?? "another unit";
                error = $"It's not your turn — waiting on {name}.";
                return false;
            }
            error = null;
            return true;
        }
    }

    /// <summary>
    /// Adds an action's cost to the actor's counter and this turn's spending.
    /// When the allowance has been used up the turn ends on its own.
    /// Call only after <see cref="TryValidateTurn"/> returned true.
    /// </summary>
    public void ApplyCost(Unit unit, int cost)
    {
        lock (_lock)
        {
            var slot = FindLocked(unit);
            if (slot == null || !ReferenceEquals(slot, _active)) return;
            SpendLocked(slot, cost);
            if (slot.Spent >= Allowance) EndTurnLocked();
        }
    }

    /// <summary>
    /// Voluntarily ends the actor's turn. Passing is free once at least
    /// <see cref="FreePassThreshold"/> has been spent this turn; otherwise it
    /// still advances the counter by <see cref="PassCost"/>.
    /// Call only after <see cref="TryValidateTurn"/> returned true.
    /// </summary>
    public void Pass(Unit unit)
    {
        lock (_lock)
        {
            var slot = FindLocked(unit);
            if (slot == null || !ReferenceEquals(slot, _active)) return;
            int cost = slot.Spent >= FreePassThreshold ? 0 : PassCost;
            SpendLocked(slot, cost);
            EndTurnLocked();
        }
    }

    /// <summary>Serialisable snapshot of the timeline for the client.</summary>
    public TurnState BuildState()
    {
        lock (_lock)
        {
            EnsureActiveLocked();
            var current = _active;
            var order = _order
                .OrderBy(s => s.Counter)
                .ThenBy(s => s.InitialRoll)
                .ThenBy(s => s.TieBreak)
                .ThenBy(s => s.Sequence)
                .Select(s => new TurnSlotState(
                    s.Unit.ID,
                    s.Unit.Name,
                    s.Counter,
                    s.Spent,
                    s.IsEnemy,
                    current != null && ReferenceEquals(current, s)))
                .ToList();

            return new TurnState(
                Active: true,
                Cursor: CursorLocked(),
                Allowance: Allowance,
                CurrentActorId: current?.Unit?.ID,
                CurrentActorName: current?.Unit?.Name,
                Order: order);
        }
    }

    private void SpendLocked(TurnSlot slot, int cost)
    {
        slot.Spent += cost;
        slot.Counter += cost;
    }

    private void EndTurnLocked()
    {
        if (_active != null) _active.Spent = 0;
        _active = null;
    }

    private void AddSlotLocked(Unit unit, int counter, int initialRoll)
    {
        _order.Add(new TurnSlot
        {
            Unit = unit,
            Counter = counter,
            InitialRoll = initialRoll,
            Sequence = _sequence++,
        });
    }

    /// <summary>
    /// For every initial-roll tie, give the tied units distinct re-rolls so a
    /// stable order exists. Counter placement versus other units is untouched.
    /// </summary>
    private void BreakInitialTiesLocked()
    {
        foreach (var group in _order.GroupBy(s => s.InitialRoll).Where(g => g.Count() > 1))
        {
            var used = new HashSet<int>();
            foreach (var slot in group)
            {
                int tieBreak;
                do { tieBreak = RollD20(); } while (!used.Add(tieBreak));
                slot.TieBreak = tieBreak;
            }
        }
    }

    private TurnSlot? FindLocked(Unit unit) =>
        _order.FirstOrDefault(s => ReferenceEquals(s.Unit, unit));

    private TurnSlot? CurrentLocked() =>
        _order.Count == 0
            ? null
            : _order.OrderBy(s => s.Counter)
                    .ThenBy(s => s.InitialRoll)
                    .ThenBy(s => s.TieBreak)
                    .ThenBy(s => s.Sequence)
                    .First();

    private int CursorLocked() =>
        _order.Count == 0 ? 0 : _order.Min(s => s.Counter);

    /// <summary>
    /// Opens a turn for the lowest unit on the timeline. Enemies are left as the
    /// active actor for the tile's AI to resolve; players are left for the hub.
    /// Idempotent while a turn is already open.
    /// </summary>
    private void EnsureActiveLocked()
    {
        if (_active != null) return;
        var current = CurrentLocked();
        if (current == null) return;
        _active = current;
        _active.Spent = 0;
    }

    private static int RollD20() => Random.Shared.Next(1, 21);
}

/// <summary>Snapshot of a cell's turn order, broadcast to the tile group.</summary>
public sealed record TurnState(
    bool Active,
    int Cursor,
    int Allowance,
    Guid? CurrentActorId,
    string? CurrentActorName,
    IReadOnlyList<TurnSlotState> Order);

/// <summary>One combatant's place in a <see cref="TurnState"/>.</summary>
public sealed record TurnSlotState(
    Guid UnitId,
    string? Name,
    int Counter,
    int Spent,
    bool IsEnemy,
    bool IsCurrent);
