using System.Collections.Generic;
using System.Dynamic;

namespace Gammtek.Conduit.Dynamic.Collections
{
	internal sealed class DynamicKeyValuePair<TKey, TValue> : DynamicObject
	{
		private KeyValuePair<TKey, TValue> _kvp;

		internal DynamicKeyValuePair(KeyValuePair<TKey, TValue> item)
		{
			_kvp = item;
		}

		public TKey Key
		{
			get { return _kvp.Key; }
		}

		public TValue Value
		{
			get { return _kvp.Value; }
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			var key = binder.Name;

			if (_kvp.Key.ToString() != key)
			{
				return base.TryGetMember(binder, out result);
			}

			result = _kvp.Value;

			return true;
		}
	}
}
