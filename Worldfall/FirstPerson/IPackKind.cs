using System.Collections.Generic;

namespace FirstPerson
{
	internal interface IPackKind
	{
		string Kind { get; }

		void Apply(List<PackInfo> topFirst);
	}
}
