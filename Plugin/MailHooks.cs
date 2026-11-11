using System;
using HarmonyLib;

namespace SpecialOrdersMod
{
	// Looking at a mailbox (receive side) or the parcel locker shows your special orders.
	[HarmonyPatch]
	internal static class MailHooks
	{
		[HarmonyPatch(typeof(Mailbox), "UpdateUseMessage")]
		[HarmonyPostfix]
		private static void Mailbox_Postfix(Mailbox __instance)
		{
			try
			{
				if (!SpecialOrdersPlugin.MailboxHint.Value || __instance.mode != Mailbox.Mode.Receive) return;
				string s = SpecialOrdersPlugin.DeliverySummary(false);
				if (s.Length > 0) UseText.instance.ChangeUseText(__instance.title, __instance.useMessage + "  ·  " + s);
			}
			catch (Exception) { }
		}

		[HarmonyPatch(typeof(ParcelLocker), "UpdateUseMessage")]
		[HarmonyPostfix]
		private static void Locker_Postfix(ParcelLocker __instance)
		{
			try
			{
				if (!SpecialOrdersPlugin.MailboxHint.Value) return;
				string s = SpecialOrdersPlugin.DeliverySummary(true);
				if (s.Length > 0) UseText.instance.ChangeUseText(__instance.title, __instance.useMessage + "  ·  " + s);
			}
			catch (Exception) { }
		}
	}
}
