using System;
using System.Collections.Generic;

namespace TidalCSharp {

	public interface IClassCreator {
		string GetDataAccessClassText(string projectNamespace, string modelNamespace, List<ModelDef> modelDefList);
	}
}

