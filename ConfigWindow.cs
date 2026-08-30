using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace JEBlock;

public sealed class ConfigWindow : Window
{
    private static readonly Vector4 ErrorColor = new(0.9f, 0.3f, 0.3f, 1.0f);
    private static readonly Vector4 ActiveColor = new(0.2f, 0.9f, 0.2f, 1.0f);

    private readonly Plugin plugin;

    private string triggerEmoteIdText = string.Empty;
    private bool triggerEmoteIdValid = true;

    private string jumpKeyText = string.Empty;
    private bool jumpKeyValid = true;

    public ConfigWindow(Plugin plugin)
        : base("JEBlock Configuration")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 260),
            MaximumSize = new Vector2(600, 400)
        };
    }

    public override void OnOpen()
    {
        // Re-sync the text buffers with the live configuration every time
        // the window is opened, in case it changed while the window was closed.
        SyncFromConfiguration();
    }

    private void SyncFromConfiguration()
    {
        triggerEmoteIdText = plugin.Configuration.TriggerEmoteId.ToString(CultureInfo.InvariantCulture);
        triggerEmoteIdValid = true;

        jumpKeyText = plugin.Configuration.JumpKey.ToString("X2", CultureInfo.InvariantCulture);
        jumpKeyValid = true;
    }

    public override void Draw()
    {
        ImGui.TextWrapped(
            "When the 'Trigger' emote is being performed " +
            "jumping and emotes will be blocked.");
        ImGui.Spacing();

        // ----------------------------------------
        // Trigger Emote ID
        // ----------------------------------------
        ImGui.Text("Trigger Emote-ID that activates JEBlock");
        ImGui.SetNextItemWidth(100);

        // NOTE: buffer size is 5 (not 6) - ushort.MaxValue (65535) is 5 digits,
        // so a 6th digit slot let the box hold values that could never parse.
        if (ImGui.InputText(
                "##TriggerEmoteId",
                ref triggerEmoteIdText,
                5,
                ImGuiInputTextFlags.CharsDecimal))
        {
            // Validate on every keystroke for immediate feedback, but do NOT
            // write into plugin.Configuration here - anything reading live
            // config (e.g. IsTriggerEmoteCurrentlyActive() below) would
            // otherwise see a half-typed value while the user is still editing.
            triggerEmoteIdValid = triggerEmoteIdText.Length == 0
                || ushort.TryParse(triggerEmoteIdText, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (triggerEmoteIdValid
                && ushort.TryParse(triggerEmoteIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var emoteId))
            {
                plugin.Configuration.TriggerEmoteId = emoteId;
                plugin.SaveConfiguration();
            }
            else
            {
                // Snap back to the last valid value rather than leaving
                // garbage text sitting in the box.
                triggerEmoteIdText = plugin.Configuration.TriggerEmoteId.ToString(CultureInfo.InvariantCulture);
                triggerEmoteIdValid = true;
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(222 = /Wringhands)");

        if (!triggerEmoteIdValid)
        {
            ImGui.TextColored(ErrorColor, "Enter a value between 0 and 65535.");
        }

        ImGui.Spacing();

        // ----------------------------------------
        // Current Emote
        // ----------------------------------------
        ImGui.TextDisabled("For diagnostics, the current live Emote-ID =");
        var currentEmoteId = plugin.GetCurrentEmoteId();
        ImGui.SameLine();
        ImGui.TextDisabled(currentEmoteId.HasValue ? currentEmoteId.Value.ToString(CultureInfo.InvariantCulture) : "None");
        ImGui.Spacing();

        // ----------------------------------------
        // Jump key
        // ----------------------------------------
        ImGui.Text("Jump Key (hex)");
        ImGui.SetNextItemWidth(100);

        if (ImGui.InputText(
                "##JumpKey",
                ref jumpKeyText,
                2,
                ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase))
        {
            jumpKeyValid = jumpKeyText.Length == 0 || TryParseJumpKey(jumpKeyText, out _);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (jumpKeyValid && TryParseJumpKey(jumpKeyText, out var jumpKey))
            {
                plugin.Configuration.JumpKey = jumpKey;
                plugin.SaveConfiguration();
            }
            else
            {
                jumpKeyText = plugin.Configuration.JumpKey.ToString("X2", CultureInfo.InvariantCulture);
                jumpKeyValid = true;
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(20 = Space)");

        if (!jumpKeyValid)
        {
            ImGui.TextColored(ErrorColor, "Enter a hex value between 00 and FF.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Reset to defaults
        // ----------------------------------------
        if (ImGui.Button("Reset to defaults"))
        {
            // NOTE: hardcoded fallbacks - replace with your real Configuration
            // defaults (or named constants) if they differ from these.
            plugin.Configuration.TriggerEmoteId = 222;
            plugin.Configuration.JumpKey = 0x20;
            plugin.SaveConfiguration();
            SyncFromConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Active status
        // ----------------------------------------
        if (plugin.IsTriggerEmoteCurrentlyActive())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ActiveColor);
            ImGui.TextWrapped(
                "Status is ACTIVE - Jump and emotes are currently blocked.");
            ImGui.PopStyleColor();
        }
    }

    private static bool TryParseJumpKey(string text, out int value)
    {
        var ok = int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return ok && value is >= 0 and <= 0xFF;
    }
}