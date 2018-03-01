using System;
using System.Data;
using System.Data.Common;

namespace TidalCSharp {
	public interface IProcessor	{
		DbConnection GetConnection(string connectionString, string password);

        ITableExtractor GetTableExtractor();
		
		ITableScriptWriter GetTableScriptWriter();
		
        IClassCreator GetClassCreator();

        IProcedureCreator GetProcedureCreator();

        IProcedureReader GetProcedureReader();

        IProcedureRemover GetProcedureRemover();

        IScriptExecutor GetScriptExecutor();


	}


}

