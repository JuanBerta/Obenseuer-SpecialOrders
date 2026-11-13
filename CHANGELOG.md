# Changelog

## 1.0.6
- Fixed: Welhö stayed at the counter after a special order. The option now leaves the conversation like goodbye does, then opens the catalogue.
- Corner Shops support: its vendors take special orders for their stock list items.
- No special order option for shops with nothing to order.

## 1.0.5
- Type the amount of an item to order, or use - and + (Shift for steps of 10) and MAX. Up to 100 per order by default (setting MaxItemsPerOrder, up to 9999).

## 1.0.4
- Fixed: the special order option was missing in many traders' conversations (Sirkku's Flower Stall, Selma's Stall and others). A trader's stall is now found by its owner and name, from where you stand, within 15 m (setting ShopSearchRange).

## 1.0.3
- Shop search from the player's position, retried on each menu; detailed log line for each conversation.

## 1.0.2
- Fixed: the special order option was missing from some traders' conversations (for example ones starting with "What are you selling?"), and after the first conversation it could stop appearing at all.
- Finds the trader's stall more reliably (the stall that person owns, or the nearest one within 8 m).

## 1.0.1
- Market stalls (like Petro's Log and Block) and furniture shop counters can now take orders. They have no conversation of their own, so using them now starts a short one: trade (the game's screen, as before), "I'd like to place a special order.", or goodbye. Setting: CounterConversation.
- Optional "Special order" button on trade screens and furniture shop menus (off by default). Setting: ShopScreenButton.

## 1.0.0
- First release.
