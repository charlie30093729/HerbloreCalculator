Herblore Calculator for Old School RuneScape
A C# console application that pulls live Grand Exchange price data from the OSRS Wiki API to calculate potion crafting costs, profit margins, XP per hour, and more,all in real time.

✨ Features
Live OSRS Market Data – Fetches up-to-date high/low prices for any item via the OSRS Wiki API.

Potion Profit & XP Calculator – Calculates cost per potion, GP/XP, GP/hour, and profit to target XP.

GE Offer Helper – Suggests optimal buy/sell offers for potion ingredients based on current market trends.

Custom Item Tracker – Track the prices of any item ID in real time (e.g., Blood Shards).

Automated Refresh – Updates every 60 seconds with clear, formatted output.

Smart Pricing – Automatically determines reasonable “patient” buy prices instead of just averages.


-TODO: implement vyrewatch alternate accounts to tracker


🕹 Usage
Update your RuneScape name in Program.cs to automatically fetch your current Herblore XP.

Add or remove potions by modifying the potions list in Program.cs.

Add any item IDs you want to track in the TrackedItems list.

📜 Version History

v1.5 – Added potion ingredient suggested pricing, fixed refresh bug, fixed window size and formatting.

v1.4 – Formatting improvements, more code comments.

v1.3 – Added custom item tracker, better expandability and maintainability.

v1.2 – Improved potion class for easier expansion.

v1.1 – Added RuneScape name puller, XP tracker, decanting calculations.

v1.0 – Initial release with 2 potions and live data pull.
