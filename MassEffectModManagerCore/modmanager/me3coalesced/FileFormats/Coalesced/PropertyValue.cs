namespace MassEffect3.FileFormats.Coalesced
{
	public struct PropertyValue
	{
		public PropertyValue(int type, string value)
			: this()
		{
			Type = type;
			Value = value;
		}

		public int Type { get; set; }

		public string Value { get; set; }
	}
}