using ProtoBuf;

namespace VSpaint.Network
{
    // Client to server: save pixel data for the easel at the given position.
    [ProtoContract]
    public class PaintSavePacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public byte[] PixelData { get; set; }
    }

    // Client to server: mark the easel's painting as finished.
    [ProtoContract]
    public class PaintFinishPacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
    }
}
