﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CraftyBoxes.Utils;
using HarmonyLib;
using UnityEngine;

namespace CraftyBoxes.Patches;

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
static class FireplaceInteractPatch
{
    static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool __result, ZNetView ___m_nview)
    {
        __result = true;
        bool pullAll = Input.GetKey(CraftyBoxesPlugin.fillAllModKey.Value.MainKey); // Used to be fillAllModKey.Value.IsPressed(); something is wrong with KeyboardShortcuts always returning false
        Inventory inventory = user.GetInventory();

        if (!CraftyBoxesPlugin.AllowByKey() || hold || inventory == null ||
            (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && !pullAll))
            return true;

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }


        if (pullAll && inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
        {
            int amount =
                (int)Mathf.Min(__instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")),
                    inventory.CountItems(__instance.m_fuelItem.m_itemData.m_shared.m_name));
            inventory.RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, amount);
            inventory.Changed();
            for (int i = 0; i < amount; i++)
                ___m_nview.InvokeRPC("AddFuel");

            user.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

            __result = false;
        }

        if (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) ||
            !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) < __instance.m_maxFuel)) return __result;
        {
            List<Container> nearbyContainers = Functions.GetNearbyContainers(__instance.transform.position);

            foreach (Container c in nearbyContainers)
            {
                ItemDrop.ItemData item = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                if (item == null ||
                    !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) < __instance.m_maxFuel)) continue;
                if (((IList)CraftyBoxesPlugin.fuelDisallowTypes.Value.Split(',')).Contains(item.m_dropPrefab.name))
                {
                    CraftyBoxesPlugin.CraftyBoxesLogger.LogDebug(
                        $"(FireplaceInteractPatch) Container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
                    continue;
                }

                int amount = pullAll
                    ? (int)Mathf.Min(
                        __instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")),
                        item.m_stack)
                    : 1;
                CraftyBoxesPlugin.CraftyBoxesLogger.LogDebug($"Pull ALL is {pullAll}");

                CraftyBoxesPlugin.CraftyBoxesLogger.LogDebug(
                    $"(FireplaceInteractPatch) Container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name}, taking {amount}");

                c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, amount);
                c.Save();
                //typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });

                if (__result)
                    user.Message(MessageHud.MessageType.Center,
                        Localization.instance.Localize("$msg_fireadding",
                            __instance.m_fuelItem.m_itemData.m_shared.m_name));

                for (int i = 0; i < amount; i++)
                    ___m_nview.InvokeRPC("AddFuel");

                __result = false;

                if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) >= __instance.m_maxFuel)
                    return false;
            }
        }

        return __result;
    }
}