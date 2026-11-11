using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpecialOrdersMod
{
	/// <summary>The order catalogue and "on the way" list, drawn in the game's own style.</summary>
	internal static class Window
	{
		internal static bool Open;
		internal static bool CloseLater;
		private static Vector2 scroll;
		private static string search = "";
		private static CursorLockMode prevLock;
		private static bool prevVisible;
		private static UnityEngine.EventSystems.EventSystem pausedEvents;
		private static GameObject holder;

		internal static void ResetView() { scroll = Vector2.zero; search = ""; }

		// While open the window owns the mouse: the game's controls and the shop screen underneath are paused.
		internal static void SetOpen(bool open)
		{
			if (open == Open) return;
			if (open)
			{
				prevLock = Cursor.lockState;
				prevVisible = Cursor.visible;
				try
				{
					pausedEvents = UnityEngine.EventSystems.EventSystem.current;
					if (pausedEvents != null && pausedEvents.enabled) pausedEvents.enabled = false; else pausedEvents = null;
				}
				catch (Exception) { pausedEvents = null; }
				try
				{
					if (holder == null) { holder = new GameObject("SpecialOrders_Window"); UnityEngine.Object.DontDestroyOnLoad(holder); }
					if (GameController.instance != null) GameController.instance.ControlsDisabled(holder, true, true);
				}
				catch (Exception e) { SpecialOrdersPlugin.Warn("Window controls: " + e.Message); }
				SpecialOrdersPlugin.RefreshOffers(SpecialOrdersPlugin.CurrentShop());
			}
			else
			{
				try { if (pausedEvents != null) pausedEvents.enabled = true; } catch (Exception) { }
				pausedEvents = null;
				try { if (holder != null && GameController.instance != null) GameController.instance.ControlsEnabled(holder); } catch (Exception) { }
				Cursor.lockState = prevLock;
				Cursor.visible = prevVisible;
				GUI.FocusControl(null);
			}
			Open = open;
		}

		internal static void LateTick()
		{
			if (CloseLater) { CloseLater = false; SetOpen(false); return; }
			if (!Open) return;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		// ---- Styles ----

		private static GUIStyle sPanel, sTitle, sSub, sSection, sTab, sTabOn, sCard, sHead, sText, sSmall, sButton, sHint, sField, sHintBox;
		private static Font gameFont;
		private static bool hadFont;
		private static readonly Color Paper = new Color(0.91f, 0.88f, 0.81f), Ink = new Color(0.14f, 0.11f, 0.08f), Ochre = new Color(0.79f, 0.6f, 0.23f), Cream = new Color(0.94f, 0.9f, 0.82f);

		private static Texture2D Framed(Color fill, Color edge, int width)
		{
			var t = new Texture2D(8, 8) { hideFlags = HideFlags.HideAndDontSave };
			for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
				t.SetPixel(x, y, x < width || y < width || x >= 8 - width || y >= 8 - width ? edge : fill);
			t.Apply();
			return t;
		}

		private static GUIStyle Box(Color fill, Color edge, int width, GUIStyle from)
		{
			var s = new GUIStyle(from) { border = new RectOffset(3, 3, 3, 3) };
			s.normal.background = s.onNormal.background = Framed(fill, edge, width);
			return s;
		}

		private static string Spaced(string text) { return string.Join(" ", Array.ConvertAll(text.ToUpperInvariant().ToCharArray(), c => c.ToString())); }

		private static void InitStyles()
		{
			if (sPanel != null && sPanel.normal.background != null && sCard.normal.background != null && (gameFont != null || !hadFont)) return;
			gameFont = null;
			foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
			{
				string n = f.name.ToLowerInvariant();
				if (n.Contains("special") || n.Contains("elite") || n.Contains("typewriter") || n.Contains("courier") || n.Contains("american")) { gameFont = f; break; }
			}
			hadFont = gameFont != null;
			sPanel = Box(new Color(0.1f, 0.09f, 0.08f, 0.98f), new Color(0.27f, 0.25f, 0.21f), 2, GUI.skin.box);
			sTitle = new GUIStyle(GUI.skin.label) { font = gameFont, fontSize = 17, alignment = TextAnchor.MiddleCenter };
			sTitle.normal.textColor = Cream;
			sSub = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true };
			sSub.normal.textColor = new Color(0.78f, 0.72f, 0.6f);
			sSection = Box(new Color(0.15f, 0.14f, 0.13f), new Color(0.2f, 0.19f, 0.17f), 1, GUI.skin.label);
			sSection.font = gameFont; sSection.fontSize = 13; sSection.padding = new RectOffset(12, 12, 8, 8); sSection.margin = new RectOffset(0, 0, 12, 6);
			sSection.normal.textColor = Cream;
			sTab = Box(new Color(0.42f, 0.29f, 0.08f), new Color(0.27f, 0.18f, 0.05f), 1, GUI.skin.button);
			sTab.font = gameFont; sTab.fontSize = 12; sTab.padding = new RectOffset(10, 10, 6, 6);
			sTab.normal.textColor = sTab.hover.textColor = new Color(0.98f, 0.93f, 0.8f);
			sTab.hover.background = Framed(new Color(0.6f, 0.42f, 0.12f), new Color(0.27f, 0.18f, 0.05f), 1);
			sTabOn = new GUIStyle(sTab);
			sTabOn.normal.background = sTabOn.hover.background = Framed(Ochre, new Color(0.95f, 0.75f, 0.35f), 1);
			sTabOn.normal.textColor = sTabOn.hover.textColor = Ink;
			sCard = Box(Paper, new Color(0.55f, 0.5f, 0.42f), 1, GUI.skin.box);
			sCard.padding = new RectOffset(12, 12, 8, 8); sCard.margin = new RectOffset(0, 0, 0, 5);
			sHead = new GUIStyle(GUI.skin.label) { font = gameFont, fontSize = 14, richText = true, wordWrap = true };
			sHead.normal.textColor = Ink;
			sText = new GUIStyle(sHead) { fontSize = 13 };
			sText.normal.textColor = new Color(0.25f, 0.21f, 0.16f);
			sSmall = new GUIStyle(sHead) { fontSize = 11 };
			sSmall.normal.textColor = new Color(0.42f, 0.37f, 0.3f);
			sButton = Box(new Color(0.42f, 0.29f, 0.08f), new Color(0.27f, 0.18f, 0.05f), 1, GUI.skin.button);
			sButton.font = gameFont; sButton.fontSize = 12; sButton.richText = true;
			sButton.normal.textColor = sButton.hover.textColor = new Color(0.98f, 0.93f, 0.8f);
			sButton.hover.background = Framed(new Color(0.6f, 0.42f, 0.12f), new Color(0.27f, 0.18f, 0.05f), 1);
			sHint = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
			sHint.normal.textColor = new Color(0.66f, 0.62f, 0.53f);
			sField = new GUIStyle(GUI.skin.textField) { fontSize = 14, border = new RectOffset(3, 3, 3, 3), padding = new RectOffset(7, 7, 5, 5) };
			sField.normal.background = sField.hover.background = Framed(new Color(0.86f, 0.82f, 0.74f), new Color(0.55f, 0.45f, 0.3f), 1);
			sField.focused.background = Framed(new Color(1f, 0.98f, 0.92f), new Color(0.95f, 0.66f, 0.12f), 3);
			sField.normal.textColor = sField.hover.textColor = sField.focused.textColor = new Color(0.25f, 0.21f, 0.16f);
			sHintBox = Box(new Color(0.1f, 0.09f, 0.08f, 0.92f), Ochre, 1, GUI.skin.label);
			sHintBox.font = gameFont; sHintBox.fontSize = 13; sHintBox.alignment = TextAnchor.MiddleCenter; sHintBox.padding = new RectOffset(12, 12, 6, 6);
			sHintBox.normal.textColor = Cream;
		}

		// ---- Drawing ----

		internal static void Draw()
		{
			if (!SpecialOrdersPlugin.InGame) return;
			bool atShop = SpecialOrdersPlugin.CurrentShop() != null;
			if (!Open)
			{
				if (atShop && SpecialOrdersPlugin.ShowHint.Value && SpecialOrdersPlugin.ShopName != null && SpecialOrdersPlugin.Offers.Count > 0)
				{
					InitStyles();
					string t = "[" + SpecialOrdersPlugin.OrdersKey.Value + "]  " + Spaced("Special orders");
					Vector2 size = sHintBox.CalcSize(new GUIContent(t));
					GUI.Label(new Rect((Screen.width - size.x) / 2f, 12f, size.x, size.y), t, sHintBox);
				}
				return;
			}
			InitStyles();
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { CloseLater = true; Event.current.Use(); }
			float w = Mathf.Min(760f, Screen.width - 40f), h = Mathf.Min(700f, Screen.height - 80f);
			Rect r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
			GUI.Box(r, GUIContent.none, sPanel);
			GUI.Label(new Rect(r.x, r.y + 10f, r.width, 26f), Spaced("Special orders"), sTitle);
			string sub = atShop && SpecialOrdersPlugin.ShopName != null ? "Ordering from <b>" + SpecialOrdersPlugin.ShopName + "</b>" : "Your orders";
			GUI.Label(new Rect(r.x, r.y + 36f, r.width, 20f), sub, sSub);
			GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 62f, r.width - 32f, r.height - 74f));
			Body(atShop);
			GUILayout.EndArea();
		}

		private static readonly Dictionary<string, int> amounts = new Dictionary<string, int>();

		private static void Body(bool atShop)
		{
			var onTheWay = SpecialOrdersPlugin.OrdersOnTheWay();
			scroll = GUILayout.BeginScrollView(scroll);

			GUILayout.Label(Spaced("On the way") + "   (" + onTheWay.Count + ")", sSection);
			if (onTheWay.Count == 0) GUILayout.Label("No orders on the way.", sHint);
			foreach (var o in onTheWay)
			{
				GUILayout.BeginVertical(sCard);
				GUILayout.Label("<b>" + o.What + "</b>", sHead);
				GUILayout.Label((o.Arrived ? "<b>Arrived:</b> pick it up at your mailbox or parcel locker" : "Arrives in " + SpecialOrdersPlugin.When(o.Minutes)) + (o.Shop.Length > 0 ? "  ·  from " + o.Shop : ""), sText);
				GUILayout.EndVertical();
			}

			if (!atShop)
			{
				GUILayout.Space(6);
				GUILayout.Label("Open a furniture shop's menu or a trader's trade screen and press " + SpecialOrdersPlugin.OrdersKey.Value + " to order things they don't have in stock.", sHint);
			}
			else
			{
				var offers = SpecialOrdersPlugin.Offers;
				GUILayout.Label(Spaced("Catalogue") + "   (" + offers.Count + ")", sSection);
				if (offers.Count == 0)
				{
					string why = SpecialOrdersPlugin.CurrentShop() is FurnitureShop
						? (SpecialOrdersPlugin.AllowsFurniture ? "This shop has nothing to order." : "Ordering furniture is turned off in the settings.")
						: (SpecialOrdersPlugin.AllowsItems ? "This trader has nothing to order." : "Ordering items is turned off in the settings.");
					GUILayout.Label(why, sHint);
				}
				else
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label("Search:", sHint, GUILayout.Width(56));
					search = GUILayout.TextField(search ?? "", 40, sField);
					GUILayout.EndHorizontal();
					int open = SpecialOrdersPlugin.OpenOrdersFrom(SpecialOrdersPlugin.ShopName);
					bool full = open >= SpecialOrdersPlugin.MaxOpenOrdersPerShop.Value;
					GUILayout.Label("Orders from this shop on the way: " + open + "/" + SpecialOrdersPlugin.MaxOpenOrdersPerShop.Value +
					                ". Rarer things cost more and take longer." + PayHint(), sHint);
					string q = (search ?? "").Trim();
					foreach (var o in offers)
					{
						if (q.Length > 0 && o.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
						OfferCard(o, full);
					}
				}
			}
			GUILayout.EndScrollView();
			GUILayout.Label("Press Esc or " + SpecialOrdersPlugin.OrdersKey.Value + " to close.", sHint);
		}

		private static string PayHint()
		{
			switch (SpecialOrdersPlugin.Payment.Value)
			{
				case SpecialOrdersPlugin.PayModes.Invoice: return " Orders are paid by invoice.";
				case SpecialOrdersPlugin.PayModes.Both: return " Pay now, or by invoice (+" + Mathf.RoundToInt(SpecialOrdersPlugin.InvoiceExtraFee.Value * 100f) + "%).";
			}
			return "";
		}

		private static void OfferCard(SpecialOrdersPlugin.Offer o, bool full)
		{
			GUILayout.BeginVertical(sCard);
			GUILayout.BeginHorizontal();
			GUILayout.Label("<b>" + o.Name + "</b>", sHead);
			GUILayout.FlexibleSpace();
			int amount = 1;
			if (o.Item != null)
			{
				string key = o.Name;
				if (!amounts.TryGetValue(key, out amount)) amount = 1;
				if (GUILayout.Button("-", sButton, GUILayout.Width(26))) amount = Mathf.Max(1, amount - 1);
				GUILayout.Label("x" + amount, sText, GUILayout.Width(34));
				if (GUILayout.Button("+", sButton, GUILayout.Width(26))) amount = Mathf.Min(SpecialOrdersPlugin.MaxItemAmount.Value, amount + 1);
				amounts[key] = amount;
				o.Amount = amount;
			}
			GUILayout.EndHorizontal();
			string stock = o.Item != null ? (o.Rare ? "rare stock" : "common stock") : "in stock after " + Mathf.RoundToInt(o.StockChance * 100f) + "% of restocks";
			GUILayout.Label(SpecialOrdersPlugin.RarityWord(o) + "  ·  " + stock + "  ·  +" + Mathf.RoundToInt(o.Fee * 100f) + "% ordering fee  ·  arrives in about " + SpecialOrdersPlugin.When(Mathf.RoundToInt(o.Days * 1440f)), sSmall);
			GUILayout.BeginHorizontal();
			GUILayout.Label(SpecialOrdersPlugin.Price(o.Oc * amount, o.Rm * amount), sText);
			GUILayout.FlexibleSpace();
			GUI.enabled = !full;
			var mode = SpecialOrdersPlugin.Payment.Value;
			if (mode != SpecialOrdersPlugin.PayModes.Invoice && GUILayout.Button(full ? "ORDER LIMIT" : "ORDER, PAY NOW", sButton, GUILayout.Width(150))) SpecialOrdersPlugin.PlaceOrder(o, false);
			if (mode != SpecialOrdersPlugin.PayModes.UpFront && GUILayout.Button(full ? "ORDER LIMIT" : "ORDER ON INVOICE", sButton, GUILayout.Width(160))) SpecialOrdersPlugin.PlaceOrder(o, true);
			GUI.enabled = true;
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
	}

	// While the window is open, the game's key bindings and look/move axes report nothing.
	[HarmonyPatch]
	internal static class BlockBool
	{
		private static readonly string[] Names = { "IsKeyDown", "IsKeyUp", "IsKey", "GetKeyDown", "GetKeyDownKeyboardOnly", "GetKeyUp", "GetKey", "GetKeyWithHold", "GetAuxiliaryReloadDown" };
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return AccessTools.GetDeclaredMethods(typeof(InputManager)).Where(m => Names.Contains(m.Name) && m.ReturnType == typeof(bool)).Cast<MethodBase>();
		}
		private static bool Prefix(ref bool __result)
		{
			if (!Window.Open) return true;
			__result = false;
			return false;
		}
	}

	[HarmonyPatch]
	internal static class BlockAxis
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return AccessTools.GetDeclaredMethods(typeof(InputManager)).Where(m => m.Name.StartsWith("Get") && m.Name.EndsWith("Input") && m.ReturnType == typeof(float)).Cast<MethodBase>();
		}
		private static bool Prefix(ref float __result)
		{
			if (!Window.Open) return true;
			__result = 0f;
			return false;
		}
	}
}
