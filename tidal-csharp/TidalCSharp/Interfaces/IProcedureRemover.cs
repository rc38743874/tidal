using System;
using System.Data.Common;


namespace TidalCSharp {

	public interface IProcedureRemover {
		
		void RemoveTidalStoredProcs(
			string databaseName, string moduleName);
	}
}

