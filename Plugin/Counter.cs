using System;
using HarmonyLib;
using PixelCrushers.DialogueSystem;

namespace SpecialOrdersMod
{
	// Market stalls and furniture shop counters open their screen straight away. With CounterConversation on,
	// using one first shows a short conversation: trade (the game's screen, as before), special order, or goodbye.
	[HarmonyPatch]
	internal static class Counter
	{
		private static bool bypass;

		private static bool Busy()
		{
			try { return DialogueManager.isConversationActive; } catch (Exception) { return false; }
		}

		internal static void OpenShopScreen(object shop)
		{
			bypass = true;
			try
			{
				if (shop is Trade t) t.Interact();
				else if (shop is FurnitureShop f) f.Interact();
			}
			finally { bypass = false; }
		}

		private static string Speaker(Trade t)
		{
			try { if (t.owner != null && !string.IsNullOrEmpty(t.owner.name) && t.owner.name != "Name") return t.owner.name; } catch (Exception) { }
			return !string.IsNullOrEmpty(t.title) ? t.title : "Trader";
		}

		[HarmonyPatch(typeof(Trade), "Interact")]
		[HarmonyPrefix]
		private static bool Trade_Interact(Trade __instance)
		{
			if (bypass || !SpecialOrdersPlugin.StallTalk.Value || !SpecialOrdersPlugin.AllowsItems || Busy() || !__instance.isInteractable) return true;
			if (!__instance.open) return true;   // closed: the game's own message
			Window.OpenDialog(__instance, Speaker(__instance));
			return false;
		}

		[HarmonyPatch(typeof(BaseShop), "Interact")]
		[HarmonyPrefix]
		private static bool Shop_Interact(BaseShop __instance)
		{
			if (bypass || !(__instance is FurnitureShop) || !SpecialOrdersPlugin.StallTalk.Value || !SpecialOrdersPlugin.AllowsFurniture || Busy()) return true;
			Window.OpenDialog(__instance, !string.IsNullOrEmpty(__instance.title) ? __instance.title : "Shop");
			return false;
		}
	}
}
