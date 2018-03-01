using System;
using System.Collections.Generic;

namespace TidalCSharp {

	public interface ITableScriptWriter {
        string GetTableDropScriptText(string databaseName, List<TableDef> tableDefList);
        string GetTableCreateScriptText(string databaseName, List<TableDef> tableDefList);
	}
}

