using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SpecialOrdersMod
{
	/// <summary>
	/// Adds a "special order" option to shopkeepers' conversations. Picking it ends the conversation and opens
	/// the order catalogue for that shopkeeper's shop.
	/// </summary>
	[HarmonyPatch]
	internal static class Talk
	{
		private static Trade talkingTrade;          // the trader whose conversation registered "StartTrade"
		private static readonly Dictionary<Response, object> ours = new Dictionary<Response, object>();
		private static readonly Regex ShopWords = new Regex(@"\b(trad|buy|shop|sell|sale|goods|ware|furnit|brows|look around|what do you have|see what|merchandise|stock)\w*", RegexOptions.IgnoreCase);

		[HarmonyPatch(typeof(Trade), "RegisterFunction")]
		[HarmonyPostfix]
		private static void Register(Trade __instance) { talkingTrade = __instance; }

		[HarmonyPatch(typeof(Trade), "UnRegisterFunction")]
		[HarmonyPostfix]
		private static void Unregister(Trade __instance) { if (talkingTrade == __instance) talkingTrade = null; }

		// The shop run by the person you're talking to.
		private static object ShopOfConversant()
		{
			Transform npc = null;
			try { npc = DialogueManager.currentConversant; } catch (Exception) { }
			if (talkingTrade != null && SpecialOrdersPlugin.AllowsItems) return talkingTrade;
			if (npc == null) return null;
			Vector3 at = npc.position;
			object best = null;
			float bestD = float.MaxValue;
			if (SpecialOrdersPlugin.AllowsFurniture)
				foreach (FurnitureShop fs in UnityEngine.Object.FindObjectsByType<FurnitureShop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
				{
					float d = Vector3.Distance(fs.transform.position, at);
					if (d < 6f && d < bestD) { best = fs; bestD = d; }
				}
			if (best == null && SpecialOrdersPlugin.AllowsItems)
			{
				int id = -1;
				try { NPCInfo info = npc.GetComponentInParent<NPCInfo>(); if (info != null && info.character != null) id = info.character.ID; } catch (Exception) { }
				foreach (Trade t in UnityEngine.Object.FindObjectsByType<Trade>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
				{
					bool owner = id > 0 && t.owner != null && t.owner.ID == id;
					float d = Vector3.Distance(t.transform.position, at);
					if (owner) { best = t; break; }
					if (d < 8f && d < bestD) { best = t; bestD = d; }
				}
			}
			return best;
		}

		private static string ShopLabel(object shop)
		{
			if (shop is Trade t) return "trader '" + t.title + "' (" + t.name + ")";
			if (shop is FurnitureShop f) return "furniture shop '" + f.title + "' (" + f.name + ")";
			return shop.ToString();
		}

		private static bool LooksLikeShopMenu(Response[] responses)
		{
			foreach (Response r in responses)
			{
				if (r == null) continue;
				string text = r.formattedText != null ? r.formattedText.text : null;
				if (!string.IsNullOrEmpty(text) && ShopWords.IsMatch(text)) return true;
				string script = r.destinationEntry != null ? r.destinationEntry.userScript : null;
				if (!string.IsNullOrEmpty(script) && script.IndexOf("StartTrade", StringComparison.Ordinal) >= 0) return true;
			}
			return false;
		}

		private static int menusThisConversation;
		private static object currentController;
		private static object shopThisConversation;
		private static bool shopLooked;

		[HarmonyPatch(typeof(ConversationController), "GotoState")]
		[HarmonyPrefix]
		private static void GotoState(ConversationController __instance, ConversationState state)
		{
			try
			{
				if (!ReferenceEquals(__instance, currentController))
				{
					currentController = __instance;
					menusThisConversation = 0; ours.Clear(); shopLooked = false; shopThisConversation = null;
				}
				if (state == null || state.pcResponses == null || state.pcResponses.Length == 0 || !SpecialOrdersPlugin.InGame) return;
				menusThisConversation++;
				foreach (Response r in state.pcResponses) if (r != null && ours.ContainsKey(r)) return;   // already added
				bool shopMenu = LooksLikeShopMenu(state.pcResponses);
				if (!shopMenu && menusThisConversation > 1) return;   // the first menu, or one about trading
				if (!shopLooked)
				{
					shopLooked = true;
					shopThisConversation = ShopOfConversant();
					string who = "?";
					try { who = DialogueManager.currentConversant != null ? DialogueManager.currentConversant.name : "none"; } catch (Exception) { }
					SpecialOrdersPlugin.Log("Conversation with " + who + ": " + (shopThisConversation == null ? "no shop found nearby" : "shop " + ShopLabel(shopThisConversation)));
				}
				object shop = shopThisConversation;
				if (shop == null) return;
				var response = new Response(new FormattedText(SpecialOrdersPlugin.OptionText.Value), state.pcResponses[state.pcResponses.Length - 1].destinationEntry, true);
				ours[response] = shop;
				var list = new List<Response>(state.pcResponses);
				// before a goodbye-style last option, otherwise at the end
				list.Insert(Mathf.Max(0, list.Count - 1), response);
				state.pcResponses = list.ToArray();
			}
			catch (Exception e) { SpecialOrdersPlugin.Warn("Conversation option: " + e.Message); }
		}

		[HarmonyPatch(typeof(ConversationController), "OnSelectedResponse")]
		[HarmonyPrefix]
		private static bool Selected(SelectedResponseEventArgs e)
		{
			object shop;
			if (e == null || e.response == null || !ours.TryGetValue(e.response, out shop)) return true;
			ours.Clear();
			try { DialogueManager.StopConversation(); } catch (Exception) { }
			SpecialOrdersPlugin.OpenFor(shop);
			return false;
		}

		[HarmonyPatch(typeof(PixelCrushers.DialogueSystem.DialogueSystemController), "StartConversation", new Type[] { typeof(string), typeof(Transform), typeof(Transform), typeof(int), typeof(IDialogueUI) })]
		[HarmonyPrefix]
		private static void Start() { menusThisConversation = 0; ours.Clear(); }
	}
}
