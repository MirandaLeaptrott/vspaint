using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VSpaint
{
    public class ItemPaintbrush : Item
    {
        // Brush variant name -> palette index. Independent of the VS dye color names;
        // see DyeToVariantMap in CollectibleBehaviorPaintbrushDip for that mapping.
        private static readonly Dictionary<string, int> PotColorMap = new Dictionary<string, int>
        {
            { "white",     0 },
            { "black",     1 },
            { "darkgrey",  2 },
            { "lightgrey", 3 },
            { "red",       4 },
            { "darkred",   5 },
            { "orange",    6 },
            { "yellow",    7 },
            { "green",     8 },
            { "darkgreen", 9 },
            { "cyan",      10 },
            { "blue",      11 },
            { "darkblue",  12 },
            { "purple",    13 },
            { "brown",     14 },
            { "pink",      15 },
        };

        // Key "brushColor|dyeColor" -> result. Anything missing falls through to the
        // rule-based fallbacks in TryMix (brown sink, secondary-mixing rules).
        private static readonly Dictionary<string, string> MixMap =
            new Dictionary<string, string>
        {
            { "white|black",     "darkgrey"  },
            { "lightgrey|black", "darkgrey"  },
            { "red|black",       "darkred"   },
            { "green|black",     "darkgreen" },
            { "blue|black",      "darkblue"  },
            { "black|white",     "darkgrey"  },
            { "darkgrey|white",  "lightgrey" },
            { "darkred|white",   "red"       },
            { "darkgreen|white", "green"     },
            { "darkblue|white",  "blue"      },
            { "red|white",       "pink"      },
            { "white|red",       "pink"      },
            { "red|blue",        "purple" },
            { "blue|red",        "purple" },
            { "red|yellow",      "orange" },
            { "yellow|red",      "orange" },
            { "blue|yellow",     "green"  },
            { "yellow|blue",     "green"  },
            { "green|blue",      "cyan"   },
            { "blue|green",      "cyan"   },
        };

        public static string TryMix(string brushColor, string dyeColor)
        {
            if (brushColor == null || dyeColor == null) return null;

            if (MixMap.TryGetValue(brushColor + "|" + dyeColor, out string result))
                return result;

            // Brown acts as a sink: any further dye except white locks it to black.
            if (brushColor == "brown" && dyeColor != "white")
                return "black";

            // Secondaries muddy to brown on any unmapped dye (white/black get their
            // own entries above so they don't hit this).
            if ((brushColor == "purple" || brushColor == "orange" || brushColor == "green")
                && dyeColor != "white" && dyeColor != "black")
                return "brown";

            return null;
        }

        public static bool IsClean(ItemStack s)
        {
            if (s?.Collectible == null) return true;
            s.Collectible.Variant.TryGetValue("color", out string color);
            return color == null || color == "empty";
        }

        public static string GetColorName(ItemStack s)
        {
            if (s?.Collectible == null) return null;
            if (!s.Collectible.Variant.TryGetValue("color", out string color)) return null;
            if (color == "empty" || !PotColorMap.ContainsKey(color)) return null;
            return color;
        }

        public static int GetColor(ItemStack s)
        {
            string colorName = GetColorName(s);
            if (colorName == null) return -1;
            return PotColorMap.TryGetValue(colorName, out int idx) ? idx : -1;
        }

        public static string GetState(ItemStack s)
        {
            if (IsClean(s)) return "clean";
            if (s?.Collectible == null) return "clean";
            s.Collectible.Variant.TryGetValue("dryness", out string dryness);
            return dryness ?? "wet";
        }

        public static bool IsWet(ItemStack s) => GetState(s) == "wet";
        public static bool IsDry(ItemStack s) => GetState(s) == "dry";

        public static void Dip(ItemSlot slot, string colorName, IWorldAccessor world)
        {
            var newItem = world.GetItem(new AssetLocation("vspaint", $"paintbrush-{colorName}-wet"));
            if (newItem == null)
            {
                world.Logger.Warning("[VSpaint] Dip: could not find item vspaint:paintbrush-{0}-wet", colorName);
                return;
            }
            var newStack = new ItemStack(newItem);
            newStack.Attributes.SetDouble("dippedHour", world.Calendar.TotalHours);
            slot.Itemstack = newStack;
            slot.MarkDirty();
        }

        public static void Wash(ItemSlot slot, IWorldAccessor world)
        {
            var cleanItem = world.GetItem(new AssetLocation("vspaint", "paintbrush-empty-wet"));
            if (cleanItem == null)
            {
                world.Logger.Warning("[VSpaint] Wash: could not find item vspaint:paintbrush-empty-wet");
                return;
            }
            slot.Itemstack = new ItemStack(cleanItem);
            slot.MarkDirty();
        }

        public static void CheckDrying(ItemSlot slot, IWorldAccessor world)
        {
            if (!VSpaintConfig.Current.EnableDryingMechanics) return;
            if (world?.Calendar == null) return;
            var stack = slot?.Itemstack;
            if (stack == null || IsClean(stack)) return;
            if (!stack.Collectible.Variant.TryGetValue("dryness", out string dryness) || dryness != "wet") return;

            double dippedHour = stack.Attributes.GetDouble("dippedHour", world.Calendar.TotalHours);
            if (world.Calendar.TotalHours - dippedHour < VSpaintConfig.Current.DryAfterHours) return;

            stack.Collectible.Variant.TryGetValue("color", out string color);
            var dryItem = world.GetItem(new AssetLocation("vspaint", $"paintbrush-{color}-dry"));
            if (dryItem == null)
            {
                world.Logger.Warning("[VSpaint] CheckDrying: could not find item vspaint:paintbrush-{0}-dry", color);
                return;
            }
            slot.Itemstack = new ItemStack(dryItem);
            slot.MarkDirty();
        }

        public override string GetHeldItemName(ItemStack stack)
        {
            string colorName = GetColorName(stack);
            if (colorName == null)
                return Lang.Get("vspaint:item-paintbrush");

            string displayName = Lang.Get($"vspaint:color-{colorName}");
            return GetState(stack) == "dry"
                ? Lang.Get("vspaint:brush-name-dry", displayName)
                : Lang.Get("vspaint:brush-name-wet", displayName);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            CheckDrying(inSlot, world);

            switch (GetState(inSlot.Itemstack))
            {
                case "wet": dsc.AppendLine(Lang.Get("vspaint:brush-hint-wet"));   break;
                case "dry": dsc.AppendLine(Lang.Get("vspaint:brush-hint-dry"));   break;
                default:    dsc.AppendLine(Lang.Get("vspaint:brush-hint-clean")); break;
            }
        }

        public void UseBrush(ItemSlot slot, IPlayer byPlayer)
        {
            if (slot?.Itemstack == null) return;
            DamageItem(byPlayer.Entity.World, byPlayer.Entity, slot);
        }
    }
}
