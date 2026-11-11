# Special Orders

Order furniture and items that a shop doesn't have in stock right now, and get them delivered a few days later. No more waiting for a restock to finally drop that Malter or Biogas Reactor.

## Features
- **Ask the shopkeeper.** Talk to a trader or the person at a furniture shop and pick *"I'd like to place a special order."* Market stalls and furniture shop counters, which have no conversation of their own, get a short one: using them asks whether you want to trade, place a special order, or say goodbye. The order catalogue opens with everything that shop *can* stock, including what isn't in stock now. Only that shop's own lists, so you still go to the right merchant.
- **Furniture, items, or both:** choose in the settings what can be ordered. Furniture comes from furniture shops, items from traders.
- **Rarer things cost more.** Furniture: a +25% ordering fee for pieces the shop almost always has, up to +100% for the rarest, based on how often the shop really stocks each piece. Items cost more to order: +50% for a trader's common stock, +150% for its rare stock.
- **Rarer things take longer:** 1-2 in-game days, plus up to 2 days for the rarest.
- **Pay up front, by invoice, or choose each time.** An invoice is the game's own (pay it within 7 days at a payment machine) and costs +10%.
- **Delivered by the game's own mail**, to your mailbox or the parcel locker (your choice, for furniture and items separately). Delivery timers are the game's and are saved with your game. Furniture arrives ready to place, like when you buy it.
- **Know when it's coming:** the order window lists what's on the way, from where and when; **F8** shows that list anywhere (it can't order). You get a notification when an order arrives, and looking at your mailbox or the parcel locker shows what's waiting and when the next one arrives.
- Up to 3 orders on the way per shop at a time.
- With **Haggling**, your Haggling discount applies to orders too.

Every number can be changed in the config file or with ConfigurationManager.

## Install
1. Install BepInEx 6 (Unity.Mono, x64) for Obenseuer.
2. Put `Obenseuer-SpecialOrders.dll` in `BepInEx\plugins\Obenseuer-SpecialOrders\`.

## Build
`dotnet build -c Release` (set `/p:ObenseuerDir=...` if the game is not in the default Steam folder).

Idea from the Obenseuer Discord community. Made by JuanBerta, with AI assistance (Claude).
