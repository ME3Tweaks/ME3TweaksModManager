using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManagerCore.modmanager.save
{
    public interface IPlayerRecord
    {
        public bool Proxy_IsFemale { get; set; }
        public string Proxy_FirstName { get; set; }

        public void SetMorphHead(IMorphHead morphHead);
    }

    public interface IMorphHead
    {
    }
}
