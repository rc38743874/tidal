using System;
using System.Data.Common;

namespace TidalCSharp {

	public interface IScriptExecutor {
		void ExecuteTidalProcedureScript(string scriptText);
	}
}

