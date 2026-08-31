using System;
using System.Linq;

using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

using LociApi.Enums;
using LociApi.Helpers;
using LociApi.Ipc;

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

    // ----------------------------------------
    // Emote blocking
    // ----------------------------------------

    private readonly Hook<ExecuteEmoteDelegate> executeEmoteHook;

    private delegate bool ExecuteEmoteDelegate(
        EmoteManager* thisPtr,
        ushort emoteId,
        EmoteController.PlayEmoteOption* playEmoteOption);

    // ----------------------------------------
    // Loci integration
    // ----------------------------------------

    private readonly GetManagerInfo lociGetManagerInfo;
    private readonly EventSubscriber<nint, ManagerChangeType> lociManagerChanged;

    // Whether Loci's IPC could be reached the last time we checked.
    // Distinct from blockingActive - Loci can be detected but simply
    // not reporting the configured status as active.
    public bool IsLociAvailable { get; private set; }

    // Cached Loci blocking state.
    // Both jump and emote blocking use this value.
    private bool blockingActive;

    // When blockingActive last became true. Used to delay emote
    // blocking so an emote that starts right as blocking kicks in
    // isn't instantly cut off.
    private DateTime? blockingActiveSince;

    // Used to detect local player changes.
    private nint localPlayerAddress;

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

        try
        {
            // ----------------------------------------
            // Emote blocking hook
            // ----------------------------------------

            var executeEmoteAddress =
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
            // Loci
            // ----------------------------------------

            lociGetManagerInfo = new GetManagerInfo(pluginInterface);

            // Establish the initial state before subscribing to changes.
            RefreshBlockingState();

            lociManagerChanged =
                ManagerChanged.Subscriber(
                    pluginInterface,
                    OnLociManagerChanged);

            // ----------------------------------------
            // UI
            // ----------------------------------------

            windowSystem = new WindowSystem(Name);
            configWindow = new ConfigWindow(this);

            windowSystem.AddWindow(configWindow);

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;

            // ----------------------------------------
            // Command
            // ----------------------------------------

            commandManager.AddHandler(
                CommandName,
                new CommandInfo(OnCommand)
                {
                    HelpMessage = "opens the configuration window.\n/jeblock endemote — Stops the current emote loop and return to idle stance."
                });

            // ----------------------------------------
            // Jump blocking
            // ----------------------------------------

            framework.Update += OnFrameworkUpdate;
        }
        catch (Exception ex)
        {
            log.Error(
                ex,
                "JEBlock failed to initialize; rolling back partial setup.");

            // Whatever succeeded above - a live hook, an event
            // subscription - needs to be torn down, or a failed load
            // leaves it running forever with nothing to dispose it.
            // If the cleanup itself throws, don't let it hide the
            // real cause - log it and still surface the original.
            try
            {
                Dispose();
            }
            catch (Exception disposeEx)
            {
                log.Error(
                    disposeEx,
                    "Error while unwinding a failed JEBlock initialization.");
            }

            throw;
        }

        log.Information(
            "JEBlock loaded. Jump key: 0x{0:X2}, Loci status: {1}, Active: {2}",
            Configuration.JumpKey,
            Configuration.BlockingStatusId,
            blockingActive);
    }

    // ----------------------------------------
    // Command
    // ----------------------------------------

    private void OnCommand(string command, string args)
    {
        var trimmedArgs = args.Trim();

        if (trimmedArgs.Equals("endemote", StringComparison.OrdinalIgnoreCase))
        {
            EndCurrentEmote();
            return;
        }

        if (trimmedArgs.Length > 0)
        {
            log.Warning("JEBlock: unknown /jeblock subcommand '{0}'.", trimmedArgs);
            return;
        }

        configWindow.IsOpen = !configWindow.IsOpen;
    }

    private void EndCurrentEmote()
    {
        var localPlayer = objectTable.LocalPlayer;

        if (localPlayer == null || localPlayer.Address == nint.Zero)
        {
            log.Warning("JEBlock: /jeblock endemote — no local player found.");
            return;
        }

        ((Character*)localPlayer.Address)->SetMode(CharacterModes.Normal, 0);
        log.Information("JEBlock: emote ended via /jeblock endemote.");
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
    // Loci
    // ----------------------------------------

    private void OnLociManagerChanged(
        nint address,
        ManagerChangeType _)
    {
        // Loci's IPC event isn't guaranteed to fire on the framework
        // thread. blockingActive/blockingActiveSince are also read
        // every frame from OnFrameworkUpdate and from the emote hook,
        // so hop onto the framework thread before touching them.
        // Runs immediately if we're already on it.
        framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = objectTable.LocalPlayer;

            if (localPlayer == null)
            {
                SetBlockingActive(false);
                localPlayerAddress = nint.Zero;
                return;
            }

            if (address != localPlayer.Address)
                return;

            RefreshBlockingState();
        });
    }

    private void RefreshBlockingState()
    {
        bool newState;

        try
        {
            IsLociAvailable = lociGetManagerInfo.Valid;

            if (Configuration.BlockingStatusId == Guid.Empty ||
                !lociGetManagerInfo.Valid)
            {
                newState = false;
            }
            else
            {
                var statuses = lociGetManagerInfo.Invoke();

                newState = false;
                foreach (var status in statuses)
                {
                    if (status.GUID == Configuration.BlockingStatusId)
                    {
                        newState = true;
                        break;
                    }
                }
            }
        }
        catch (IpcNotReadyError)
        {
            // Loci hasn't finished registering its IPC yet - expected during
            // plugin load ordering, not a real failure. Fail safe and quiet.
            IsLociAvailable = false;
            newState = false;

            log.Debug("Loci IPC not ready yet; treating blocking as inactive for now.");
        }
        catch (Exception ex)
        {
            // Fail safe: if Loci cannot be queried, don't block anything.
            IsLociAvailable = false;
            newState = false;

            log.Error(
                ex,
                "Failed to read Loci status manager.");
        }

        SetBlockingActive(newState);
    }

    private void SetBlockingActive(bool value)
    {
        if (blockingActive == value)
            return;

        blockingActive = value;
        blockingActiveSince = value ? DateTime.UtcNow : null;

        log.Information(
            "JEBlock blocking is now {0}.",
            blockingActive ? "ACTIVE" : "INACTIVE");
    }

    public bool IsBlockingActive => blockingActive;

    // True once blockingActive has been on for at least
    // Configuration.EmoteBlockDelayMs. Jump blocking ignores this and
    // applies instantly; only emote blocking uses the delay.
    // Configuration.EmoteBlockDelayMs is clamped here (not just in the
    // config UI) in case the config file was ever hand-edited.
    private bool ShouldBlockEmotes =>
        blockingActive &&
        (!blockingActiveSince.HasValue ||
         (DateTime.UtcNow - blockingActiveSince.Value).TotalMilliseconds
            >= Math.Max(0, Configuration.EmoteBlockDelayMs));

    // ----------------------------------------
    // Jump blocking
    // ----------------------------------------

    private void OnFrameworkUpdate(IFramework _)
    {
        // Loci may load after us. Re-check availability every frame so
        // IsLociAvailable (and blocking state) picks it up the moment it
        // comes online, without waiting for another ManagerChanged event.
        if (lociGetManagerInfo.Valid != IsLociAvailable)
            RefreshBlockingState();

        if (!blockingActive)
            return;

        // Detect a local player change while blocking is active.
        var localPlayer = objectTable.LocalPlayer;

        if (localPlayer == null)
        {
            SetBlockingActive(false);
            localPlayerAddress = nint.Zero;
            return;
        }

        if (localPlayer.Address != localPlayerAddress)
        {
            localPlayerAddress = localPlayer.Address;
            RefreshBlockingState();

            if (!blockingActive)
                return;
        }

        if (keyState[Configuration.JumpKey])
            keyState[Configuration.JumpKey] = false;
    }

    // ----------------------------------------
    // Emote blocking
    // ----------------------------------------

    private bool ExecuteEmoteDetour(
        EmoteManager* thisPtr,
        ushort emoteId,
        EmoteController.PlayEmoteOption* playEmoteOption)
    {
        if (ShouldBlockEmotes)
            return false;

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

        // Apply configuration changes immediately.
        RefreshBlockingState();
    }

    // ----------------------------------------
    // Dispose
    // ----------------------------------------

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;

        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;

        commandManager.RemoveHandler(CommandName);

        // These are only set once their part of construction succeeds,
        // so if the constructor failed partway through and is now
        // unwinding via Dispose(), any of them may still be null.
        lociManagerChanged?.Dispose();
        executeEmoteHook?.Dispose();

        windowSystem?.RemoveAllWindows();

        log.Information("JEBlock unloaded.");
    }
}