// 配置窗口的 UI 绘制与交互逻辑。
// 负责按钮、提示与列表区域的渲染。
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using static ReMakePlacePlugin.Gui.UiHelpers;
using static ReMakePlacePlugin.ReMakePlacePlugin;

namespace ReMakePlacePlugin.Gui;

public class ConfigurationWindow : Window, IDisposable
{
    private string CustomTag = string.Empty;
    private readonly Dictionary<uint, uint> iconToFurniture = new() { };

    private readonly Vector4 PURPLE = new(0.26275f, 0.21569f, 0.56863f, 1f);
    private readonly Vector4 PURPLE_ALPHA = new(0.26275f, 0.21569f, 0.56863f, 0.5f);

    private FileDialogManager FileDialogManager { get; }

    // 上一帧鼠标悬停的物品 ID，在 Update 中统一显示/隐藏游戏原生物品悬浮窗
    private uint hoveredItemId;
    private bool isItemTooltipShown;

    public Configuration Config { get; init; }
    public ReMakePlacePlugin Plugin { get; init; }

    public ConfigurationWindow(ReMakePlacePlugin plugin)
        : base($"ReMakePlace v{Assembly.GetExecutingAssembly().GetName().Version}###ReMakePlacePlugin", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Plugin = plugin;
        Config = plugin.Config;
        FileDialogManager = new FileDialogManager()
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
        };
    }

    public void Dispose()
    {
        HideItemTooltip();
    }

    private void SafeMatch()
    {
        if (Memory.Instance.IsHousingMode())
            Plugin.MatchLayout();
    }

    public override void Draw()
    {
        SafeMatch();
        DrawScreen();

        Vector2 leftPanelSize = new Vector2(140 * ImGuiHelpers.GlobalScale, ImGui.GetWindowHeight() - 30 * ImGuiHelpers.GlobalScale);

        using (ImRaii.Child("LeftFloat", leftPanelSize, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var availX = ImGui.GetContentRegionAvail().X;
            string pluginDir = Svc.PluginInterface.AssemblyLocation.DirectoryName!;
            var imagePath = Path.Combine(pluginDir, "images/icon.png");
            var image = Svc.Texture.GetFromFile(imagePath).GetWrapOrDefault();
            if (image != null)
                ImGui.Image(image.Handle, availX.ToVector2());

            using (ImRaii.Child("MenuAndSettings", ImGui.GetContentRegionAvail()))
            {
                DrawMainMenu();
                DrawGeneralSettings();
            }
        }

        ImGui.SameLine();

        ImGui.BeginChild("RightFloat", border: true);
        ImGui.Text("当前文件位置:"); ImGui.SameLine();

        var saveLocation = Config.SaveLocation;
        string saveLocationLabel;
        if (string.IsNullOrEmpty(saveLocation))
            saveLocationLabel = "未选择文件";
        else
            saveLocationLabel = saveLocation;

        ImGui.Selectable(saveLocationLabel, false, ImGuiSelectableFlags.Disabled);
        ImGui.Text("注意：缺失物品、染色不匹配或未选楼层的物品会显示为灰色");
        DrawItemListRegion();
        ImGui.EndChild();
        this.FileDialogManager.Draw();
    }

    public override void Update()
    {
        // 窗口折叠时不会调用 Draw，hoveredItemId 保持为 0，悬浮窗随之隐藏
        if (Config.ShowItemTooltip && hoveredItemId != 0)
            ShowItemTooltip(hoveredItemId);
        else
            HideItemTooltip();
        hoveredItemId = 0;
    }

    public override void OnClose()
    {
        HideItemTooltip();
    }

    public override void PreDraw()
    {
        base.PreDraw();
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, PURPLE);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PURPLE_ALPHA);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, PURPLE_ALPHA);
        ImGui.SetNextWindowSize(new Vector2(680, 550), ImGuiCond.FirstUseEver);
    }

    public override void PostDraw()
    {
        base.PostDraw();
        ImGui.PopStyleColor(3);
        ImGui.End();
    }

    #region Basic UI

    private void TrackItemHover(uint itemId)
    {
        if (itemId != 0 && ImGui.IsItemHovered())
            hoveredItemId = itemId;
    }

    private static void CopyNameOnClick(string name)
    {
        if (!ImGui.IsItemHovered()) return;

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsItemClicked()) return;

        ImGui.SetClipboardText(name);
        Svc.NotificationManager.AddNotification(new Notification
        {
            Content = $"已复制：{name}",
            Type = NotificationType.Success,
        });
    }

    private unsafe void ShowItemTooltip(uint itemId)
    {
        var screenText = (AtkUnitBase*)Svc.GameGui.GetAddonByName("_ScreenText").Address;
        if (screenText == null) return;

        var args = stackalloc AtkTooltipManager.AtkTooltipArgs[1];
        args->Ctor();
        args->ItemArgs.Kind = DetailKind.ItemId;
        args->ItemArgs.ItemId = (int)itemId;
        AtkStage.Instance()->TooltipManager.ShowTooltip(AtkTooltipType.Item, screenText->Id, screenText->RootNode, args);
        isItemTooltipShown = true;
    }

    private unsafe void HideItemTooltip()
    {
        if (!isItemTooltipShown) return;
        isItemTooltipShown = false;

        var screenText = (AtkUnitBase*)Svc.GameGui.GetAddonByName("_ScreenText").Address;
        if (screenText != null)
            AtkStage.Instance()->TooltipManager.HideTooltip(screenText->Id);
    }

    private void LogLayoutMode()
    {
        if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Island)
        {
            LogError("（菜单：管理家具 -> 放置家具幻化）");
        }
        else
        {
            LogError("（菜单：房屋 -> 室内/室外家具）");
        }
    }

    private bool CheckModeForSave()
    {
        if (Memory.Instance.IsHousingMode()) return true;

        LogError("未处于布局模式，无法保存布局");
        LogLayoutMode();
        return false;
    }

    private bool CheckModeForLoad(bool ApplyLayout = false)
    {
        if (!Memory.Instance.IsHousingMode())
        {
            if (ApplyLayout)
                LogError("未处于旋转布局模式，无法加载并应用布局");

            return false;
        }

        // 仅加载/匹配不需要特定模式；自动摆放必须处于旋转模式。
        if (ApplyLayout && !Memory.Instance.CanEditItem())
        {
            LogError($"未处于旋转布局模式，无法加载并应用布局（当前模式：{LayoutModeName()}）");
            LogError("请先进入家具布置，并在布置菜单中点击“旋转”后再应用");
            LogLayoutMode();
            return false;
        }

        return true;
    }

    private bool CheckModeForApplyDyes()
    {
        if (!Memory.Instance.IsHousingMode() || !Memory.Instance.CanDyeItem())
        {
            LogError($"未处于家具染色模式，无法加载并应用染色（当前模式：{LayoutModeName()}）");
            LogError("请先进入家具布置，并在布置菜单中点击“家具染色”后再应用");
            LogLayoutMode();
            return false;
        }

        return true;
    }

    private static string LayoutModeName() => Memory.Instance.GetLayoutMode() switch
    {
        HousingLayoutMode.None => "未在布置",
        HousingLayoutMode.Move => "移动",
        HousingLayoutMode.Rotate => "旋转",
        HousingLayoutMode.Store => "收纳",
        HousingLayoutMode.Place => "放置",
        HousingLayoutMode.FurnishingColor => "家具染色",
        HousingLayoutMode.Remove => "移除",
        var mode => mode.ToString(),
    };

    private void SaveLayoutToFile()
    {
        if (!CheckModeForSave()) return;

        try
        {
            Plugin.GetGameLayout();
            ReMakePlacePlugin.LayoutManager.ExportLayout();
        }
        catch (Exception e)
        {
            LogError($"保存失败: {e.Message}", e.StackTrace);
        }
    }

    private void LoadLayoutFromFile(bool ApplyLayout = false)
    {
        var path = Config.SaveLocation;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                SaveLayoutManager.ImportLayout(path);
                Log(String.Format("已导入 {0} 个物品", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForLoad(ApplyLayout))
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    if (ApplyLayout)
                        Plugin.ApplyLayout();
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"加载失败: {e.Message}", e.StackTrace);
            }
        }
    }

    private void ApplyDyesFromFile()
    {
        var path = Config.SaveLocation;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                SaveLayoutManager.ImportLayout(path);
                Log(String.Format("已导入 {0} 个物品", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForApplyDyes())
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    Plugin.ApplyDyes();
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"应用染色失败: {e.Message}", e.StackTrace);
            }
        }
    }

    private void PlaceItemsFromFile()
    {
        var path = Config.SaveLocation;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                SaveLayoutManager.ImportLayout(path);
                Log(String.Format("已导入 {0} 个物品", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForLoad())
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    Plugin.ApplyLayout(true);
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"摆放物品失败: {e.Message}", e.StackTrace);
            }
        }
    }

    private void DrawItemListRegion()
    {
        ImGui.BeginChild("ItemListRegion");
        ImGui.PushStyleColor(ImGuiCol.Header, PURPLE_ALPHA);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, PURPLE);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, PURPLE);

        var furnitureSections = new List<(string label, List<HousingItem> items, List<Fixture> fixtures, bool unused)>
            {
                ("Interior",Plugin.InteriorItemList, Plugin.Layout.interiorFixture, false),
                ("Exterior",Plugin.ExteriorItemList, Plugin.Layout.exteriorFixture, false),
                ("Unused", Plugin.UnusedItemList, new List<Fixture>{ }, true)
            };

        foreach (var section in furnitureSections)
        {
            ImGui.PushID(section.label);

            var displayLabel = section.label switch
            {
                "Interior" => "室内",
                "Exterior" => "室外",
                "Unused" => "未使用",
                _ => section.label
            };

            if (ImGui.CollapsingHeader($"{displayLabel} 家具 ({GetItemCountText(section.items, section.unused)})###Furniture", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (section.unused) { DrawItemList(section.items, true); }
                else { DrawItemList(section.items, false, $"{displayLabel}家具"); }
            }

            if (!section.unused)
            {
                if (ImGui.CollapsingHeader($"{displayLabel} 固定装置 ({section.fixtures.Count})###Fixtures", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    DrawFixtureList(section.fixtures);
                }
            }
            ImGui.PopID();
        }
        ImGui.PopStyleColor(3);
        ImGui.EndChild();
    }

    // 匹配：场景中已摆放并关联上的物品；就位：位置、旋转、染色/材质都已正确
    private static string GetItemCountText(List<HousingItem> items, bool unused)
    {
        if (unused) return items.Count.ToString();

        int matched = 0, placed = 0;
        foreach (var item in items)
        {
            if (item.ItemStruct == IntPtr.Zero) continue;
            matched++;
            if (item.CorrectLocation && item.CorrectRotation && item.DyeMatch) placed++;
        }
        return $"就位 {placed} / 匹配 {matched} / 共 {items.Count}";
    }

    unsafe private void DrawGeneralSettings()
    {
        if (ImGui.Checkbox("选择上一次染料", ref Config.SelectPreviousDye)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("打开家具染色菜单时自动选择上次使用的染料\n仍需你确认");

        if (ImGui.Checkbox("自动确认染色", ref Config.AutoConfirmDye)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("染色时自动按“是”");

        if (ImGui.Checkbox("允许稀有染料", ref Config.UseRareStains)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("允许使用稀有染料（如纯白、煤黑等）");

        //ImGui.BeginChild("SettingsPanel", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeightWithSpacing() * 7));
        if (ImGui.Checkbox("标注家具", ref Config.DrawScreen)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("在屏幕上显示家具名称");

        //ImGui.SameLine();ImGui.Dummy(new Vector2(10, 0));ImGui.SameLine();

        if (ImGui.Checkbox("显示物品详情", ref Config.ShowItemTooltip)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                ImGui.Text("鼠标悬停在物品列表上时显示游戏原生的物品详情");
                ImGui.Text("配合 Daily Routines「更好的市场布告板」可显示市场价格");
            }
        }

        if (ImGui.Checkbox("显示提示", ref Config.ShowTooltips)) Config.Save();

        bool hasFloors = false;
        try
        {
            hasFloors = Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors && !Memory.Instance.GetIndoorHouseSize().Equals("Apartment");
        }
        catch (NullReferenceException)
        {
            // Thanks zbee
        }

        DrawMainMenuButton("TeamCraft 导出", () =>
        {
            var allItemsList = new Dictionary<string, int>();
            for (int i = 0; i < Plugin.InteriorItemList.Count(); i++)
            {
                var itemId = Plugin.InteriorItemList[i].ItemKey.ToString();
                if (allItemsList.ContainsKey(itemId))
                {
                    allItemsList[itemId]++;
                }
                else
                {
                    allItemsList.Add(itemId, 1);
                }
            }
            for (int i = 0; i < Plugin.ExteriorItemList.Count(); i++)
            {
                var itemId = Plugin.ExteriorItemList[i].ItemKey.ToString();
                if (allItemsList.ContainsKey(itemId))
                {
                    allItemsList[itemId]++;
                }
                else
                {
                    allItemsList.Add(itemId, 1);
                }
            }
            Utils.TeamcraftExport(allItemsList);
        },
        string.IsNullOrEmpty(Config.SaveLocation),
        "生成可导入 TeamCraft 的列表链接",
        ImGui.GetContentRegionAvail().X);
        if (string.IsNullOrEmpty(Config.SaveLocation))
        {
            if (Config.ShowTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("没有可导出的文件");
            }
        }

        DrawMainMenuButton("同步到 GBR", () =>
        {
            var listName = $"ReMakePlace - {Path.GetFileNameWithoutExtension(Config.SaveLocation)}";
            var itemIds = Plugin.InteriorItemList.Concat(Plugin.ExteriorItemList).Select(item => item.ItemKey);
            GatherBuddySync.Sync(listName, itemIds);
        },
        string.IsNullOrEmpty(Config.SaveLocation),
        "将布局所需家具同步到 GatherBuddy Reborn：可制作的物品生成制作清单，其余物品生成商店购买清单",
        ImGui.GetContentRegionAvail().X);

        ImGui.Text("摆放间隔");

        ImGui.Dummy(new Vector2(5, 0)); ImGui.SameLine();
        ImGui.PushItemWidth(60);
        if (ImGui.InputInt("毫秒", ref Config.LoadInterval))
        {
            Config.Save();
        }
        ImGui.PopItemWidth();
        if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("应用布局时两次摆放之间的时间间隔。若过低（如 200ms），可能会漏摆放。");

        ImGui.Dummy(new Vector2(10, 0));

        if (hasFloors)
        {
            ImGui.Text("启用楼层");
            float height = ImGui.GetFrameHeightWithSpacing() * 3 + ImGui.GetStyle().WindowPadding.Y;
            float width = 120;
            ImGui.BeginChild("FloorSelection", new Vector2(width, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            //if (ImGui.CollapsingHeader("Enabled Floors")){
            if (Memory.Instance.HasUpperFloor() && ImGui.Checkbox("上层", ref Config.UpperFloor)) Config.Save();
            if (ImGui.Checkbox("一层", ref Config.GroundFloor)) Config.Save();
            if (ImGui.Checkbox("地下室", ref Config.Basement)) Config.Save();
            //}
            ImGui.EndChild();
        }
        //ImGui.EndChild();
    }

    unsafe private void DrawMainMenu()
    {
        Vector2 menuDimensions = ImGui.GetContentRegionAvail();

        DrawMainMenuButton("打开文件", () =>
        {
            var currentPath = Config.SaveLocation;
            var saveName = string.IsNullOrEmpty(currentPath)
                ? "save"
                : Path.GetFileNameWithoutExtension(currentPath);

            FileDialogManager.OpenFileDialog("选择布局文件", ".json", (ok, res) =>
            {
                if (!ok) return;
                Config.SaveLocation = res.FirstOrDefault("");
                Config.Save();
                LoadLayoutFromFile();
            }, 1, Path.GetDirectoryName(Config.SaveLocation ?? string.Empty));
        },
        false,
        "选择要打开的文件",
        menuDimensions.X);

        DrawMainMenuButton("应用布局", () =>
        {
            Config.Save();
            LoadLayoutFromFile(true);
        },
        string.IsNullOrEmpty(Config.SaveLocation),
        "从当前文件位置尝试应用布局",
        menuDimensions.X);

        var ctrlKeyPressed = ImGui.GetIO().KeyCtrl;
        var dyeingItems = ReMakePlacePlugin.CurrentlyDyeingItems;

        DrawMainMenuButton(dyeingItems ? "停止染色" : "应用染色", () =>
        {
            if (dyeingItems)
            {
                Plugin.StopDyeingItems();
            }
            else
            {
                Config.Save();
                ApplyDyesFromFile();
            }
        },
        dyeingItems ? false : (string.IsNullOrEmpty(Config.SaveLocation) || !ctrlKeyPressed),
        dyeingItems ? "将停止对家具进行染色" :
            (ctrlKeyPressed ? "尝试应用染色，需要打开“家具染色”窗口" : "按住 CTRL 以应用染色"),
        menuDimensions.X);

        //DrawMainMenuButton("Place Items Down", () =>
        //{
        //    Config.Save();
        //    Log($"Number of Items interior {Plugin.InteriorItemList.Count}");
        //    //ReMakePlacePlugin.PlaceItemInternal();
        //    //PlaceItemsFromFile();
        //},
        //Config.SaveLocation.IsNullOrEmpty(),
        //"Tries to place items from Inventory & Storage",
        //menuDimensions.X);

        DrawMainMenuButton("另存为", () =>
        {
            if (CheckModeForSave())
            {
                var currentPath = Config.SaveLocation;
                var saveName = string.IsNullOrEmpty(currentPath)
                    ? "save"
                    : Path.GetFileNameWithoutExtension(currentPath);

                FileDialogManager.SaveFileDialog("选择保存位置", ".json", saveName, "json", (ok, res) =>
                {
                    if (!ok) return;
                    Config.SaveLocation = res;
                    Config.Save();
                    SaveLayoutToFile();
                }, Path.GetDirectoryName(Config.SaveLocation ?? string.Empty));
            }
        },
        false,
        "将布局保存到新文件位置",
        menuDimensions.X);

        DrawMainMenuButton("保存",
            SaveLayoutToFile,
            string.IsNullOrEmpty(Config.SaveLocation),
            "将布局保存到当前文件位置",
            menuDimensions.X);
    }

    private void DrawMainMenuButton(string label, System.Action onClick, bool disabled = false, string? tooltip = null, float width = 100)
    {
        float height = ImGui.GetFrameHeight();

        ImGui.BeginDisabled(disabled);
        if (ImGui.Button(label, new Vector2(width, height)) && !disabled)
            onClick();
        ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltip);
    }


    private void DrawRow(int i, HousingItem housingItem, bool showSetPosition = true, int childIndex = -1)
    {
        ImGui.TableNextColumn();
        if (showSetPosition)
        {
            string uniqueID = childIndex == -1 ? i.ToString() : i.ToString() + "_" + childIndex.ToString();
            bool noMatch = housingItem.ItemStruct == IntPtr.Zero;

            if (!noMatch)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0.0f, 0.0f));
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                var buttonWidth = ImGui.GetContentRegionAvail().X;
                if (ImGui.Button("设置##" + uniqueID, new Vector2(buttonWidth, 0)))
                {
                    Plugin.MatchLayout();

                    if (housingItem.ItemStruct != IntPtr.Zero)
                    {
                        SetItemPosition(housingItem);
                    }
                    else
                    {
                        LogError($"无法为 {housingItem.Name} 设置位置");
                    }
                }
                ImGui.PopStyleVar(2);
            }
            ImGui.TableNextColumn();
        }

        var displayName = housingItem.Name;

        if (Svc.Data.GetExcelSheet<Item>().TryGetRow(housingItem.ItemKey, out var item))
        {
            DrawIcon(item.Icon, new Vector2(20, 20));
            TrackItemHover(housingItem.ItemKey);
            CopyNameOnClick(displayName);
            ImGui.SameLine();
        }

        if (housingItem.ItemStruct == IntPtr.Zero)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        }
        ImGui.Text(displayName);
        TrackItemHover(housingItem.ItemKey);
        CopyNameOnClick(displayName);
        ImGui.TableNextColumn();

        if (!housingItem.CorrectLocation) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        string posText = $"{housingItem.X:0.0###}, {housingItem.Y:0.0###}, {housingItem.Z:0.0###}";
        float posX = (ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(posText).X - ImGui.GetScrollX());
        if (posX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(posX);
        ImGui.Text(posText);
        if (!housingItem.CorrectLocation) ImGui.PopStyleColor();
        ImGui.TableNextColumn();

        if (!housingItem.CorrectRotation) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        string rotateText = $"{Utils.radToDeg(housingItem.Rotate):0.00##}";
        posX = (ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(rotateText).X - ImGui.GetScrollX());
        if (posX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(posX);
        ImGui.Text(rotateText);
        if (!housingItem.CorrectRotation) ImGui.PopStyleColor();
        ImGui.TableNextColumn();

        var stain = Svc.Data.GetExcelSheet<Stain>().GetRowOrDefault(housingItem.Stain);

        if (housingItem.Stain != 0)
        {
            if (stain.HasValue)
            {
                Utils.StainButton("dye_" + i, stain.Value, new Vector2(20));
                ImGui.SameLine();

                if (!housingItem.DyeMatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                ImGui.Text(stain.Value.Name.ToString());
                if (!housingItem.DyeMatch) ImGui.PopStyleColor();
            }
            else
            {
                // Extremely defensive: if the row can't be found, at least show the id.
                ImGui.Text($"Dye #{housingItem.Stain}");
            }
        }
        else if (housingItem.MaterialItemKey != 0)
        {
            if (Svc.Data.GetExcelSheet<Item>().TryGetRow(housingItem.MaterialItemKey, out var mitem))
            {
                if (!housingItem.DyeMatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                DrawIcon(mitem.Icon, new Vector2(20, 20));
                TrackItemHover(housingItem.MaterialItemKey);
                ImGui.SameLine();
                ImGui.Text(mitem.Name.ToString());
                TrackItemHover(housingItem.MaterialItemKey);
                if (!housingItem.DyeMatch) ImGui.PopStyleColor();
            }
        }
    }

    private void DrawFixtureList(List<Fixture> fixtureList)
    {
        try
        {
            if (ImGui.Button("清空"))
            {
                fixtureList.Clear();
                Config.Save();
            }

            if (ImGui.BeginTable("FixtureList", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("等级", ImGuiTableColumnFlags.None, 2);
                ImGui.TableSetupColumn("装置", ImGuiTableColumnFlags.None, 1);
                ImGui.TableSetupColumn("物品", ImGuiTableColumnFlags.None, 5);
                ImGui.TableHeadersRow();

                foreach (var fixture in fixtureList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(fixture.level);

                    ImGui.TableNextColumn();
                    ImGui.Text(fixture.type);

                    ImGui.TableNextColumn();
                    if (Svc.Data.GetExcelSheet<Item>().TryGetRow(fixture.itemId, out var item))
                    {
                        DrawIcon(item.Icon, new Vector2(20, 20));
                        TrackItemHover(fixture.itemId);
                        ImGui.SameLine();
                    }
                    ImGui.Text(fixture.name);
                    TrackItemHover(fixture.itemId);
                }

                ImGui.EndTable();
            }
        }
        catch (Exception e)
        {
            LogError(e.Message, e.StackTrace);
        }

    }

    private void DrawItemList(List<HousingItem> itemList, bool isUnused = false, string? syncLabel = null)
    {
        if (ImGui.Button("排序"))
        {
            itemList.Sort((x, y) =>
            {
                if (x.Name.CompareTo(y.Name) != 0)
                    return x.Name.CompareTo(y.Name);
                if (x.X.CompareTo(y.X) != 0)
                    return x.X.CompareTo(y.X);
                if (x.Y.CompareTo(y.Y) != 0)
                    return x.Y.CompareTo(y.Y);
                if (x.Z.CompareTo(y.Z) != 0)
                    return x.Z.CompareTo(y.Z);
                if (x.Rotate.CompareTo(y.Rotate) != 0)
                    return x.Rotate.CompareTo(y.Rotate);
                return 0;
            });
            Config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("清空"))
        {
            itemList.Clear();
            Config.Save();
        }
        ImGui.SameLine();
        if (IconTextButton(FontAwesomeIcon.SyncAlt, "刷新"))
        {
            LoadLayoutFromFile();
        }

        if (syncLabel != null)
        {
            ImGui.SameLine();
            var noFile = string.IsNullOrEmpty(Config.SaveLocation);
            ImGui.BeginDisabled(noFile);
            if (ImGui.Button("同步到 GBR") && !noFile)
            {
                // 用独立的清单名，避免覆盖整体同步生成的清单
                var listName = $"ReMakePlace - {Path.GetFileNameWithoutExtension(Config.SaveLocation)} - {syncLabel}";
                GatherBuddySync.Sync(listName, itemList.Select(item => item.ItemKey));
            }
            ImGui.EndDisabled();
            if (Config.ShowTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(noFile ? "没有可同步的文件" : $"只将{syncLabel}同步到 GatherBuddy Reborn，生成单独的制作清单和商店购买清单");
        }

        // name, position, r, color, set
        int columns = isUnused ? 4 : 5;
        var setColumnWidth = 25f * ImGuiHelpers.GlobalScale;
        if (!isUnused)
        {
            var setLabelWidth = ImGui.CalcTextSize("设置").X + ImGui.GetStyle().FramePadding.X * 2 + 6f;
            setColumnWidth = Math.Max(setColumnWidth, setLabelWidth);
        }


        if (ImGui.BeginTable("ItemList", columns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Reorderable))
        {
            if (!isUnused)
            {
                ImGui.TableSetupColumn("设置", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, setColumnWidth);
            }

            // Stretch columns with relative weights
            ImGui.TableSetupColumn("物品", ImGuiTableColumnFlags.WidthStretch, 1.8f);           // Wider
            ImGui.TableSetupColumn("位置 (X,Y,Z)", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("旋转", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("染色/材质", ImGuiTableColumnFlags.WidthStretch, 1.0f);

            ImGui.TableHeadersRow();

            for (int i = 0; i < itemList.Count(); i++)
            {
                var housingItem = itemList[i];

                ImGui.TableNextRow();
                DrawRow(i, housingItem, !isUnused);
                if (housingItem.ItemStruct == IntPtr.Zero)
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndTable();
        }
    }

    #endregion

    #region Draw Screen

    protected void DrawScreen()
    {
        if (Config.DrawScreen)
            DrawItemOnScreen();
    }

    private unsafe void DrawItemOnScreen()
    {

        if (Memory.Instance == null) return;

        var itemList = Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors ? Plugin.InteriorItemList : Plugin.ExteriorItemList;

        for (int i = 0; i < itemList.Count(); i++)
        {
            var player = Svc.Objects.LocalPlayer;
            if (player == null) return;
            var playerPos = player.Position;
            var housingItem = itemList[i];

            if (housingItem.ItemStruct == IntPtr.Zero) continue;

            var itemStruct = (HousingItemStruct*)housingItem.ItemStruct;

            var itemPos = new Vector3(itemStruct->Position.X, itemStruct->Position.Y, itemStruct->Position.Z);
            if (Config.HiddenScreenItemHistory.IndexOf(i) >= 0) continue;
            if (Config.DrawDistance > 0 && (playerPos - itemPos).Length() > Config.DrawDistance)
                continue;
            var displayName = housingItem.Name;
            if (Svc.GameGui.WorldToScreen(itemPos, out var screenCoords))
            {
                ImGui.PushID("HousingItemWindow" + i);
                ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));
                ImGui.SetNextWindowBgAlpha(0.8f);
                if (ImGui.Begin("HousingItem" + i,
                    ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav))
                {

                    ImGui.Text(displayName);

                    ImGui.SameLine();

                    if (ImGui.Button("设置" + "##ScreenItem" + i.ToString()))
                    {
                        if (!Memory.Instance.CanEditItem())
                        {
                            LogError("未处于旋转布局模式，无法设置位置");
                            continue;
                        }

                        SetItemPosition(housingItem);
                        Config.HiddenScreenItemHistory.Add(i);
                        Config.Save();
                    }

                    ImGui.SameLine();
                    ImGui.End();
                }

                ImGui.PopID();
            }
        }
    }

    #endregion
}

