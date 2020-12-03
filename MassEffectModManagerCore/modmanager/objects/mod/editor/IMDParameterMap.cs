using System;
using System.Collections.Generic;
using System.Text;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.objects.mod.editor
{
    interface IMDParameterMap
    {
        /// <summary>
        /// Maps the object parameter map
        /// </summary>
        public void BuildParameterMap();
        /// <summary>
        /// The list of parameters
        /// </summary>
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; }
    }
}
