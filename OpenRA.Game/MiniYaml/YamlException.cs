using System;

namespace OpenRA
{
	[Serializable]
	public class YamlException : Exception
	{
		public YamlException(string s)
			: base(s) { }
	}
}
