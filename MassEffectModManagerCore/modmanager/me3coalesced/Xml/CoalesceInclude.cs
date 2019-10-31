namespace MassEffect3.Coalesce.Xml
{
	public class CoalesceInclude
	{
		public CoalesceInclude(string source, CoalesceIncludeTarget target = CoalesceIncludeTarget.PreSections)
		{
			Target = target;
			Source = source;
		}

		public string Source { get; set; }

		public CoalesceIncludeTarget Target { get; set; }
	}
}
