using System.ComponentModel;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects.mod;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class ReadOnlyOption : AlternateOption, INotifyPropertyChanged
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

        //Fody uses this property on weaving
#pragma warning disable 0169
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0169
    }
}