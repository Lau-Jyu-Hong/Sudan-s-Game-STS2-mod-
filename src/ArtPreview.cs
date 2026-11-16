using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace ArtuMod;

internal static class ArtPreview
{
    internal static string? Existing(string key)
    {
        string path = $"res://ArtuMod/images/art_preview/{key}.ctex";
        return ResourceLoader.Exists(path) ? path : null;
    }

    internal static string? Scene(string key)
    {
        string path = $"res://ArtuMod/scenes/artu_art_preview_{key}.tscn";
        return ResourceLoader.Exists(path) ? path : null;
    }

    internal static Texture2D? Texture(string key) =>
        Existing(key) is {} path ? ResourceLoader.Load<Texture2D>(path) : null;
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class FitArtuSelectBackground
{
    private static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton)
    {
        if (charSelectButton.Character is not Artu ||
            __instance.GetNodeOrNull<Control>("AnimatedBg") is not {} host ||
            host.GetNodeOrNull<TextureRect>(charSelectButton.Character.Id.Entry+"_bg") is not {} art) return;
        // Native AnimatedBg reserves overscan for the base character scenes.
        // Fit the flat illustrated backdrop to the real viewport, not that overscan box.
        var viewport=art.GetViewport();
        void Fit()
        {
            var inverse=host.GetGlobalTransformWithCanvas().AffineInverse();
            var rect=viewport.GetVisibleRect();
            var start=inverse*rect.Position;
            var end=inverse*rect.End;
            art.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            art.Position=start;
            art.Size=end-start;
        }
        Fit();
        viewport.SizeChanged+=Fit;
        art.TreeExiting+=()=>viewport.SizeChanged-=Fit;
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class SultanFullArtLayout
{
    // This method is visual only. Native card reuse resets visibility and text first.
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() ||
            __instance.GetNodeOrNull<TextureRect>("%AncientPortrait") is not {} full ||
            __instance.GetNodeOrNull<TextureRect>("%Portrait") is not {} old ||
            __instance.GetNodeOrNull<TextureRect>("%PortraitBorder") is not {} border ||
            __instance.GetNodeOrNull<TextureRect>("%Frame") is not {} frame) return;

        // Don't show the special art when the ordinary portrait has been hidden or blurred.
        if (__instance.Model is not SultanCard card || !old.Visible || old.Material != null ||
            ArtPreview.Texture(card.GetType().Name.ToLowerInvariant()+"_full") is not {} art) return;
        full.Texture = art;
        full.Show();
        old.Hide();
        border.Hide();
        frame.Hide();
        // Keep the ordinary banner, labels, energy, targeting and enchantment visuals.
        // The lower half of the outpainted texture already provides a dark copy-safe area.
    }
}

[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
internal static class ArtuEnergyCounterArt
{
    private static void Postfix(NEnergyCounter __instance, Player ____player)
    {
        if (____player.Character is not Artu || ArtPreview.Texture("energy") is not {} texture ||
            __instance.GetNodeOrNull<Control>("%Layers") is not {} layers ||
            __instance.GetNodeOrNull<Control>("Label") is not {} label) return;
        // Draw behind the native label and leave its energy events and tooltip intact.
        var emblem = new TextureRect
        {
            Name = "ArtuEnergyEmblem", Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddChild(emblem);
        emblem.ShowBehindParent = true;
        emblem.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        emblem.OffsetLeft = -55; emblem.OffsetTop = -42;
        emblem.OffsetRight = 55; emblem.OffsetBottom = 42;
        layers.Hide();
        // Reuse the normal count; also use it to darken the new graphic at zero energy.
        if (____player.PlayerCombatState is {} combat)
        {
            void Refresh() => emblem.Modulate = combat.Energy == 0 ? new Color("605253") : Colors.White;
            void EnergyChanged(int _, int __) => Refresh();
            // Counter lifetime is shorter than the combat model on room transitions.
            combat.EnergyChanged += EnergyChanged;
            emblem.TreeExiting += () => combat.EnergyChanged -= EnergyChanged;
            Refresh();
        }
    }
}
