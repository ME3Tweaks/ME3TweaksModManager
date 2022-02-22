using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects
{
    [AddINotifyPropertyChangedInterface]
    public class ReadOnlyOption : AlternateOption
    {
        public override string Description { get; internal set; } = M3L.GetString(M3L.string_descriptionSetConfigFilesReadOnly);
        public override string FriendlyName { get; internal set; } = M3L.GetString(M3L.string_makeConfigFilesReadonly);
        public override bool CheckedByDefault => false;
        public override bool IsManual => true;
        public override bool IsAlways => false;
        public override void BuildParameterMap(Mod mod)
        {
            // This class does not use parameters
        }

        public override double CheckboxOpacity => 1;
        public override bool UIRequired => false;
        public override bool UINotApplicable => false;

        public override bool UIIsSelectable { get => true; set { } }
    }
}