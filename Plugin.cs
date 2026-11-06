using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace NameplateOffset;

/// <summary>
///     Raises the in-world nameplate of a game object by adding Offset / Scale to
///     GameObject.NameplateOffsetScaleMultiplier, the plate-height term the client never interpolates.
///     The plate is anchored at its bottom-centre, so a positive Y raises the whole plate.
/// </summary>
public sealed unsafe class Plugin : IDalamudPlugin
{
    private const string CommandName = "/npoffset";

    private readonly IPluginLog log;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IUiBuilder uiBuilder;
    private readonly ICommandManager commandManager;

    /// <summary>Per-object baseline (the value the game itself wanted) plus what we last wrote.</summary>
    private readonly Dictionary<nint, OffsetRules.PlateState> tracked = [];

    private Configuration config;
    private bool windowOpen;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IObjectTable objectTable,
        IFramework framework,
        ICommandManager commandManager)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.objectTable = objectTable;
        this.framework = framework;
        this.uiBuilder = pluginInterface.UiBuilder;
        this.commandManager = commandManager;

        this.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // A hand-edited config can hold NaN/Infinity or an out-of-range value.
        this.config.Offset = OffsetRules.Sanitize(this.config.Offset);

        this.uiBuilder.Draw += this.DrawConfigWindow;
        this.uiBuilder.OpenConfigUi += this.OpenConfig;
        this.framework.Update += this.OnFrameworkUpdate;

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the nameplate height window, or: /npoffset <yalms>  e.g. /npoffset 0.35",
        });
    }

    public string Name => "Nameplate Offset";

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        this.uiBuilder.Draw -= this.DrawConfigWindow;
        this.uiBuilder.OpenConfigUi -= this.OpenConfig;
        this.commandManager.RemoveHandler(CommandName);

        // Put every plate back where the game wanted it.
        this.RestoreAll();
    }

    private void OpenConfig() => this.windowOpen = true;

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Length == 0)
        {
            this.windowOpen = !this.windowOpen;
            return;
        }

        if (!OffsetRules.TryParse(trimmed, out var value))
        {
            this.log.Warning("Could not parse '{0}' as a number of yalms.", trimmed);
            return;
        }

        this.config.Offset = value;
        this.pluginInterface.SavePluginConfig(this.config);
        this.RestoreAll();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            this.UpdatePlates();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Nameplate offset update failed.");
        }
    }

    private void UpdatePlates()
    {
        var delta = this.config.Offset;
        var localAddress = this.objectTable.LocalPlayer?.Address ?? 0;

        if (delta == 0f && this.tracked.Count == 0)
        {
            return;
        }

        var seen = 0;

        foreach (var obj in this.objectTable)
        {
            if (obj is null || obj.Address == 0)
            {
                continue;
            }

            if (!this.Applies(obj, obj.Address == localAddress))
            {
                continue;
            }

            seen++;

            var address = obj.Address;
            var native = (GameObject*)address;

            // The lift is divided by Scale below; a zero/garbage scale would turn it into NaN or a launch.
            if (!(native->Scale > 0.01f) || !float.IsFinite(native->Scale))
            {
                continue;
            }

            if (!this.tracked.TryGetValue(address, out var entry))
            {
                if (delta == 0f)
                {
                    continue;
                }

                entry = new OffsetRules.PlateState();
                this.tracked[address] = entry;
            }

            // Plate Y = Position.Y + NameplateOffset.Y + NameplateOffsetScaleMultiplier * Scale
            // (GetNamePlateWorldPosition). The client re-sets NameplateOffset/Target inside its own tick, after
            // IFramework.Update, so a lift written there loses on those frames (the jitter after stopping a run).
            // The multiplier is set at model setup (SetupRaw) and never interpolated, so the lift lives there.
            if (!entry.TryAdopt(native->NameplateOffsetScaleMultiplier))
            {
                continue;
            }

            if (delta == 0f)
            {
                this.tracked.Remove(address);
                continue;
            }

            var target = entry.BaselineY + (delta / native->Scale);
            native->NameplateOffsetScaleMultiplier = target;

            entry.WrittenY = target;
            entry.Adopted = true;
        }

        // Drop bookkeeping for objects that left the scene.
        if (this.tracked.Count > seen)
        {
            var stale = new List<nint>();
            foreach (var address in this.tracked.Keys)
            {
                var stillHere = false;
                foreach (var obj in this.objectTable)
                {
                    if (obj?.Address == address)
                    {
                        stillHere = true;
                        break;
                    }
                }

                if (!stillHere)
                {
                    stale.Add(address);
                }
            }

            foreach (var address in stale)
            {
                this.tracked.Remove(address);
            }
        }
    }

    private bool Applies(IGameObject obj, bool isLocal)
    {
        if (isLocal)
        {
            return this.config.IncludeSelf;
        }

        return obj switch
        {
            IPlayerCharacter => this.config.IncludePlayers,
            _ => this.config.IncludeOthers,
        };
    }

    private void RestoreAll()
    {
        // Never dereference an address the object table no longer lists: it may have been freed.
        var live = new HashSet<nint>();
        foreach (var obj in this.objectTable)
        {
            if (obj is not null)
            {
                live.Add(obj.Address);
            }
        }

        foreach (var (address, entry) in this.tracked)
        {
            if (!live.Contains(address))
            {
                continue;
            }

            ((GameObject*)address)->NameplateOffsetScaleMultiplier = entry.BaselineY;
        }

        this.tracked.Clear();
    }

    private void DrawConfigWindow()
    {
        if (!this.windowOpen)
        {
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(340, -1), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nameplate Offset###NameplateOffset", ref this.windowOpen))
        {
            ImGui.End();
            return;
        }

        ImGui.TextWrapped("Raises the plate above your head. 1 yalm = 1 in-game metre.");
        ImGui.Spacing();

        var changed = false;

        var offset = this.config.Offset;
        if (ImGui.SliderFloat("Height (yalms)", ref offset, -1.5f, 3.0f, "%+.2f"))
        {
            this.config.Offset = Math.Clamp(offset, -1.5f, 3.0f);
            changed = true;
        }

        ImGui.Spacing();

        ImGui.TextDisabled("Apply to:");
        var includeSelf = this.config.IncludeSelf;
        if (ImGui.Checkbox("My character", ref includeSelf))
        {
            this.config.IncludeSelf = includeSelf;
            changed = true;
        }

        var includePlayers = this.config.IncludePlayers;
        if (ImGui.Checkbox("Other players", ref includePlayers))
        {
            this.config.IncludePlayers = includePlayers;
            changed = true;
        }

        var includeOthers = this.config.IncludeOthers;
        if (ImGui.Checkbox("NPCs / minions / objects", ref includeOthers))
        {
            this.config.IncludeOthers = includeOthers;
            changed = true;
        }

        if (changed)
        {
            this.pluginInterface.SavePluginConfig(this.config);
            this.RestoreAll();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("/npoffset <yalms>  |  /npoffset 0 to reset");

        ImGui.End();
    }
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>How far up to lift the plate, in yalms.</summary>
    public float Offset { get; set; } = 0.35f;

    public bool IncludeSelf { get; set; } = true;

    public bool IncludePlayers { get; set; }

    public bool IncludeOthers { get; set; }
}
