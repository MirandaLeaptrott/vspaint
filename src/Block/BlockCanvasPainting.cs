using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSpaint
{
    public class BlockCanvasPainting : Block
    {
        public override bool DoPlaceBlock(
            IWorldAccessor world, IPlayer byPlayer,
            BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
                var be = world.BlockAccessor.GetBlockEntity(blockSel.Position)
                         as BlockEntityCanvasPainting;
                if (be != null)
                {
                    byte[] pixelData = byItemStack?.Attributes?.GetBytes("pixelData", null);
                    if (pixelData != null)
                        be.SetPixelData(pixelData);
                    else
                        be.ClearPixelData();
                }
            }

            return placed;
        }

        public override ItemStack[] GetDrops(
            IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvasPainting;

            // Drop the -north variant as the canonical item form so it can be
            // re-placed in any orientation; HorizontalAttachable picks the facing.
            Block northVariant = world.GetBlock(new AssetLocation("vspaint", "canvas-painting-north"));
            if (northVariant == null || northVariant.Id == 0)
                return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            var stack = new ItemStack(northVariant);

            if (be?.PixelData != null)
                stack.Attributes.SetBytes("pixelData", be.PixelData);

            return new ItemStack[] { stack };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvasPainting;

            Block northVariant = world.GetBlock(new AssetLocation("vspaint", "canvas-painting-north"));
            if (northVariant == null || northVariant.Id == 0)
                return base.OnPickBlock(world, pos);

            var stack = new ItemStack(northVariant);
            if (be?.PixelData != null)
                stack.Attributes.SetBytes("pixelData", be.PixelData);

            return stack;
        }
    }
}
