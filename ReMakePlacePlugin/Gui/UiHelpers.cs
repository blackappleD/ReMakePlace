using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;
using System.Numerics;

namespace ReMakePlacePlugin.Gui;

public static class UiHelpers
{
    /// <summary>
    /// Draw an icon from the Dalamud api.
    /// </summary>
    /// <param name="icon"> Item id, must be below 65000</param>
    /// <param name="size"></param>
    public static void DrawIcon(ushort icon, Vector2 size)
    {
        if (icon < 65000)
        {
            var iconTexture = Svc.Texture.GetFromGameIcon(new GameIconLookup(icon));
            ImGui.Image(iconTexture.GetWrapOrEmpty().Handle, size);
        }
    }

    /// <summary>Make a button with both an icon and text.</summary>
    public static bool IconTextButton(FontAwesomeIcon icon, string text)
    {
        var iconSize = GetIconSize(icon);
        var textSize = ImGui.CalcTextSize(text);
        var padding = ImGui.GetStyle().FramePadding;
        var spacing = ImGui.GetStyle().ItemInnerSpacing;

        var buttonSizeX = iconSize.X + textSize.X + padding.X * 2 + spacing.X;
        var contentHeight = iconSize.Y > textSize.Y ? iconSize.Y : textSize.Y;
        var buttonSizeY = contentHeight + padding.Y * 2;
        var frameHeight = ImGui.GetFrameHeight();
        if (buttonSizeY < frameHeight) buttonSizeY = frameHeight;

        var buttonClicked = ImGui.Button("###" + icon.ToIconString() + text, new Vector2(buttonSizeX, buttonSizeY));

        // 用 DrawList 绘制图标和文字，保证按钮是最后一个控件，SameLine 才能与按钮对齐
        var itemMin = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGuiCol.Text);

        ImGui.PushFont(UiBuilder.IconFont);
        drawList.AddText(new Vector2(itemMin.X + padding.X, itemMin.Y + (buttonSizeY - iconSize.Y) * 0.5f), color, icon.ToIconString());
        ImGui.PopFont();

        drawList.AddText(new Vector2(itemMin.X + padding.X + iconSize.X + spacing.X, itemMin.Y + (buttonSizeY - textSize.Y) * 0.5f), color, text);

        return buttonClicked;
    }

    /// <summary>Get the size of an icon.</summary>
    public static Vector2 GetIconSize(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(icon.ToIconString());
        ImGui.PopFont();
        return iconSize;
    }
}
