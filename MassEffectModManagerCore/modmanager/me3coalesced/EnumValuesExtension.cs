using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Markup;

namespace MassEffect3.CoalesceTool
{
    [Localizable(false)]
	[MarkupExtensionReturnType(typeof (IEnumerable))]
	public class EnumValuesExtension : MarkupExtension
	{
		public EnumValuesExtension() {}

		public EnumValuesExtension(Type enumType)
		{
			EnumType = enumType;
		}

		[ConstructorArgument("enumType")]
		public Type EnumType { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (EnumType == null)
			{
				throw new ArgumentException("The enum type is not set");
			}

			return Enum.GetValues(EnumType);
		}
	}
}
