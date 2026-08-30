using Dalamud.Configuration;

namespace JEBlock;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// The emote ID that activates JEBlock.
    /// </summary>
    public ushort TriggerEmoteId { get; set; } = 222;

    /// <summary>
    /// Windows virtual-key code used for jumping.
    /// 0x20 = Space.
    /// </summary>
    public int JumpKey { get; set; } = 0x20;
}

