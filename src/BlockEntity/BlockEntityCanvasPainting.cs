using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSpaint
{
    public class BlockEntityCanvasPainting : BlockEntity
    {
        public byte[] PixelData { get; private set; }

        // Mesh is built on the main thread, read on the tessellation thread.
        private readonly object meshLock = new object();
        private MeshData clientMesh;
        private bool     needsRebuild;
        private bool     rebuildQueued;

        // Atlas slot we currently own; captured from GetOrInsertTexture so we can
        // free it when the painting changes or the BE goes away.
        private int currentAtlasSubId;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
                RequestMeshRebuild();
        }

        public void SetPixelData(byte[] data)
        {
            PixelData = data;
            MarkDirty(true);
            if (Api?.Side == EnumAppSide.Client)
                RequestMeshRebuild();
        }

        public void ClearPixelData()
        {
            PixelData = null;
            lock (meshLock) { clientMesh = null; }
            FreeAtlasSlot();
            MarkDirty(true);
        }

        // Caller must be on the main client thread; atlas mutation is not threadsafe.
        private void FreeAtlasSlot()
        {
            if (currentAtlasSubId == 0) return;
            if (Api is ICoreClientAPI capi)
                capi.BlockTextureAtlas.FreeTextureSpace(currentAtlasSubId);
            currentAtlasSubId = 0;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            FreeAtlasSlot();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            FreeAtlasSlot();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Api?.Side == EnumAppSide.Client && needsRebuild)
            {
                needsRebuild = false;
                RequestMeshRebuild();
            }

            MeshData mesh;
            lock (meshLock) { mesh = clientMesh; }

            if (mesh != null)
            {
                mesher.AddMeshData(mesh.Clone());
                return true;
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        private void RequestMeshRebuild()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            var capi = (ICoreClientAPI)Api;

            lock (meshLock)
            {
                if (rebuildQueued) return;
                rebuildQueued = true;
            }

            capi.Event.EnqueueMainThreadTask(() =>
            {
                lock (meshLock) rebuildQueued = false;
                BuildClientMesh(capi);
            }, "vspaint-canvas-rebuild");
        }

        private void BuildClientMesh(ICoreClientAPI capi)
        {
            if (PixelData == null)
            {
                lock (meshLock) { clientMesh = null; }
                MarkDirty(true);
                return;
            }

            try
            {
                byte[] pngBytes = PaintingUtil.PixelsToPng(PixelData);
                if (pngBytes == null)
                {
                    lock (meshLock) { clientMesh = null; }
                    return;
                }

                // Content hash in the key forces a fresh atlas slot per unique
                // painting; without it, GetOrInsertTexture's cache would echo stale
                // textures when a new painting is hung at the same position.
                int hash = 17;
                foreach (byte b in PixelData) hash = hash * 31 + b;
                string key = $"vspaint-painting-{Pos.X}-{Pos.Y}-{Pos.Z}-{hash}";
                var texLoc = new AssetLocation("vspaint", key);

                capi.BlockTextureAtlas.GetOrInsertTexture(
                    texLoc,
                    out int newSubId,
                    out TextureAtlasPosition texPos,
                    () => capi.Render.BitmapCreateFromPng(pngBytes),
                    0.005f
                );

                if (texPos == null)
                {
                    lock (meshLock) { clientMesh = null; }
                    return;
                }

                // Same subId means the atlas served from cache and there's nothing
                // to free; otherwise drop the previous slot now that we have a new one.
                if (currentAtlasSubId != 0 && currentAtlasSubId != newSubId)
                    capi.BlockTextureAtlas.FreeTextureSpace(currentAtlasSubId);
                currentAtlasSubId = newSubId;

                ITexPositionSource defaultSrc = capi.Tesselator.GetTextureSource(Block);
                var paintingSrc = new PaintingTexSource(defaultSrc, texPos, capi.BlockTextureAtlas.Size);

                capi.Tesselator.TesselateShape(
                    "vspaint-canvas",
                    Block.Code,
                    Block.Shape,
                    out MeshData mesh,
                    paintingSrc
                );

                lock (meshLock)
                {
                    clientMesh = mesh;
                }

                MarkDirty(true);
            }
            catch (Exception ex)
            {
                capi.Logger.Error("[VSpaint] BuildClientMesh failed at {0}: {1}", Pos, ex);
                lock (meshLock) { clientMesh = null; }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (PixelData != null)
                tree.SetBytes("pixelData", PixelData);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            PixelData = tree.GetBytes("pixelData", null);

            if (Api?.Side == EnumAppSide.Client)
            {
                needsRebuild = true;
                RequestMeshRebuild();
            }
        }

        // Maps the "painting" texture key to the generated atlas slot; everything
        // else falls through to the block's normal texture source.
        private sealed class PaintingTexSource : ITexPositionSource
        {
            private readonly ITexPositionSource defaultSrc;
            private readonly TextureAtlasPosition paintingPos;
            private readonly Size2i atlasSize;

            public PaintingTexSource(
                ITexPositionSource defaultSrc,
                TextureAtlasPosition paintingPos,
                Size2i atlasSize)
            {
                this.defaultSrc  = defaultSrc;
                this.paintingPos = paintingPos;
                this.atlasSize   = atlasSize;
            }

            public TextureAtlasPosition this[string textureCode] =>
                textureCode == "painting" ? paintingPos : defaultSrc[textureCode];

            public Size2i AtlasSize => atlasSize;
        }
    }
}
