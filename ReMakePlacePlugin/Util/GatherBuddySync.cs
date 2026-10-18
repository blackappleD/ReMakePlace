// 通过 GatherBuddy Reborn 的 IPC 把布局所需家具同步为制作清单与商店购买清单。
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using System.Collections.Generic;
using System.Linq;
using static ReMakePlacePlugin.ReMakePlacePlugin;

namespace ReMakePlacePlugin;

public static class GatherBuddySync
{
    private const string IpcPrefix = "GatherBuddyReborn";

    // GBR 从 IPC 版本 7 开始提供 CraftingListReplaceByName
    private const int RequiredIpcVersion = 7;

    private const int ListBusy = -3;
    private const int VendorNotReady = -4;

    public static void Sync(string listName, IEnumerable<uint> itemIds)
    {
        var items = itemIds
            .Where(id => id != 0)
            .GroupBy(id => id)
            .Select(g => (ItemId: g.Key, Quantity: (uint)g.Count()))
            .ToArray();
        if (items.Length == 0)
        {
            LogError("布局中没有可同步的物品");
            return;
        }

        try
        {
            var version = Svc.PluginInterface.GetIpcSubscriber<int>($"{IpcPrefix}.Version").InvokeFunc();
            if (version < RequiredIpcVersion)
            {
                LogError($"GatherBuddy Reborn 版本过旧（IPC {version}），请更新到支持制作清单同步的版本");
                return;
            }

            var (written, uncraftable) = Svc.PluginInterface
                .GetIpcSubscriber<string, (uint, uint)[], (int, uint[])>($"{IpcPrefix}.CraftingListReplaceByName")
                .InvokeFunc(listName, items);
            if (written == ListBusy)
            {
                LogError("GatherBuddy Reborn 正在执行制作清单队列，请停止后再同步");
                return;
            }
            if (written < 0)
            {
                LogError($"同步制作清单失败（错误码 {written}）");
                return;
            }

            var messages = new List<string> { $"制作清单 {written} 种" };

            if (uncraftable.Length > 0)
            {
                var vendorItems = items.Where(item => uncraftable.Contains(item.ItemId)).ToArray();
                var bought = Svc.PluginInterface
                    .GetIpcSubscriber<string, (uint, uint)[], int>($"{IpcPrefix}.VendorBuyListReplaceByName")
                    .InvokeFunc(listName, vendorItems);
                if (bought == ListBusy)
                    messages.Add("商店购买清单未更新：GBR 正在执行购买清单");
                else if (bought == VendorNotReady)
                    messages.Add("商店购买清单未更新：GBR 商店数据仍在加载，请稍后重试");
                else if (bought < 0)
                    messages.Add($"商店购买清单未更新（错误码 {bought}）");
                else
                {
                    messages.Add($"商店购买清单 {bought} 种");
                    if (vendorItems.Length > bought)
                        messages.Add($"另有 {vendorItems.Length - bought} 种物品既不能制作也无法从商店购买");
                }
            }

            Log($"已同步到 GatherBuddy Reborn「{listName}」：{string.Join("，", messages)}");
        }
        catch (IpcNotReadyError)
        {
            LogError("未检测到 GatherBuddy Reborn，请确认插件已安装并启用");
        }
        catch (IpcError e)
        {
            LogError($"调用 GatherBuddy Reborn 失败：{e.Message}", e.ToString());
        }
    }
}
