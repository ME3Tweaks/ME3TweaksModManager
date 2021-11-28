namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    // 00BAEF40
    public class PlotCodexPage : IUnrealSerializable
    {
        public int Page;
        public bool New;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Page);
            stream.Serialize(ref this.New);
        }
    }
}
