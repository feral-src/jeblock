using System;
using Dalamud.Configuration;

namespace JEBlock;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Windows virtual-key code used for jumping.
    /// 0x20 = Space.
    /// </summary>
    public int JumpKey { get; set; } = 0x20;

    /// <summary>
    /// GUID of the Loci status that activates JEBlock when present on
    /// the local player. Guid.Empty means no Loci status is configured
    /// and JEBlock will remain inactive.
    /// </summary>
    public Guid BlockingStatusId { get; set; } = Guid.Empty;

    /// <summary>
    /// Delay, in milliseconds, between the Loci blocking status
    /// becoming active and JEBlock actually starting to block emotes.
    /// Gives Gagspeak's own trigger time to run first.
    /// </summary>
    public int EmoteBlockDelayMs { get; set; } = 500;
}