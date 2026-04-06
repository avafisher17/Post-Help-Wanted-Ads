using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace PostHelpWantedAds
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private ModData _moddedData;
        private string _playerInput = "";
        private NamingMenu _namingMenu = null;
        private bool _hasShownBillboardMessage = false;
        private int _postedItem = 0;

        public override void Entry(IModHelper helper)
        {
            // Hook events here (like DayStarted, ButtonPressed, etc.)
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;

            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Display.MenuChanged += OnMenuChanged;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
        }
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _moddedData = Helper.Data.ReadSaveData<ModData>("PostHelpWantedAds") ?? new ModData();
        }
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            //if (_moddedData.DidPostAd)
            //{
            //   
            //}

            _moddedData.DidPostAd = false; // reset daily state
            Monitor.Log("New day started, reset DidPostAd.", LogLevel.Info);
        }
       
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Only care about world-ready and a specific key
            if (!Context.IsWorldReady || e.Button != SButton.P) // P for “Post Ad”
                return;

            // Only activate while Billboard menu is open
            if (Game1.activeClickableMenu == null || Game1.activeClickableMenu.GetType().Name != "Billboard")
            {
                return;
            }

            // Use reflection to check if "dailyQuestBoard" is active
            bool isHelpWanted = (bool)Game1.activeClickableMenu.GetType()
                .GetField("dailyQuestBoard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Game1.activeClickableMenu);

            if (!isHelpWanted)
            {
                return;
            }

            // Bring up text box for user input
            if (_moddedData.DidPostAd == false)
            {
                _namingMenu = new NamingMenu(
                    naming =>
                    {
                        _playerInput = naming;
                        Monitor.Log($"Player requested {_playerInput}", LogLevel.Info);
                        
                        string itemIdString = getItemId(_playerInput);

                        if (int.TryParse(itemIdString, out int itemId))
                        {
                            bool isAvailable = isAvailableForQuests(itemId);
                            if (!isAvailable)
                            {
                                _namingMenu.textBox.Text = "";
                            }
                            else
                            {
                                bool inSeason = isInSeason(itemId);
                                if (!inSeason)
                                {
                                    _namingMenu.textBox.Text = "";
                                }

                                if (isAvailable && inSeason)
                                {
                                    _postedItem = itemId;
                                    Monitor.Log($"_postedItem set to: {_postedItem}", LogLevel.Info);
                                    Game1.chatBox.addMessage("Ad posted for " + _playerInput! + ". Let's see if anyone responds.", Color.White);

                                    Game1.player.addQuest("PostHelpWantedAds.PostedAd");
                                    Helper.GameContent.InvalidateCache("Data/Quests");
                                    var quest = Game1.player.questLog.FirstOrDefault(q => q.id.Value == "PostHelpWantedAds.PostedAd");
                                    if (quest == null)
                                    {
                                        Monitor.Log("Quest was null after addQuest!", LogLevel.Warn);
                                    }

                                    _moddedData.DidPostAd = true;
                                    Helper.Data.WriteSaveData("PostHelpWantedAds", _moddedData);

                                    Game1.exitActiveMenu();
                                }
                            }
                        }
                        else
                        {
                            Game1.addHUDMessage(new HUDMessage("Invalid input entered! Try again", HUDMessage.error_type));
                            _namingMenu.textBox.Text = "";
                        }
                    },
                    "Enter desired item:",
                    "");

                _namingMenu.randomButton.bounds = new Rectangle(0, 0, 0, 0);
                _namingMenu.randomButton.visible = false;
                _namingMenu.randomButton.myID = -1;
                Game1.activeClickableMenu = _namingMenu;;
            }
            else
            {
                Monitor.Log("Already posted today.", LogLevel.Info);
                Game1.addHUDMessage(new HUDMessage("You've already posted an ad!", HUDMessage.error_type));
            }
        }
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // Format: "type/title/description/objective/[extra]/reward/cancelable/days"
                    data["PostHelpWantedAds.PostedAd"] = $"Basic/Help Wanted Ad Posted/I've posted an ad for a {_playerInput}. The more friends I have in town, the more likely someone is to respond, I think!/Wait for somebody to bring you a {_playerInput}/0/true/2";
                });
            }
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu?.GetType().Name != "Billboard")
            {
                _hasShownBillboardMessage = false;
                return;
            }

            bool isHelpWanted = (bool)Game1.activeClickableMenu.GetType()
                .GetField("dailyQuestBoard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Game1.activeClickableMenu);

            if (isHelpWanted && !_hasShownBillboardMessage)
            {
                Game1.chatBox.addMessage("Press P to post your own \"help wanted\" ad!", Color.Green);
                _hasShownBillboardMessage = true;
            }
        }

        private string getItemId(string playerInput)
        {
            Dictionary<string, string> nameItemLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in Game1.objectData)
            {
                nameItemLookup[pair.Value.Name] = pair.Key;
            }

            try
            {
                string itemIdString = nameItemLookup[playerInput];
                _playerInput = Game1.objectData[itemIdString].Name;

                return itemIdString;
            }
            catch
            {
                string itemIdString = "203"; // lol Stange Bun
                return itemIdString;
            }
            
        }

        

        private bool isAvailableForQuests(int itemId)
        {
            bool isAvailable = false;

            // Check for typical items
            Dictionary<int, string> eligibleDefaultItems = new Dictionary<int, string>
            {
                // FORAGE

                { 16,  "Wild Horseradish" },{ 18,  "Daffodil" },{ 20,  "Leek" },{ 22,  "Dandelion" },
                { 257, "Morel" },{ 399, "Spring Onion" }, { 396, "Spice Berry" },{ 398, "Grape" },
                { 402, "Sweet Pea" },{ 404, "Common Mushroom" },{ 406, "Wild Plum" },{ 408, "Hazelnut" },
                { 412, "Winter Root" },{ 414, "Crystal Fruit" },{ 416, "Snow Yam" },{ 418, "Crocus" },
                { 78,  "Cave Carrot" },{ 283, "Holly"},{ 392, "Nautilus Shell"},{ 393, "Coral"},
                { 394, "Sea Urchin"},{ 397, "Rainbow Shell"},{ 153, "Green Algae"},{ 152, "Seaweed"},
                { 157, "White Algae"},{ 410, "Blackberry"},{ 296, "Salmonberry"},

                // CROPS (no fruit tree fruits)
 
                { 24,  "Parsnip" },{ 188, "Green Bean" },{ 190, "Cauliflower" },{ 192, "Potato" },
                { 250, "Kale" },{ 591, "Tulip" },{ 597, "Blue Jazz" },{ 254, "Melon" },
                { 256, "Tomato" },{ 258, "Hot Pepper" },{ 260, "Blueberry" },{ 270, "Corn" },
                { 304, "Hops" },{ 421, "Sunflower" },{ 593, "Summer Spangle" },{ 376, "Poppy" },
                { 272, "Eggplant" },{ 278, "Bok Choy" },{ 280, "Yam" },{ 282, "Cranberries" },
                { 300, "Amaranth" },{ 302, "Grape" },{ 595, "Fairy Rose" },{ 264, "Radish" },
                { 276, "Pumpkin" },{ 262, "Wheat" },

                // OTHERS
                { 174, "Egg" },{ 176, "Large Egg" },{ 184, "Milk" },{ 186, "Large Milk" },
                { 436, "Goat Milk" },{ 438, "L. Goat Milk" },{ 330, "Clay" },{ 723, "Oyster" },
                { 719, "Mussel" },{ 718, "Cockle" }
            };

            if (eligibleDefaultItems.ContainsKey(itemId))
            {
                isAvailable = true;
                return isAvailable;
            }
            else
            {
                // Check for items that must be shipped before requesting
                Dictionary<int, string> eligibleShippedItems = new Dictionary<int, string>
                {
                    { 266, "Red Cabbage" },{ 965, "Carrot" },{ 967, "Summer Squash" },{ 969, "Broccoli" },
                    { 971, "Powdermelon" },{ 281, "Chanterelle" }, { 420, "Red Mushroom" },{ 422, "Purple Mushroom" },
                    { 88,  "Coconut" },{ 90,  "Cactus Fruit" },{ 193, "Garlic" },{ 252, "Rhubarb" },
                    { 268, "Starfruit" },{ 274, "Artichoke" },{ 284, "Beet" },{ 259, "Fiddlehead Fern"},
                    { 400, "Strawberry" },{ 271, "Unmilled Rice" },{ 830, "Taro Root" },{ 832, "Pineapple" },
                    { 851, "Magma Cap" },{ 852, "Dragon Tooth" },{ 829, "Ginger" },{ 815, "Tea Leaves" },
                    { 787, "Battery Pack" },{ 338, "Refined Quartz" },{ 334, "Copper Bar" },{ 335, "Iron Bar" },
                    { 336, "Gold Bar" },{ 82, "Fire Quartz" },{ 80, "Quartz" },{ 72, "Diamond" },
                    { 70, "Jade" },{ 68, "Topaz" },{ 66, "Amethyst" },{ 64, "Ruby" },
                    { 62, "Aquamarine" },{ 60, "Emerald" },{ 86, "Earth Crystal" },{ 84, "Frozen Tear" }
                };

                if (eligibleShippedItems.ContainsKey(itemId))
                {
                    bool hasShipped = Game1.player.basicShipped.ContainsKey($"(O){itemId}");
                    if (hasShipped)
                    {
                        isAvailable = true;
                        return isAvailable;
                    }
                    else
                    {
                        Game1.addHUDMessage(new HUDMessage("You haven't encountered that item yet", HUDMessage.error_type));
                    }
                }
                else
                {
                    // Check for fish that have been caught
                    Dictionary<int, string> eligibleFish = new Dictionary<int, string>
                    {
                        { 128, "Pufferfish" },{ 129, "Anchovy" },{ 130, "Tuna" },{ 131, "Sardine" },
                        { 132, "Bream" },{ 136, "Largemouth Bass" },{ 137, "Smallmouth Bass" },{ 138, "Rainbow Trout" },
                        { 140, "Walleye" },{ 142, "Carp" },{ 143, "Catfish" },{ 144, "Pike" },
                        { 145, "Sunfish" },{ 146, "Red Mullet" },{ 147, "Herring" },{ 148, "Eel" },
                        { 149, "Octopus" },{ 150, "Red Snapper" },{ 151, "Squid" },{ 154, "Sea Cucumber" },
                        { 155, "Super Cucumber" },{ 156, "Ghostfish" },{ 158, "Stonefish" },{ 161, "Ice Pip" },
                        { 162, "Lava Eel" },{ 164, "Sandfish" },{ 165, "Scorpion Carp" },{ 734, "Woodskip" },
                        { 698, "Sturgeon" },{ 699, "Tiger Trout" },{ 700, "Bullhead" },{ 701, "Tilapia" },
                        { 702, "Chub" },{ 704, "Dorado" },{ 705, "Albacore" },{ 706, "Shad" },
                        { 707, "Lingcod" },{ 708, "Halibut" },{ 798, "Midnight Carp" },{ 722, "Periwinkle" },
                        { 715, "Lobster" },{ 716, "Crayfish" },{ 717, "Crab" },{ 720, "Shrimp" },
                        { 721, "Snail" },{ 836, "Stingray" },{ 837, "Lionfish" },{ 838, "Blue Discus" },
                        { 267, "Flounder" },{ 139, "Salmon" },{ 141, "Perch" }
                    };

                    if (eligibleFish.ContainsKey(itemId))
                    {
                        bool hasCaught = Game1.player.fishCaught.ContainsKey($"(O){itemId}");
                        if (hasCaught)
                        {
                            isAvailable = true;
                            return isAvailable;
                        }
                        else
                        {
                            Game1.addHUDMessage(new HUDMessage("You haven't caught that fish yet", HUDMessage.error_type));
                        }
                    }
                    else
                    {
                        Game1.addHUDMessage(new HUDMessage("Sorry, that item isn't available", HUDMessage.error_type));
                    }
                }
            }
            return isAvailable;
        }

        private bool isInSeason(int itemId)
        {
            bool inSeason = false;

            Dictionary<int, string> alwaysInSeason = new Dictionary<int, string>
            {
                { 420, "Red Mushroom" },{ 422, "Purple Mushroom" },{ 88,  "Coconut" },{ 90,  "Cactus Fruit" },
                { 851, "Magma Cap" },{ 852, "Dragon Tooth" },{ 829, "Ginger" },{ 815, "Tea Leaves" },
                { 787, "Battery Pack" },{ 338, "Refined Quartz" },{ 334, "Copper Bar" },{ 335, "Iron Bar" },
                { 336, "Gold Bar" },{ 82, "Fire Quartz" },{ 80, "Quartz" },{ 72, "Diamond" },
                { 70, "Jade" },{ 68, "Topaz" },{ 66, "Amethyst" },{ 64, "Ruby" },
                { 62, "Aquamarine" },{ 60, "Emerald" },{ 78,  "Cave Carrot" },{ 393, "Coral"},
                { 394, "Sea Urchin"},{ 153, "Green Algae"},{ 152, "Seaweed"},{ 157, "White Algae"},
                { 174, "Egg" },{ 176, "Large Egg" },{ 184, "Milk" },{ 186, "Large Milk" },
                { 436, "Goat Milk" },{ 438, "L. Goat Milk" },{ 330, "Clay" },{ 723, "Oyster" },
                { 719, "Mussel" },{ 718, "Cockle" },{ 86, "Earth Crystal" },{ 84, "Frozen Tear" },
                { 132, "Bream" },{ 136, "Largemouth Bass" },{ 142, "Carp" },
                { 156, "Ghostfish" },{ 158, "Stonefish" },{ 161, "Ice Pip" },
                { 162, "Lava Eel" },{ 164, "Sandfish" },{ 165, "Scorpion Carp" },{ 734, "Woodskip" },
                { 700, "Bullhead" },{ 702, "Chub" },{ 722, "Periwinkle" },
                { 715, "Lobster" },{ 716, "Crayfish" },{ 717, "Crab" },{ 720, "Shrimp" },
                { 721, "Snail" },{ 836, "Stingray" },{ 837, "Lionfish" },{ 838, "Blue Discus" }
            };

            Dictionary<int, string> springSeasonals = new Dictionary<int, string>
            {
                { 16,  "Wild Horseradish" },{ 18,  "Daffodil" },{ 20,  "Leek" },{ 22,  "Dandelion" },
                { 257, "Morel" },{ 399, "Spring Onion" },{ 24,  "Parsnip" },{ 188, "Green Bean" },
                { 190, "Cauliflower" },{ 192, "Potato" },{ 250, "Kale" },{ 591, "Tulip" },
                { 597, "Blue Jazz" },{ 965, "Carrot" },{ 193, "Garlic" },{ 252, "Rhubarb" },
                { 400, "Strawberry" },{ 271, "Unmilled Rice" },{ 129, "Anchovy" },{ 131, "Sardine" },
                { 137, "Smallmouth Bass" },{ 143, "Catfish" },{ 145, "Sunfish" },{ 147, "Herring" },
                { 148, "Eel" },{ 706, "Shad" },{ 708, "Halibut" },{ 267, "Flounder" },{ 296, "Salmonberry"}
            };

            Dictionary<int, string> summerSeasonals = new Dictionary<int, string>
            {
                { 396, "Spice Berry" },{ 398, "Grape" },{ 402, "Sweet Pea" },{ 254, "Melon" },
                { 256, "Tomato" },{ 258, "Hot Pepper" },{ 260, "Blueberry" },{ 270, "Corn" },
                { 304, "Hops" },{ 421, "Sunflower" },{ 593, "Summer Spangle" },{ 376, "Poppy" },
                { 264, "Radish" },{ 262, "Wheat" },{ 266, "Red Cabbage" },{ 967, "Summer Squash" },
                { 268, "Starfruit" },{ 259, "Fiddlehead Fern"},{ 830, "Taro Root" },{ 832, "Pineapple" },
                { 128, "Pufferfish" },{ 130, "Tuna" },{ 138, "Rainbow Trout" },{ 397, "Rainbow Shell"},
                { 144, "Pike" },{ 145, "Sunfish" },{ 146, "Red Mullet" },{ 149, "Octopus" },
                { 150, "Red Snapper" },{ 155, "Super Cucumber" },{ 698, "Sturgeon" },{ 701, "Tilapia" },
                { 704, "Dorado" },{ 706, "Shad" },{ 708, "Halibut" },{ 267, "Flounder" }
            };

            Dictionary<int, string> fallSeasonals = new Dictionary<int, string>
            {
                { 404, "Common Mushroom" },{ 406, "Wild Plum" },{ 408, "Hazelnut" },{ 410, "Blackberry"},
                { 270, "Corn" },{ 421, "Sunflower" },{ 272, "Eggplant" },{ 278, "Bok Choy" },
                { 280, "Yam" },{ 282, "Cranberries" },{ 300, "Amaranth" },{ 302, "Grape" },
                { 595, "Fairy Rose" },{ 276, "Pumpkin" },{ 262, "Wheat" },{ 969, "Broccoli" },
                { 274, "Artichoke" },{ 284, "Beet" },
                { 129, "Anchovy" },{ 131, "Sardine" },{ 137, "Smallmouth Bass" },{ 140, "Walleye" },
                { 143, "Catfish" },{ 148, "Eel" },{ 150, "Red Snapper" },{ 154, "Sea Cucumber" },
                { 155, "Super Cucumber" },{ 699, "Tiger Trout" },{ 701, "Tilapia" },{ 705, "Albacore" },
                { 706, "Shad" },{ 798, "Midnight Carp" },{ 139, "Salmon" }
            };

            Dictionary<int, string> winterSeasonals = new Dictionary<int, string>
            {
                { 412, "Winter Root" },{ 414, "Crystal Fruit" },{ 416, "Snow Yam" },{ 418, "Crocus" },
                { 283, "Holly"},{ 392, "Nautilus Shell"},{ 971, "Powdermelon" },
                { 130, "Tuna" },{ 131, "Sardine" },{ 146, "Red Mullet" },{ 147, "Herring" },
                { 149, "Octopus" },{ 151, "Squid" },{ 154, "Sea Cucumber" },{ 155, "Super Cucumber" },
                { 141, "Perch" },{ 698, "Sturgeon" },{ 705, "Albacore" },{ 707, "Lingcod" },
                { 708, "Halibut" },{ 798, "Midnight Carp" }
            };

            if (alwaysInSeason.ContainsKey(itemId))
            {
                inSeason = true;
                return inSeason;
            }

            if (Game1.season == Season.Spring)
            {
                if (springSeasonals.ContainsKey(itemId))
                {
                    inSeason = true;
                    return inSeason;
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("That item isn't in season", HUDMessage.error_type));
                    return inSeason;
                }
            }

            if (Game1.season == Season.Summer)
            {
                if (summerSeasonals.ContainsKey(itemId))
                {
                    inSeason = true;
                    return inSeason;
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("That item isn't in season", HUDMessage.error_type));
                    return inSeason;
                }
            }

            if (Game1.season == Season.Fall)
            {
                if (fallSeasonals.ContainsKey(itemId))
                {
                    inSeason = true;
                    return inSeason;
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("That item isn't in season", HUDMessage.error_type));
                    return inSeason;
                }
            }
            if (Game1.season == Season.Winter)
            {
                if (winterSeasonals.ContainsKey(itemId))
                {
                    inSeason = true;
                    return inSeason;
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("That item isn't in season", HUDMessage.error_type));
                    return inSeason;
                }
            }

            return inSeason;
        }
    }
}