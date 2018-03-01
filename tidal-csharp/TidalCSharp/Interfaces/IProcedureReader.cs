using System;
using System.Collections.Generic;

namespace TidalCSharp {

	public interface IProcedureReader {
		
		List<ProcedureDef> MakeProcedureDefList(string databaseName, 
		                                        string moduleName, 
		                                        Dictionary<string, TableDef> tableDefMap);
	}
}

