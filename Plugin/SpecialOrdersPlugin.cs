using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpecialOrdersMod
{
	/// <summary>
	/// Special Orders: order furniture from furniture shops, and items from traders, that aren't in stock right now.
	/// Only what that shop can stock (its own lists). Rarer things cost more and take longer. Delivery uses the
	/// game's own mail (mailbox or parcel locker), with the game's saved timers. Pay up front or by invoice.
	/// </summary>
	[BepInPlugin("JuanBerta.Obenseuer.SpecialOrders", "Special Orders", "1.0.5")]
	public class SpecialOrdersPlugin : BaseUnityPlugin
	{
		internal static SpecialOrdersPlugin Instance;

		internal enum OrderKinds { Furniture, Items, Both }
		internal enum PayModes { UpFront, Invoice, Both }
		internal enum DeliverTo { Mailbox, ParcelLocker }

		internal static ConfigEntry<KeyboardShortcut> OrdersKey;
		internal static ConfigEntry<OrderKinds> Kinds;
		internal static ConfigEntry<PayModes> Payment;
		internal static ConfigEntry<DeliverTo> FurnitureTo;
		internal static ConfigEntry<DeliverTo> ItemsTo;
		internal static ConfigEntry<float> FurnitureMinFee, FurnitureMaxFee, ItemCommonFee, ItemRareFee, InvoiceExtraFee;
		internal static ConfigEntry<float> MinDays, MaxDays, RareExtraDays;
		internal static ConfigEntry<int> InvoiceDays, MaxOpenOrdersPerShop, MaxItemAmount;
		internal static ConfigEntry<bool> HagglingDiscount;
		internal static ConfigEntry<string> OptionText;
		internal static ConfigEntry<bool> ArrivalNotice, MailboxHint, ShopButton, StallTalk;
		internal static ConfigEntry<float> ShopRange;

		internal const string MailPrefix = "Special order";

		private void Awake()
		{
			Instance = this;
			ConfigDescription D(string t, int o, AcceptableValueBase r = null) { return new ConfigDescription(t, r, new ConfigurationManagerAttributes { Order = o }); }
			OrdersKey = Config.Bind("General", "DeliveriesKey", new KeyboardShortcut(KeyCode.F8), D("Shows your orders on the way, anywhere (you can't order with it: ordering is a conversation option with the shopkeeper). Clear it to turn it off.", 100));
			ShopRange = Config.Bind("General", "ShopSearchRange", 15f, D("How close (in meters) you must be to a trader's stall or a furniture shop for the special order option to appear in a conversation.", 96, new AcceptableValueRange<float>(2f, 20f)));
			StallTalk = Config.Bind("General", "CounterConversation", true, D("Using a market stall or a furniture shop counter first asks what you want: trade, place a special order, or goodbye (those shops have no conversation of their own).", 97));
			ShopButton = Config.Bind("General", "ShopScreenButton", false, D("Also show a \"Special order\" button on trade screens and furniture shop menus.", 96));
			ArrivalNotice = Config.Bind("General", "ArrivalNotification", true, D("Show a notification when a special order arrives.", 98));
			MailboxHint = Config.Bind("General", "MailboxHint", true, D("Looking at a mailbox or parcel locker shows when your next special order arrives.", 97));
			OptionText = Config.Bind("General", "ConversationOption", "I'd like to place a special order.", D("The conversation option shopkeepers get.", 95));
			Kinds = Config.Bind("General", "WhatCanBeOrdered", OrderKinds.Both, D("Furniture (from furniture shops), Items (from traders) or Both.", 90));
			Payment = Config.Bind("Payment", "PaymentMode", PayModes.Both, D("UpFront: pay when you order. Invoice: the game sends you an invoice to pay later. Both: you choose for each order.", 100));
			InvoiceExtraFee = Config.Bind("Payment", "InvoiceExtraFee", 0.1f, D("Paying by invoice costs this much more (0.1 = +10%).", 90, new AcceptableValueRange<float>(0f, 2f)));
			InvoiceDays = Config.Bind("Payment", "InvoiceDays", 7, D("In-game days you have to pay an order's invoice (the game's invoice rules apply after that).", 85, new AcceptableValueRange<int>(1, 30)));
			HagglingDiscount = Config.Bind("Payment", "HagglingDiscount", true, D("With the Haggling mod, your Haggling discount also applies to orders.", 80));
			FurnitureMinFee = Config.Bind("Fees", "FurnitureMinFee", 0.25f, D("Ordering fee for furniture the shop almost always has (0.25 = +25%).", 100, new AcceptableValueRange<float>(0f, 5f)));
			FurnitureMaxFee = Config.Bind("Fees", "FurnitureMaxFee", 1.0f, D("Ordering fee for the rarest furniture (1.0 = +100%, double the price). Fees in between follow how rarely the shop stocks it.", 95, new AcceptableValueRange<float>(0f, 10f)));
			ItemCommonFee = Config.Bind("Fees", "ItemCommonFee", 0.5f, D("Ordering fee for a trader's common items (0.5 = +50%). Items cost more to order than furniture.", 90, new AcceptableValueRange<float>(0f, 10f)));
			ItemRareFee = Config.Bind("Fees", "ItemRareFee", 1.5f, D("Ordering fee for a trader's rare items (1.5 = +150%).", 85, new AcceptableValueRange<float>(0f, 10f)));
			MaxItemAmount = Config.Bind("Fees", "MaxItemsPerOrder", 100, D("Most items of one kind in one order. Type the amount in the box, use - and + (Shift for steps of 10), or MAX.", 80, new AcceptableValueRange<int>(1, 9999)));
			MinDays = Config.Bind("Delivery", "MinDays", 1f, D("Fewest in-game days for a delivery.", 100, new AcceptableValueRange<float>(0f, 14f)));
			MaxDays = Config.Bind("Delivery", "MaxDays", 2f, D("Most in-game days for a common thing.", 95, new AcceptableValueRange<float>(0f, 14f)));
			RareExtraDays = Config.Bind("Delivery", "RareExtraDays", 2f, D("Extra days for the rarest things (scaled by rarity).", 90, new AcceptableValueRange<float>(0f, 14f)));
			FurnitureTo = Config.Bind("Delivery", "DeliverFurnitureTo", DeliverTo.Mailbox, D("Where ordered furniture arrives: your mailbox or the parcel locker.", 85));
			ItemsTo = Config.Bind("Delivery", "DeliverItemsTo", DeliverTo.Mailbox, D("Where ordered items arrive: your mailbox or the parcel locker.", 80));
			MaxOpenOrdersPerShop = Config.Bind("Delivery", "MaxOpenOrdersPerShop", 3, D("Most orders on the way from one shop at a time.", 75, new AcceptableValueRange<int>(1, 50)));

			var h = new Harmony("JuanBerta.Obenseuer.SpecialOrders");
			h.PatchAll(typeof(BlockBool));
			h.PatchAll(typeof(BlockAxis));
			h.PatchAll(typeof(Talk));
			h.PatchAll(typeof(MailHooks));
			h.PatchAll(typeof(Counter));
			MailController.MailReceived += name => Arrived(name, false);
			MailController.PackageReceived += name => Arrived(name, true);
			Logger.LogInfo("Special Orders loaded.");
		}

		internal static void Log(string s) { if (Instance != null) Instance.Logger.LogInfo(s); }
		internal static void Warn(string s) { if (Instance != null) Instance.Logger.LogWarning(s); }
		internal static void Notify(string title, string text) { if (Notifications.instance != null) Notifications.instance.CreateNotification(title, text); }

		internal static bool InGame { get { return PlayerStats.instance != null && SceneManager.GetActiveScene().name != "Main Menu"; } }

		// ---- The shop you're at ----

		internal sealed class Offer
		{
			public string Name;
			public Furniture Furniture;
			public Item Item;
			public float StockChance;   // how often the shop has it in stock (0..1)
			public bool Rare;
			public float Fee;           // ordering fee share
			public int Oc;              // price of one, with the fee
			public float Rm;
			public float Days;
			public int Amount = 1;
		}

		internal static string ShopName;
		internal static string ShopKey;
		internal static List<Offer> Offers = new List<Offer>();


		// The shop the catalogue was opened for (from the shopkeeper's conversation), or null for "orders on the way".
		internal static object WindowShop;
		internal static object CurrentShop() { return WindowShop; }

		// A shop screen the game has open right now (trade screen or furniture shop menu), for the button.
		internal static object OpenShopScreen()
		{
			try
			{
				if (TradePanel.instance != null && TradePanel.instance.activeTrade != null) return AllowsItems ? TradePanel.instance.activeTrade : null;
				if (ShopController.instance != null && ShopController.instance.menuIsOpen && ShopController.instance.currentShop is FurnitureShop)
					return AllowsFurniture ? ShopController.instance.currentShop : null;
			}
			catch (Exception) { }
			return null;
		}

		internal static void OpenFor(object shop)
		{
			WindowShop = shop;
			RefreshOffers(shop);
			Window.ResetView();
			Window.SetOpen(true);
		}

		internal static bool AllowsFurniture { get { return Kinds.Value != OrderKinds.Items; } }
		internal static bool AllowsItems { get { return Kinds.Value != OrderKinds.Furniture; } }

		internal static void RefreshOffers(object shop)
		{
			Offers.Clear();
			ShopName = null; ShopKey = null;
			if (shop == null) return;
			try
			{
				if (shop is FurnitureShop fs) { if (AllowsFurniture) FurnitureOffers(fs); }
				else if (shop is Trade tr) { if (AllowsItems) ItemOffers(tr); }
			}
			catch (Exception e) { Warn("Catalogue: " + e.Message); }
			Offers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		}

		private static float Discount()
		{
			if (!HagglingDiscount.Value) return 0f;
			try
			{
				Type t = AccessTools.TypeByName("HagglingMod.HagglingPlugin");
				PropertyInfo p = t != null ? AccessTools.Property(t, "Discount") : null;
				return p != null ? Mathf.Clamp((float)p.GetValue(null, null), 0f, 0.5f) : 0f;
			}
			catch (Exception) { return 0f; }
		}

		private static void FurnitureOffers(FurnitureShop shop)
		{
			ShopName = !string.IsNullOrEmpty(shop.title) ? shop.title : shop.name;
			ShopKey = shop.gameObject.scene.name + "/" + ShopName;
			var lists = Traverse.Create(shop).Field("itemsLists").GetValue<List<FurnitureShopItemsList>>();
			if (lists == null) return;
			// Chance a piece is in stock after a restock: each list picks a few of its pieces at random.
			var notIn = new Dictionary<string, float>();
			var byId = new Dictionary<string, Furniture>();
			foreach (FurnitureShopItemsList l in lists)
			{
				if (l == null || l.furnitures == null) continue;
				int n = 0;
				foreach (Furniture f in l.furnitures) if (f != null) n++;
				if (n == 0) continue;
				float picks = Mathf.Max(0f, (l.minItemAmount + Mathf.Max(l.minItemAmount, l.maxItemAmount - 1)) / 2f);   // Random.Range(min, max) on ints
				float p = Mathf.Clamp01(picks / n);
				foreach (Furniture f in l.furnitures)
				{
					if (f == null) continue;
					string id = f.Id ?? f.title;
					float cur; if (!notIn.TryGetValue(id, out cur)) cur = 1f;
					notIn[id] = cur * (1f - p);
					byId[id] = f;
				}
			}
			float disc = Discount();
			foreach (var kv in byId)
			{
				Furniture f = kv.Value;
				float chance = 1f - notIn[kv.Key];
				float fee = Mathf.Lerp(FurnitureMinFee.Value, FurnitureMaxFee.Value, 1f - chance);
				float mult = (1f + fee) * (1f - disc);
				Offers.Add(new Offer
				{
					Name = f.title, Furniture = f, StockChance = chance, Rare = chance < 0.35f, Fee = fee,
					Oc = Mathf.CeilToInt(shop.GetOcBuyPriceOf(f) * mult), Rm = Mathf.Round(shop.GetRmBuyPriceOf(f) * mult * 100f) / 100f,
					Days = Mathf.Lerp(MinDays.Value, Mathf.Max(MinDays.Value, MaxDays.Value), 0.5f) + RareExtraDays.Value * (1f - chance)
				});
			}
		}

		private static void ItemOffers(Trade trade)
		{
			Storage st = Traverse.Create(trade).Field("storage").GetValue<Storage>();
			ShopName = !string.IsNullOrEmpty(trade.title) ? trade.title : (st != null && !string.IsNullOrEmpty(st.title) ? st.title : trade.name);
			ShopKey = trade.gameObject.scene.name + "/" + ShopName;
			if (st == null || string.IsNullOrEmpty(st.itemSpawnCategory) || StorageSpawnCategoryDatabase.instance == null) return;
			StorageSpawnCategory cat = StorageSpawnCategoryDatabase.instance.FetchStorageSpawnCategory(st.itemSpawnCategory);
			if (cat == null) return;
			float disc = Discount();
			bool rm = trade.purchasePriceMode == Trade.PriceMode.OnlyRM;
			var seen = new HashSet<int>();
			Action<List<int>, bool> add = (ids, rare) =>
			{
				if (ids == null) return;
				foreach (int id in ids)
				{
					if (!seen.Add(id)) continue;
					Item it = ItemDatabase.instance != null ? ItemDatabase.instance.FetchItemByID(id) : null;
					if (it == null || id <= 1) continue;   // not money
					float fee = rare ? ItemRareFee.Value : ItemCommonFee.Value;
					float baseOc = Trade.GetItemPrice(it, -1, it.CloneMeta(), Money.Currency.OC, trade.basePurchasePrice);
					float price = baseOc * (1f + fee) * (1f - disc);
					var o = new Offer
					{
						Name = it.Title, Item = it, Rare = rare, Fee = fee, StockChance = rare ? 0.2f : 0.8f,
						Days = Mathf.Lerp(MinDays.Value, Mathf.Max(MinDays.Value, MaxDays.Value), 0.5f) + (rare ? RareExtraDays.Value : 0f)
					};
					if (rm) o.Rm = Mathf.Max(0.01f, Mathf.Round(Money.instance.OcToRmConvert(price) * 100f) / 100f);
					else o.Oc = Mathf.Max(1, Mathf.CeilToInt(price));
					Offers.Add(o);
				}
			};
			add(cat.ItemCommon, false);
			add(cat.ItemRare, true);
		}

		// ---- Orders on the way (read from the game's own mail queue) ----

		internal struct OnTheWay { public string What; public string Shop; public int Minutes; public bool Arrived; }

		internal static List<OnTheWay> OrdersOnTheWay()
		{
			var list = new List<OnTheWay>();
			try
			{
				var mc = MailController.instance;
				if (mc == null) return list;
				foreach (var kv in mc.unprocessedMail)
				{
					var m = kv.Key;
					if (m == null || m.mailName == null || !m.mailName.StartsWith(MailPrefix)) continue;
					list.Add(Parse(m.mailName, kv.Value != null ? kv.Value.remainingInSeconds / 60 : 0, false));
				}
				foreach (var m in mc.receivedMail)
					if (m != null && m.mailName != null && m.mailName.StartsWith(MailPrefix)) list.Add(Parse(m.mailName, 0, true));
				foreach (var m in mc.receivedPackages)
					if (m != null && m.mailName != null && m.mailName.StartsWith(MailPrefix)) list.Add(Parse(m.mailName, 0, true));
			}
			catch (Exception) { }
			list.Sort((a, b) => a.Arrived != b.Arrived ? (a.Arrived ? -1 : 1) : a.Minutes.CompareTo(b.Minutes));
			return list;
		}

		// Mail name: "Special order: <what> (from <shop>)"
		private static OnTheWay Parse(string name, int minutes, bool arrived)
		{
			string body = name.Length > MailPrefix.Length + 2 ? name.Substring(MailPrefix.Length + 2) : name;
			string shop = "";
			int at = body.LastIndexOf(" (from ");
			if (at > 0 && body.EndsWith(")")) { shop = body.Substring(at + 7, body.Length - at - 8); body = body.Substring(0, at); }
			return new OnTheWay { What = body, Shop = shop, Minutes = minutes, Arrived = arrived };
		}

		private static void Arrived(string mailName, bool locker)
		{
			try
			{
				if (!ArrivalNotice.Value || string.IsNullOrEmpty(mailName) || !mailName.StartsWith(MailPrefix)) return;
				var o = Parse(mailName, 0, true);
				Notify("Special order arrived", o.What + (o.Shop.Length > 0 ? " from " + o.Shop : "") + " is waiting at your " + (locker ? "parcel locker" : "mailbox") + ".");
			}
			catch (Exception) { }
		}

		// "next arrives in 5h", "1 waiting", for the mailbox / parcel locker text.
		internal static string DeliverySummary(bool locker)
		{
			int waiting = 0, coming = 0, next = int.MaxValue;
			try
			{
				var mc = MailController.instance;
				foreach (var m in locker ? mc.receivedPackages : mc.receivedMail)
					if (m != null && m.mailName != null && m.mailName.StartsWith(MailPrefix)) waiting++;
				foreach (var o in OrdersOnTheWay())
					if (!o.Arrived) { coming++; next = Mathf.Min(next, o.Minutes); }
			}
			catch (Exception) { }
			var parts = new List<string>();
			if (waiting > 0) parts.Add(waiting + " special order" + (waiting > 1 ? "s" : "") + " here");
			if (coming > 0) parts.Add(coming + " on the way, next in " + When(next));
			return string.Join(", ", parts.ToArray());
		}

		internal static int OpenOrdersFrom(string shop)
		{
			int n = 0;
			foreach (var o in OrdersOnTheWay()) if (!o.Arrived && o.Shop == shop) n++;
			return n;
		}

		// ---- Placing an order ----

		internal static void PlaceOrder(Offer o, bool invoice)
		{
			if (o == null || ShopName == null) return;
			if (OpenOrdersFrom(ShopName) >= MaxOpenOrdersPerShop.Value)
			{
				Notify("Special orders", ShopName + " already has " + MaxOpenOrdersPerShop.Value + " orders on the way for you.");
				return;
			}
			int amount = o.Item != null ? Mathf.Clamp(o.Amount, 1, MaxItemAmount.Value) : 1;
			int oc = o.Oc * amount;
			float rm = o.Rm * amount;
			if (invoice)
			{
				oc = Mathf.CeilToInt(oc * (1f + InvoiceExtraFee.Value));
				rm = Mathf.Round(rm * (1f + InvoiceExtraFee.Value) * 100f) / 100f;
			}
			else
			{
				if ((oc > 0 && !Money.instance.CheckMoney(oc, Money.Currency.OC)) || (rm > 0f && !Money.instance.CheckMoney(rm, Money.Currency.RM)))
				{
					Notify("Special orders", "You can't afford that: " + Price(oc, rm) + ".");
					return;
				}
				if (oc > 0) Money.instance.RemoveMoney(oc, Money.Currency.OC);
				if (rm > 0f) Money.instance.RemoveMoney(rm, Money.Currency.RM);
			}

			float days = Mathf.Max(0.05f, o.Days + UnityEngine.Random.Range(-0.25f, 0.25f));
			var delay = new TimeAndDay { hours = Mathf.Floor(days * 24f), minutes = Mathf.Round((days * 24f - Mathf.Floor(days * 24f)) * 60f) };
			string what = o.Item != null && amount > 1 ? o.Name + " x" + amount : o.Name;
			string mailName = MailPrefix + ": " + what + " (from " + ShopName + ")";
			try
			{
				if (o.Furniture != null)
				{
					var mail = new MailController.FurnitureMail { mailName = mailName, furniture = o.Furniture };
					MailController.instance.SendMail(mail, FurnitureTo.Value == DeliverTo.ParcelLocker ? MailController.Target.PlayerParcelLocker : MailController.Target.Player, delay);
				}
				else
				{
					var mail = new MailController.ItemMail { mailName = mailName, item = new ItemReference(o.Item), amount = amount, meta = o.Item.CloneMeta() };
					MailController.instance.SendMail(mail, ItemsTo.Value == DeliverTo.ParcelLocker ? MailController.Target.PlayerParcelLocker : MailController.Target.Player, delay);
				}
			}
			catch (Exception e)
			{
				Warn("Order failed: " + e.Message);
				if (!invoice) { if (oc > 0) Money.instance.AddMoney(oc, Money.Currency.OC); if (rm > 0f) Money.instance.AddMoney(rm, Money.Currency.RM); }
				Notify("Special orders", "The order didn't go through. You weren't charged.");
				return;
			}
			if (invoice)
			{
				try { InvoiceController.instance.CreateInvoice(ShopName, new Money.money(oc, rm), InvoiceDays.Value); }
				catch (Exception e) { Warn("Invoice: " + e.Message); }
			}
			bool locker = (o.Furniture != null ? FurnitureTo.Value : ItemsTo.Value) == DeliverTo.ParcelLocker;
			Notify("Special order placed", what + " from " + ShopName + ": " + Price(oc, rm) + (invoice ? " (invoice to pay within " + InvoiceDays.Value + " days)" : "") +
			       ". Arrives at your " + (locker ? "parcel locker" : "mailbox") + " in about " + When(Mathf.RoundToInt(days * 1440f)) + ".");
		}

		internal static string Price(int oc, float rm)
		{
			var parts = new List<string>();
			if (oc > 0) parts.Add(oc.ToString(CultureInfo.InvariantCulture) + " OC");
			if (rm > 0f) parts.Add(rm.ToString("0.00", CultureInfo.InvariantCulture) + " RM");
			return parts.Count == 0 ? "free" : string.Join(" + ", parts.ToArray());
		}

		internal static string When(int minutes)
		{
			if (minutes <= 0) return "now";
			int d = minutes / 1440, h = (minutes % 1440) / 60;
			return d > 0 ? d + "d " + h + "h" : Mathf.Max(1, h) + "h";
		}

		internal static string RarityWord(Offer o)
		{
			if (o.Item != null) return o.Rare ? "Rare" : "Common";
			if (o.StockChance >= 0.75f) return "Common";
			if (o.StockChance >= 0.45f) return "Uncommon";
			if (o.StockChance >= 0.2f) return "Rare";
			return "Very rare";
		}

		// ---- Update: open/close the window ----

		private void Update()
		{
			try
			{
				if (!InGame) { if (Window.Open) Window.SetOpen(false); return; }
				if (OrdersKey.Value.MainKey != KeyCode.None && OrdersKey.Value.IsDown())
				{
					if (Window.Open) Window.SetOpen(false);
					else { WindowShop = null; RefreshOffers(null); Window.SetOpen(true); }
				}
				else if (Window.Open && Input.GetKeyDown(KeyCode.Escape)) Window.CloseLater = true;
			}
			catch (Exception e) { Warn("Special Orders: " + e.Message); }
		}

		private void LateUpdate() { Window.LateTick(); }

		private void OnGUI()
		{
			try { Window.Draw(); } catch (Exception e) { Warn("Special Orders UI: " + e.Message); }
		}
	}

	internal static class FurnitureShopExt
	{
		// Same as the shop's own GetOcBuyPrice / GetRmBuyPrice, for a piece that isn't in stock.
		internal static float GetOcBuyPriceOf(this FurnitureShop shop, Furniture f) { return f.PriceOC * (shop.buyPercent / 100f); }
		internal static float GetRmBuyPriceOf(this FurnitureShop shop, Furniture f) { return f.PriceRM * (shop.buyPercent / 100f); }
	}

	internal sealed class ConfigurationManagerAttributes
	{
		public int? Order;
		public bool? IsAdvanced;
	}
}
