using System;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    // 00BAB140
    public class Door : IUnrealSerializable
    {
        public Guid DoorGUID;
        public byte CurrentState;
        public byte OldState;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.DoorGUID);
            stream.Serialize(ref this.CurrentState);
            stream.Serialize(ref this.OldState);
        }

        public override string ToString()
        {
            return String.Format("{0} = {1}, {2}",
                this.DoorGUID,
                this.CurrentState,
                this.OldState);
        }
    }
}
