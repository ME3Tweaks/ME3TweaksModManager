namespace ME3TweaksModManager.modmanager.save.game2.FileFormats
{
    public interface IUnrealSerializable
    {
        void Serialize(IUnrealStream stream);
    }
}
