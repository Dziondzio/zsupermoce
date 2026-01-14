using CounterStrikeSharp.API.Core;

namespace WASDSharedAPI;

public interface IWasdMenu
{
    public string? Title { get; set; }
    public string? ItemText { get; set; }
    public string? ItemHoverText { get; set; }
    public string? ControlText { get; set; }
    public LinkedList<IWasdMenuOption>? Options { get; set; }
    // previous option node
    public LinkedListNode<IWasdMenuOption>? Prev { get; set; }
    public LinkedListNode<IWasdMenuOption> Add(string display, Action<CCSPlayerController, IWasdMenuOption> onChoice);
}