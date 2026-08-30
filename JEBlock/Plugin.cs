using System;

using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace JEBlock;

public unsafe sealed class Plugin : IDalamudPlugin
{
public string Name => "JEBlock";

private const string CommandName = "/jeblock";

private readonly IDalamudPluginInterface pluginInterface;
private readonly IFramework framework;
private readonly IKeyState keyState;
private readonly IObjectTable objectTable;
private readonly IPluginLog log;
private readonly ICommandManager commandManager;

private readonly WindowSystem windowSystem;
private readonly ConfigWindow configWindow;

private readonly Hook<ExecuteEmoteDelegate> executeEmoteHook;

private delegate bool ExecuteEmoteDelegate(
    EmoteManager* thisPtr,
    ushort emoteId,
    EmoteController.PlayEmoteOption* playEmoteOption);

public Configuration Configuration { get; }

public Plugin(
    IDalamudPluginInterface pluginInterface,
    IFramework framework,
    IKeyState keyState,
    IObjectTable objectTable,
    IPluginLog log,
    IGameInteropProvider gameInterop,
    ICommandManager commandManager)
{
    this.pluginInterface = pluginInterface;
    this.framework = framework;
    this.keyState = keyState;
    this.objectTable = objectTable;
    this.log = log;
    this.commandManager = commandManager;

    Configuration =
        pluginInterface.GetPluginConfig() as Configuration
        ?? new Configuration();

    // ----------------------------------------
    // UI
    // ----------------------------------------

    windowSystem = new WindowSystem("JEBlock");

    configWindow = new ConfigWindow(this);

    windowSystem.AddWindow(configWindow);

    pluginInterface.UiBuilder.Draw += DrawUI;
    pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
    pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;

    // ----------------------------------------
    // Command
    // ----------------------------------------

    commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
    {
        HelpMessage = "Opens the JEBlock config window."
    });

    // ----------------------------------------
    // Emote hook
    // ----------------------------------------

    nint executeEmoteAddress =
        (nint)EmoteManager.MemberFunctionPointers.ExecuteEmote;

    if (executeEmoteAddress == 0)
    {
        throw new InvalidOperationException(
            "FFXIVClientStructs returned a null ExecuteEmote address.");
    }

    executeEmoteHook =
        gameInterop.HookFromAddress<ExecuteEmoteDelegate>(
            executeEmoteAddress,
            ExecuteEmoteDetour);

    executeEmoteHook.Enable();

    // ----------------------------------------
    // Jump blocking
    // ----------------------------------------

    framework.Update += OnFrameworkUpdate;

    log.Information(
        "JEBlock loaded. Trigger emote: {0}, Jump key: 0x{1:X2}",
        Configuration.TriggerEmoteId,
        Configuration.JumpKey);
}

// ----------------------------------------
// Command
// ----------------------------------------

private void OnCommand(string command, string args)
{
    configWindow.IsOpen = !configWindow.IsOpen;
}

// ----------------------------------------
// UI
// ----------------------------------------

private void DrawUI()
{
    windowSystem.Draw();
}

public void OpenConfigUi()
{
    configWindow.IsOpen = true;
}

public void OpenMainUi()
{
    configWindow.IsOpen = true;
}

// ----------------------------------------
// Local player / character lookup
// ----------------------------------------

/// <summary>
/// Gets a pointer to the local player's character,
/// or null if there is no local player.
/// </summary>
private Character* GetLocalCharacter()
{
    var localPlayer = objectTable.LocalPlayer;

    if (localPlayer == null)
        return null;

    return (Character*)localPlayer.Address;
}

// ----------------------------------------
// Current Emote
// ----------------------------------------

/// <summary>
/// Gets the emote ID currently being performed
/// by the local player.
///
/// Returns null when there is no active emote.
/// </summary>
public ushort? GetCurrentEmoteId()
{
    var character = GetLocalCharacter();

    if (character == null)
        return null;

    if (!character->EmoteController.IsEmoting())
        return null;

    return character->EmoteController.EmoteId;
}

// ----------------------------------------
// Trigger
// ----------------------------------------

/// <summary>
/// Returns true only while the configured
/// trigger emote is currently playing.
/// </summary>
public bool IsTriggerEmoteCurrentlyActive()
{
    var character = GetLocalCharacter();

    if (character == null)
        return false;

    if (!character->EmoteController.IsEmoting())
        return false;

    return character->EmoteController.EmoteId ==
           Configuration.TriggerEmoteId
           && character->EmoteController.IsInEmoteLoop();
}

// ----------------------------------------
// Jump blocking
// ----------------------------------------

private void OnFrameworkUpdate(IFramework framework)
{
    try
    {
        if (!IsTriggerEmoteCurrentlyActive())
        {
            return;
        }

            /*
             * The configured trigger emote is active.
             *
             * Suppress the configured jump key.
             */
            // Only write when the key is actually pressed, avoiding an
            // unnecessary keyState write every single frame.
            if (keyState[Configuration.JumpKey])
            {
                keyState[Configuration.JumpKey] = false;
                log.Debug("Blocked jump keypress while trigger emote {0} is active.",Configuration.TriggerEmoteId);
            }
    }
    catch (Exception ex)
    {
        log.Error(
            ex,
            "Error while processing jump blocking.");
    }
}

// ----------------------------------------
// Emote blocking
// ----------------------------------------

private bool ExecuteEmoteDetour(
    EmoteManager* thisPtr,
    ushort emoteId,
    EmoteController.PlayEmoteOption* playEmoteOption)
{
    try
    {
        /*
         * If the trigger emote is currently active,
         * prevent any new emote from executing.
         */
        if (IsTriggerEmoteCurrentlyActive())
        {
           
            log.Debug(
                "Blocked emote {0} while trigger emote {1} is active.",
                emoteId,
                Configuration.TriggerEmoteId);          

            return false;
        }
    }
    catch (Exception ex)
    {
        /*
         * If the status check fails, allow the original
         * function to execute.
         */
        log.Error(
            ex,
            "Error checking trigger emote for emote {0}.",
            emoteId);
    }

    return executeEmoteHook.Original(
        thisPtr,
        emoteId,
        playEmoteOption);
}

// ----------------------------------------
// Configuration
// ----------------------------------------

public void SaveConfiguration()
{
    pluginInterface.SavePluginConfig(Configuration);
}

// ----------------------------------------
// Dispose
// ----------------------------------------

public void Dispose()
{
    commandManager.RemoveHandler(CommandName);

    framework.Update -= OnFrameworkUpdate;

    pluginInterface.UiBuilder.Draw -= DrawUI;
    pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
    pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;

    windowSystem.RemoveAllWindows();

    executeEmoteHook.Dispose();

    log.Information("JEBlock unloaded.");
}

}