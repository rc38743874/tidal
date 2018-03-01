using System;
using System.Collections.Generic;

namespace TidalCSharp {

	public class MySQLTableScriptWriter : ITableScriptWriter {
		

        public string GetTableDropScriptText(string databaseName, List<TableDef> tableDefList) {
            throw new NotImplementedException();
        }

        public string GetTableCreateScriptText(string databaseName, List<TableDef> tableDefList) {
            throw new NotImplementedException();
		}
	}
}

