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
		private static readonly Dictionary<Response, Response> leaveWith = new Dictionary<Response, Response>();
		private static readonly Regex LeaveWords = new Regex(@"\b(bye|goodbye|leave|leaving|later|never ?mind|nothing|no thanks|see you|go now|have to go|i'?ll go|farewell)\b", RegexOptions.IgnoreCase);

		// The shop to open once the conversation has ended the normal way.
		internal static object PendingShop;

		// The option the player would pick to leave: a goodbye-like line, else the last option if it doesn't trade.
		private static Response LeaveResponse(Response[] responses)
		{
			Response last = null;
			foreach (Response r in responses)
			{
				if (r == null || ours.ContainsKey(r)) continue;
				string text = r.formattedText != null ? r.formattedText.text : "";
				if (!string.IsNullOrEmpty(text) && LeaveWords.IsMatch(text)) return r;
				last = r;
			}
			if (last == null) return null;
			string script = last.destinationEntry != null ? last.destinationEntry.userScript : null;
			string lt = last.formattedText != null ? last.formattedText.text : "";
			if ((!string.IsNullOrEmpty(script) && script.IndexOf("StartTrade", StringComparison.Ordinal) >= 0) || ShopWords.IsMatch(lt ?? "")) return null;
			return last;
		}
		private static readonly Regex ShopWords = new Regex(@"\b(trad|buy|shop|sell|sale|goods|ware|furnit|brows|look around|what do you have|see what|merchandise|stock)\w*", RegexOptions.IgnoreCase);

		[HarmonyPatch(typeof(Trade), "RegisterFunction")]
		[HarmonyPostfix]
		private static void Register(Trade __instance) { talkingTrade = __instance; }

		[HarmonyPatch(typeof(Trade), "UnRegisterFunction")]
		[HarmonyPostfix]
		private static void Unregister(Trade __instance) { if (talkingTrade == __instance) talkingTrade = null; }

		// The shop run by the person you're talking to: the trader whose conversation registered StartTrade,
		// else the stall that person owns, else the nearest shop to you (you're standing at its counter).
		private static object ShopOfConversant(out string why)
		{
			why = "";
			if (talkingTrade != null && SpecialOrdersPlugin.AllowsItems) { why = "registered trade"; return talkingTrade; }
			Transform npc = null;
			try { npc = DialogueManager.currentConversant; } catch (Exception) { }
			int id = -1;
			string first = null, full = null;
			try
			{
				NPCInfo info = npc != null ? (npc.GetComponentInParent<NPCInfo>() ?? npc.GetComponentInChildren<NPCInfo>()) : null;
				if (info != null && info.character != null)
				{
					id = info.character.ID;
					first = info.character.Name;
					full = info.character.FullName;
				}
			}
			catch (Exception) { }
			if (SpecialOrdersPlugin.AllowsItems)
			{
				Trade corner = SpecialOrdersPlugin.CornerShopFor(full) ?? SpecialOrdersPlugin.CornerShopFor(first);
				if (corner != null) { why = "corner shop"; return corner; }
			}
			Vector3 me = PlayerLocator.instance != null ? PlayerLocator.instance.transform.position : (npc != null ? npc.position : Vector3.zero);
			Trade bestTrade = null; float tradeD = float.MaxValue;
			if (SpecialOrdersPlugin.AllowsItems)
				foreach (Trade t in UnityEngine.Object.FindObjectsByType<Trade>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
				{
					if (id > 0 && t.owner != null && t.owner.ID == id) { why = "owner id"; return t; }
					if (NameMatch(t, first, full)) { why = "name"; return t; }
					float d = Vector3.Distance(t.transform.position, me);
					if (d < tradeD) { tradeD = d; bestTrade = t; }
				}
			FurnitureShop bestShop = null; float shopD = float.MaxValue;
			if (SpecialOrdersPlugin.AllowsFurniture)
				foreach (FurnitureShop fs in UnityEngine.Object.FindObjectsByType<FurnitureShop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
				{
					float d = Vector3.Distance(fs.transform.position, me);
					if (d < shopD) { shopD = d; bestShop = fs; }
				}
			why = "npc " + (full ?? first ?? "?") + " id " + id + ", nearest trade " + (bestTrade != null ? "'" + bestTrade.title + "' at " + tradeD.ToString("0.0") + " m" : "none") +
			      ", nearest furniture shop " + (bestShop != null ? "'" + bestShop.title + "' at " + shopD.ToString("0.0") + " m" : "none");
			float range = SpecialOrdersPlugin.ShopRange.Value;
			if (bestTrade != null && tradeD <= range && tradeD <= shopD) return bestTrade;
			if (bestShop != null && shopD <= range) return bestShop;
			return null;
		}

		// "Sirkku's Flower Stall" belongs to Sirkku; also the stall's owner name.
		private static bool NameMatch(Trade t, string first, string full)
		{
			try
			{
				string owner = t.owner != null ? t.owner.name : null;
				if (!string.IsNullOrEmpty(owner) && owner != "Name" && ((full != null && owner == full) || (first != null && owner.StartsWith(first + " ")) || owner == first)) return true;
				string title = t.title ?? "";
				if (string.IsNullOrEmpty(first) || first.Length < 3) return false;
				if (title.StartsWith(first + "'", StringComparison.OrdinalIgnoreCase) || title.StartsWith(first + "s'", StringComparison.OrdinalIgnoreCase)) return true;
				// spelling differences like Sirku/Sirkku, Selma/Selmas: compare the word before the apostrophe
				int ap = title.IndexOf('\'');
				if (ap > 2)
				{
					string word = title.Substring(0, ap).TrimEnd('s', 'S');
					string f = first.TrimEnd('s', 'S');
					if (Loose(word).Equals(Loose(f), StringComparison.OrdinalIgnoreCase)) return true;
				}
			}
			catch (Exception) { }
			return false;
		}

		private static string Loose(string w)
		{
			var sb = new System.Text.StringBuilder();
			char last = '\0';
			foreach (char c in w.ToLowerInvariant()) { if (c != last) sb.Append(c); last = c; }   // kk -> k
			return sb.ToString();
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
				if (shopThisConversation == null && !shopLooked)
				{
					string why;
					shopThisConversation = ShopOfConversant(out why);
					// No option for a shop with nothing to order (like a mod's corner shop with a fixed daily stock).
					if (shopThisConversation != null && !SpecialOrdersPlugin.HasOffers(shopThisConversation))
					{
						why = ShopLabel(shopThisConversation) + " has nothing to order";
						shopThisConversation = null;
					}
					if (!shopLooked || shopThisConversation != null)
						SpecialOrdersPlugin.Log("Conversation: " + (shopThisConversation == null ? "no shop (" + why + ")" : "shop " + ShopLabel(shopThisConversation) + " (" + why + ")"));
					shopLooked = true;
				}
				object shop = shopThisConversation;
				if (shop == null) return;
				var response = new Response(new FormattedText(SpecialOrdersPlugin.OptionText.Value), state.pcResponses[state.pcResponses.Length - 1].destinationEntry, true);
				ours[response] = shop;
				Response leave = LeaveResponse(state.pcResponses);
				if (leave != null) leaveWith[response] = leave;
				var list = new List<Response>(state.pcResponses);
				// before a goodbye-style last option, otherwise at the end
				list.Insert(Mathf.Max(0, list.Count - 1), response);
				state.pcResponses = list.ToArray();
			}
			catch (Exception e) { SpecialOrdersPlugin.Warn("Conversation option: " + e.Message); }
		}

		[HarmonyPatch(typeof(ConversationController), "OnSelectedResponse")]
		[HarmonyPrefix]
		private static bool Selected(ConversationController __instance, object sender, SelectedResponseEventArgs e)
		{
			object shop;
			if (e == null || e.response == null || !ours.TryGetValue(e.response, out shop)) return true;
			Response leave;
			leaveWith.TryGetValue(e.response, out leave);
			ours.Clear(); leaveWith.Clear();
			// End the conversation the way the player leaving would, so the shopkeeper's own script runs (Welhö
			// goes back inside when you say goodbye); the catalogue opens once it's over.
			if (leave != null)
			{
				try
				{
					PendingShop = shop;
					__instance.OnSelectedResponse(sender, new SelectedResponseEventArgs(leave));
					return false;
				}
				catch (Exception ex) { SpecialOrdersPlugin.Warn("Leaving the conversation: " + ex.Message); PendingShop = null; }
			}
			try { DialogueManager.StopConversation(); } catch (Exception) { }
			SpecialOrdersPlugin.OpenFor(shop);
			return false;
		}

		[HarmonyPatch(typeof(PixelCrushers.DialogueSystem.DialogueSystemController), "StartConversation", new Type[] { typeof(string), typeof(Transform), typeof(Transform), typeof(int), typeof(IDialogueUI) })]
		[HarmonyPrefix]
		private static void Start() { menusThisConversation = 0; ours.Clear(); leaveWith.Clear(); }

		// Called every frame: opens the catalogue picked in a conversation once that conversation has ended.
		internal static void Tick()
		{
			if (PendingShop == null) return;
			bool active = true;
			try { active = DialogueManager.isConversationActive; } catch (Exception) { active = false; }
			if (active) return;
			object shop = PendingShop; PendingShop = null;
			SpecialOrdersPlugin.OpenFor(shop);
		}
	}
}
