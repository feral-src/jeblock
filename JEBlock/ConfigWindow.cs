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

    private string jumpKeyText = string.Empty;
    private bool jumpKeyValid = true;

    private string blockingStatusIdText = string.Empty;
    private bool blockingStatusIdValid = true;

    private int emoteBlockDelayMs;

    public ConfigWindow(Plugin plugin)
        : base("JEBlock Configuration")
    {
        this.plugin = plugin;

        // Applied once, the first time the window is ever opened.
        // After that the user's own size sticks.
        Size = new Vector2(430, 320);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Free to resize, but not below this - keeps the fields and
        // labels from clipping if the user drags it down too small.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
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
        jumpKeyText = plugin.Configuration.JumpKey.ToString(
            "X2",
            CultureInfo.InvariantCulture);

        jumpKeyValid = true;

        // Show an empty box rather than "00000000-0000-..." when unset,
        // since Guid.Empty means "no status configured".
        blockingStatusIdText = GetBlockingStatusIdText();
        blockingStatusIdValid = true;

        emoteBlockDelayMs = plugin.Configuration.EmoteBlockDelayMs;
    }

    private string GetBlockingStatusIdText()
    {
        return plugin.Configuration.BlockingStatusId == Guid.Empty
            ? string.Empty
            : plugin.Configuration.BlockingStatusId.ToString(
                "D",
                CultureInfo.InvariantCulture);
    }

    public override void Draw()
    {
        // ----------------------------------------
        // Loci activation status GUID
        // ----------------------------------------

        ImGui.TextWrapped(
            "When this Loci-ID is active, jumping and " +
            "emotes will be blocked.");

        ImGui.SetNextItemWidth(320);

        // Standard "D" format GUID is 36 chars (8-4-4-4-12). Give a little
        // headroom in case someone pastes one with braces and we trim them.
        if (ImGui.InputText(
                "##BlockingStatusId",
                ref blockingStatusIdText,
                40))
        {
            blockingStatusIdValid =
                blockingStatusIdText.Length == 0 ||
                Guid.TryParse(blockingStatusIdText.Trim(), out _);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            var trimmed = blockingStatusIdText.Trim();

            if (trimmed.Length == 0)
            {
                // Empty box means "no Loci status configured".
                plugin.Configuration.BlockingStatusId = Guid.Empty;
                plugin.SaveConfiguration();

                blockingStatusIdValid = true;
            }
            else if (Guid.TryParse(trimmed, out var statusId))
            {
                plugin.Configuration.BlockingStatusId = statusId;
                plugin.SaveConfiguration();

                blockingStatusIdText = statusId.ToString(
                    "D",
                    CultureInfo.InvariantCulture);

                blockingStatusIdValid = true;
            }
            else
            {
                // Restore the last valid value rather than leaving
                // invalid text sitting in the box.
                blockingStatusIdText = GetBlockingStatusIdText();
                blockingStatusIdValid = true;
            }
        }

        ImGui.TextDisabled("(leave empty to disable)");

        if (!blockingStatusIdValid)
        {
            ImGui.TextColored(
                ErrorColor,
                "Enter a valid GUID, e.g. 12345678-1234-1234-1234-123456789abc.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Emote block delay
        // ----------------------------------------

        ImGui.TextWrapped(
            "Delay between Loci-ID being detected and emotes being blocked. " +
            "As we need to give the GagSpeak trigger time to complete.");
        ImGui.SetNextItemWidth(100);

        ImGui.InputInt("##EmoteBlockDelayMs", ref emoteBlockDelayMs, 50, 100);

        ImGui.SameLine();
        ImGui.TextDisabled("(ms)");

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            emoteBlockDelayMs = Math.Clamp(emoteBlockDelayMs, 0, 10000);

            plugin.Configuration.EmoteBlockDelayMs = emoteBlockDelayMs;
            plugin.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Jump key
        // ----------------------------------------

        ImGui.Text("Jump Key to be blocked");
        ImGui.SetNextItemWidth(100);

        if (ImGui.InputText(
                "##JumpKey",
                ref jumpKeyText,
                2,
                ImGuiInputTextFlags.CharsHexadecimal |
                ImGuiInputTextFlags.CharsUppercase))
        {
            jumpKeyValid =
                jumpKeyText.Length == 0 ||
                TryParseJumpKey(jumpKeyText, out _);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (jumpKeyValid &&
                TryParseJumpKey(jumpKeyText, out var jumpKey))
            {
                plugin.Configuration.JumpKey = jumpKey;
                plugin.SaveConfiguration();
            }
            else
            {
                jumpKeyText = plugin.Configuration.JumpKey.ToString(
                    "X2",
                    CultureInfo.InvariantCulture);

                jumpKeyValid = true;
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(hex, 20 = Space)");

        if (!jumpKeyValid)
        {
            ImGui.TextColored(
                ErrorColor,
                "Enter a hex value between 00 and FF.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Reset to defaults
        // ----------------------------------------

        if (ImGui.Button("Reset to defaults"))
        {
            plugin.Configuration.JumpKey = 0x20;
            plugin.Configuration.BlockingStatusId = Guid.Empty;
            plugin.Configuration.EmoteBlockDelayMs = 500;

            plugin.SaveConfiguration();
            SyncFromConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ----------------------------------------
        // Active status
        // ----------------------------------------

        if (plugin.IsBlockingActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ActiveColor);

            ImGui.TextWrapped(
                "ACTIVE - Jump and emotes are currently blocked.");

            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.TextDisabled(
                "Inactive - Jump and emotes are allowed.");
        }
    }

    private static bool TryParseJumpKey(string text, out int value)
    {
        var ok = int.TryParse(
            text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);

        return ok && value is >= 0 and <= 0xFF;
    }
}