using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.objects.mod.editor
{
    /// <summary>
    /// Serialization interface for moddesc.ini
    /// </summary>
    interface IMDParameterMap
    {
        /// <summary>
        /// Maps the object parameter map
        /// </summary>
        public void BuildParameterMap(Mod mod);
        /// <summary>
        /// The list of parameters
        /// </summary>
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; }
    }
}
