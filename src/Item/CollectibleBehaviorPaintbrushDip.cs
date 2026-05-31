using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSpaint
{
    public class CollectibleBehaviorPaintbrushDip : CollectibleBehavior
    {
        // VS uses "gray" and "woad"; our brush variants don't, so we translate.
        // Anything not listed here is treated as not-a-dye.
        private static readonly Dictionary<string, string> DyeToVariantMap = new Dictionary<string, string>
        {
            { "white",  "white"    },
            { "black",  "black"    },
            { "gray",   "darkgrey" },
            { "red",    "red"      },
            { "orange", "orange"   },
            { "yellow", "yellow"   },
            { "green",  "green"    },
            { "blue",   "blue"     },
            { "purple", "purple"   },
            { "pink",   "pink"     },
            { "woad",   "darkblue" },
        };

        public CollectibleBehaviorPaintbrushDip(CollectibleObject collObj) : base(collObj) { }

        public override void OnHeldInteractStart(
            ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handHandling,
            ref EnumHandling handling)
        {
            if (!firstEvent || blockSel == null) return;

            var world = byEntity.World;
            var block = world.BlockAccessor.GetBlock(blockSel.Position);
            var be    = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            // Two container shapes need different take paths: barrel/bucket take by
            // block position, bowls (placed as ground storage) take by inner itemstack.
            ItemStack content        = null;
            BlockLiquidContainerBase containerBlock = null;
            BlockLiquidContainerBase bowlBlock      = null;
            ItemSlot                 bowlSlot       = null;

            var liquidBE = be as BlockEntityLiquidContainer;
            if (liquidBE != null)
            {
                content        = liquidBE.GetContent();
                containerBlock = block as BlockLiquidContainerBase;
            }
            else
            {
                var groundStorage = be as IBlockEntityContainer;
                if (groundStorage != null)
                {
                    foreach (ItemSlot sl in groundStorage.Inventory)
                    {
                        if (sl?.Itemstack?.Collectible is BlockLiquidContainerBase lcb)
                        {
                            content   = lcb.GetContent(sl.Itemstack);
                            bowlBlock = lcb;
                            bowlSlot  = sl;
                            break;
                        }
                    }
                }
            }

            string brushVariantName = null;
            bool   isWaterContainer = false;

            if (content?.Item != null)
            {
                string firstPart = content.Item.FirstCodePart();
                if (firstPart == "dye")
                {
                    if (!content.Item.Variant.TryGetValue("color", out string dyeColor) || dyeColor == null)
                        dyeColor = content.Item.LastCodePart();
                    if (dyeColor != null)
                        DyeToVariantMap.TryGetValue(dyeColor, out brushVariantName);
                }
                else if (firstPart == "waterportion")
                {
                    isWaterContainer = true;
                }
            }

            // Also accept clicking directly on a world water block, not just containers.
            bool isWater = isWaterContainer || IsWaterAt(block, world, blockSel);

            if (!isWater && brushVariantName == null) return;

            handHandling = EnumHandHandling.PreventDefault;
            handling     = EnumHandling.PreventDefault;

            ItemPaintbrush.CheckDrying(slot, world);
            string state = ItemPaintbrush.GetState(slot.Itemstack);

            if (isWater)
            {
                if (state == "dry")
                {
                    if (world.Side == EnumAppSide.Client)
                        ((ICoreClientAPI)world.Api).TriggerIngameError(this, "dry",
                            Lang.Get("vspaint:brush-hint-needbarrel"));
                }
                else if (!ItemPaintbrush.IsClean(slot.Itemstack))
                {
                    ItemPaintbrush.Wash(slot, world);

                    if (world.Side == EnumAppSide.Server && isWaterContainer)
                        TakeFromContainer(blockSel.Position, be, content, containerBlock, bowlBlock, bowlSlot, 0.02);

                    if (world.Side == EnumAppSide.Client)
                        ((ICoreClientAPI)world.Api).TriggerIngameError(this, "washed",
                            Lang.Get("vspaint:brush-washed"));
                }
            }
            else
            {
                if (state == "dry")
                {
                    if (world.Side == EnumAppSide.Client)
                        ((ICoreClientAPI)world.Api).TriggerIngameError(this, "dry",
                            Lang.Get("vspaint:brush-hint-needbarrel"));
                }
                else if (!ItemPaintbrush.IsClean(slot.Itemstack))
                {
                    string currentColor = ItemPaintbrush.GetColorName(slot.Itemstack);
                    string mixResult    = ItemPaintbrush.TryMix(currentColor, brushVariantName);

                    if (mixResult == null)
                    {
                        if (world.Side == EnumAppSide.Client)
                            ((ICoreClientAPI)world.Api).TriggerIngameError(this, "cantmix",
                                Lang.Get("vspaint:brush-cant-mix"));
                    }
                    else
                    {
                        ItemPaintbrush.Dip(slot, mixResult, world);

                        if (world.Side == EnumAppSide.Server)
                            TakeFromContainer(blockSel.Position, be, content, containerBlock, bowlBlock, bowlSlot, 0.1);

                        if (world.Side == EnumAppSide.Client)
                        {
                            string displayName = Lang.Get($"vspaint:color-{mixResult}");
                            ((ICoreClientAPI)world.Api).TriggerIngameError(this, "mixed",
                                Lang.Get("vspaint:brush-mixed", displayName));
                        }
                    }
                }
                else
                {
                    ItemPaintbrush.Dip(slot, brushVariantName, world);

                    if (world.Side == EnumAppSide.Server)
                        TakeFromContainer(blockSel.Position, be, content, containerBlock, bowlBlock, bowlSlot, 0.1);

                    if (world.Side == EnumAppSide.Client)
                    {
                        string displayName = Lang.Get($"vspaint:color-{brushVariantName}");
                        ((ICoreClientAPI)world.Api).TriggerIngameError(this, "dipped",
                            Lang.Get("vspaint:brush-dipped", displayName));
                    }
                }
            }
        }

        // Takes ~litres litres via whichever container shape was detected: pos-based
        // for barrels/buckets, itemstack-based for ground-stored bowls.
        private static void TakeFromContainer(
            BlockPos pos, BlockEntity be, ItemStack content,
            BlockLiquidContainerBase containerBlock,
            BlockLiquidContainerBase bowlBlock, ItemSlot bowlSlot,
            double litres)
        {
            var props = BlockLiquidContainerBase.GetContainableProps(content);
            float ipl    = props?.ItemsPerLitre ?? 1f;
            int toTake   = Math.Max(1, (int)Math.Round(litres * ipl));

            if (containerBlock != null)
            {
                containerBlock.TryTakeContent(pos, toTake);
            }
            else if (bowlBlock != null && bowlSlot != null)
            {
                bowlBlock.TryTakeContent(bowlSlot.Itemstack, toTake);
                bowlSlot.MarkDirty();
                be?.MarkDirty(true);
            }
        }

        private static bool IsWaterAt(Block block, IWorldAccessor world, BlockSelection blockSel)
        {
            if (IsBlockWater(block)) return true;
            var above = world.BlockAccessor.GetBlock(blockSel.Position.UpCopy());
            return IsBlockWater(above);
        }

        private static bool IsBlockWater(Block block)
        {
            if (block == null) return false;
            string liq = block.LiquidCode ?? "";
            if (liq == "water") return true;
            return block.Code?.Path?.Contains("water") == true;
        }
    }
}
