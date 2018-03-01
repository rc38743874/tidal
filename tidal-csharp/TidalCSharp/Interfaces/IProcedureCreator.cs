using System;
using System.Collections.Generic;

namespace TidalCSharp {

	public interface IProcedureCreator {
		string GetStoredProcedureScriptText(string moduleName, List<TableDef> tableDefList, int listAllLimit);
	}
}

