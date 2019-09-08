using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;

namespace Gammtek.Conduit.Dynamic
{
	public class ElasticObject : DynamicObject, IElasticHierarchyWrapper, INotifyPropertyChanged
	{
		private readonly IElasticHierarchyWrapper _elasticProvider = new SimpleHierarchyWrapper();
		private NodeType _nodeType = NodeType.Element;

		public ElasticObject(string name = null)
		{
			InternalName = name ?? string.Format("id{0}", Guid.NewGuid());
		}

		internal ElasticObject(string name, object value)
			: this(name)
		{
			InternalValue = value;
		}

		public string InternalFullName
		{
			get
			{
				var path = InternalName;
				var parent = InternalParent;

				while (parent != null)
				{
					path = parent.InternalName + "_" + path;
					parent = parent.InternalParent;
				}

				return path;
			}
		}

		public IEnumerable<KeyValuePair<string, ElasticObject>> Attributes
		{
			get { return _elasticProvider.Attributes; }
		}

		public bool HasAttribute(string name)
		{
			return _elasticProvider.HasAttribute(name);
		}

		public IEnumerable<ElasticObject> Elements
		{
			get { return _elasticProvider.Elements; }
		}

		public void SetAttributeValue(string name, object obj)
		{
			_elasticProvider.SetAttributeValue(name, obj);
		}

		public object GetAttributeValue(string name)
		{
			return _elasticProvider.GetAttributeValue(name);
		}

		public ElasticObject Attribute(string name)
		{
			return _elasticProvider.Attribute(name);
		}

		public ElasticObject Element(string name)
		{
			return _elasticProvider.Element(name);
		}

		public void AddAttribute(string key, ElasticObject value)
		{
			value._nodeType = NodeType.Attribute;
			value.InternalParent = this;
			_elasticProvider.AddAttribute(key, value);
		}

		public void RemoveAttribute(string key)
		{
			_elasticProvider.RemoveAttribute(key);
		}

		public void AddElement(ElasticObject element)
		{
			element._nodeType = NodeType.Element;
			element.InternalParent = this;
			_elasticProvider.AddElement(element);
		}

		public void RemoveElement(ElasticObject element)
		{
			_elasticProvider.RemoveElement(element);
		}

		public object InternalValue
		{
			get { return _elasticProvider.InternalValue; }
			set { _elasticProvider.InternalValue = value; }
		}

		public object InternalContent
		{
			get { return _elasticProvider.InternalContent; }
			set { _elasticProvider.InternalContent = value; }
		}

		public string InternalName
		{
			get { return _elasticProvider.InternalName; }
			set { _elasticProvider.InternalName = value; }
		}

		public ElasticObject InternalParent
		{
			get { return _elasticProvider.InternalParent; }
			set { _elasticProvider.InternalParent = value; }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void AddAttribute(string memberName, object value)
		{
			if (value is ElasticObject)
			{
				var eobj = value as ElasticObject;

				if (!Elements.Contains(eobj))
				{
					AddElement(eobj);
				}
			}
			else
			{
				if (!_elasticProvider.HasAttribute(memberName))
				{
					_elasticProvider.AddAttribute(memberName, new ElasticObject(memberName, value));
				}
				else
				{
					_elasticProvider.SetAttributeValue(memberName, value);
				}
			}

			OnPropertyChanged(memberName);
		}

		internal ElasticObject CreateOrGetAttribute(string memberName, object value)
		{
			if (!HasAttribute(memberName))
			{
				AddAttribute(memberName, new ElasticObject(memberName, value));
			}

			return Attribute(memberName);
		}

		private void OnPropertyChanged(string prop)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(prop));
			}
		}

		public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result)
		{
			if (binder.Operation == ExpressionType.LeftShiftAssign && _nodeType == NodeType.Element)
			{
				InternalContent = arg;
				result = this;
				return true;
			}

			if (binder.Operation == ExpressionType.LeftShiftAssign && _nodeType == NodeType.Attribute)
			{
				InternalValue = arg;
				result = this;
				return true;
			}

			switch (binder.Operation)
			{
				case ExpressionType.LeftShift:
				{
					if (arg is string)
					{
						var exp = new ElasticObject(arg as string, null)
						{
							_nodeType = NodeType.Element
						};

						AddElement(exp);
						result = exp;
						return true;
					}

					if (!(arg is ElasticObject))
					{
						return base.TryBinaryOperation(binder, arg, out result);
					}

					var eobj = arg as ElasticObject;

					if (!Elements.Contains(eobj))
					{
						AddElement(eobj);
					}

					result = eobj;

					return true;
				}
				case ExpressionType.LessThan:
				{
					var memberName = "";

					if (arg is string)
					{
						memberName = arg as string;

						if (HasAttribute(memberName))
						{
							throw new InvalidOperationException("An attribute with name" + memberName + " already exists");
						}

						var att = new ElasticObject(memberName, null);
						AddAttribute(memberName, att);
						result = att;

						return true;
					}

					if (!(arg is ElasticObject))
					{
						return base.TryBinaryOperation(binder, arg, out result);
					}

					var eobj = arg as ElasticObject;

					AddAttribute(memberName, eobj);
					result = eobj;

					return true;
				}
				case ExpressionType.GreaterThan:
				{
					if (!(arg is FormatType))
					{
						return base.TryBinaryOperation(binder, arg, out result);
					}

					result = this.ToXElement();

					return true;
				}
			}

			return base.TryBinaryOperation(binder, arg, out result);
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			if ((indexes.Length == 1) && indexes[0] == null)
			{
				result = _elasticProvider.Elements.ToList();
			}
			else if ((indexes.Length == 1) && indexes[0] is int)
			{
				var indx = (int) indexes[0];
				var elmt = Elements.ElementAt(indx);
				result = elmt;
			}
			else if ((indexes.Length == 1) && indexes[0] is Func<dynamic, bool>)
			{
				var filter = indexes[0] as Func<dynamic, bool>;
				result = Elements.Where
					(c => filter(c)).ToList();
			}
			else
			{
				result = Elements.Where
					(c => indexes.Cast<string>().Contains(c.InternalName)).ToList();
			}

			return true;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (_elasticProvider.HasAttribute(binder.Name))
			{
				result = _elasticProvider.Attribute(binder.Name).InternalValue;
			}
			else
			{
				var obj = _elasticProvider.Element(binder.Name);

				if (obj != null)
				{
					result = obj;
				}
				else
				{
					var exp = new ElasticObject(binder.Name, null);
					_elasticProvider.AddElement(exp);

					result = exp;
				}
			}

			return true;
		}

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			var obj = new ElasticObject(binder.Name, null);

			foreach (var a in args)
			{
				foreach (var p in a.GetType().GetProperties())
				{
					AddAttribute(p.Name, p.GetValue(a, null));
				}
			}

			AddElement(obj);
			result = obj;

			return true;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			var memberName = binder.Name;

			AddAttribute(memberName, value);

			return true;
		}

		public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result)
		{
			if (binder.Operation != ExpressionType.OnesComplement)
			{
				return base.TryUnaryOperation(binder, out result);
			}

			result = (_nodeType == NodeType.Element) ? InternalContent : InternalValue;

			return true;
		}
	}
}
