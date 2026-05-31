using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSpaint.Network
{
    public class PaintNetworkHandler
    {
        private const string ChannelId = "vspaint";

        private readonly ICoreAPI api;

        public PaintNetworkHandler(ICoreAPI api)
        {
            this.api = api;
        }

        public void RegisterChannel()
        {
            if (api.Side == EnumAppSide.Server)
            {
                ((ICoreServerAPI)api).Network
                    .RegisterChannel(ChannelId)
                    .RegisterMessageType<PaintSavePacket>()
                    .RegisterMessageType<PaintFinishPacket>()
                    .SetMessageHandler<PaintSavePacket>(OnServerReceiveSave)
                    .SetMessageHandler<PaintFinishPacket>(OnServerReceiveFinish);
            }
            else
            {
                ((ICoreClientAPI)api).Network
                    .RegisterChannel(ChannelId)
                    .RegisterMessageType<PaintSavePacket>()
                    .RegisterMessageType<PaintFinishPacket>();
            }
        }

        // Generous: GUI may stay open while the player walks around. Beyond this
        // it's almost certainly a crafted packet rather than a real interaction.
        private const double MaxInteractDistSq = 16 * 16;

        private void OnServerReceiveSave(IServerPlayer fromPlayer, PaintSavePacket packet)
        {
            var sapi = (ICoreServerAPI)api;

            if (packet?.PixelData == null || packet.PixelData.Length != PaintingUtil.EncodedSize)
            {
                sapi.Logger.Warning("[VSpaint] Save packet from {0}: bad pixel data length ({1})",
                    fromPlayer.PlayerName, packet?.PixelData?.Length ?? -1);
                return;
            }

            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            if (fromPlayer.Entity.Pos.SquareDistanceTo(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5) > MaxInteractDistSq)
            {
                sapi.Logger.Warning("[VSpaint] Save packet from {0}: too far from easel at {1}", fromPlayer.PlayerName, pos);
                return;
            }

            var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityEasel;
            if (be == null)
            {
                sapi.Logger.Warning("[VSpaint] Save packet from {0}: no BlockEntityEasel at {1}", fromPlayer.PlayerName, pos);
                return;
            }
            if (!be.HasCanvas || be.IsFinished)
            {
                sapi.Logger.Warning("[VSpaint] Save packet from {0}: easel at {1} not editable (hasCanvas={2}, finished={3})",
                    fromPlayer.PlayerName, pos, be.HasCanvas, be.IsFinished);
                return;
            }

            be.UpdatePixelData(packet.PixelData);
            sapi.Logger.Debug("[VSpaint] Saved painting from {0} at {1} ({2} bytes)", fromPlayer.PlayerName, pos, packet.PixelData.Length);
        }

        private void OnServerReceiveFinish(IServerPlayer fromPlayer, PaintFinishPacket packet)
        {
            var sapi = (ICoreServerAPI)api;
            if (packet == null) return;

            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            if (fromPlayer.Entity.Pos.SquareDistanceTo(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5) > MaxInteractDistSq)
            {
                sapi.Logger.Warning("[VSpaint] Finish packet from {0}: too far from easel at {1}", fromPlayer.PlayerName, pos);
                return;
            }

            var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityEasel;
            if (be == null || !be.HasCanvas || be.IsFinished) return;

            be.FinishPainting();
        }

        public void SendSave(BlockPos pos, byte[] pixelData)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new PaintSavePacket
            {
                PosX      = pos.X,
                PosY      = pos.Y,
                PosZ      = pos.Z,
                PixelData = pixelData
            });
        }

        public void SendFinish(BlockPos pos)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new PaintFinishPacket
            {
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z
            });
        }
    }
}
