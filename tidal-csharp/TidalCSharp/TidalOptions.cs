using System;
using Mono.Options;
using System.Collections.Generic;

namespace TidalCSharp {
	public class TidalOptions {

		private OptionSet optionSet;

		public DatabaseTypeEnum DatabaseType { get; set; }
		public string DataAccessFileNameOut { get; set; } 
		public string TableDefFileNameOut { get; set; }
		public string TableDefFileNameIn { get; set; }
		public string TableCreateScriptFileName { get; set; }
        public string TableDropScriptFileName { get; set; }
		public string SQLScriptFileNameOut { get; set; }
		public string SQLScriptFileNameIn { get; set; }
		public string ModelsPathOut { get; set; } 
		public bool RemoveProcedures { get; set; }
		public bool CreateProcedures { get; set; }
		public string StoredProcDefFileNameOut { get; set; }
		public string StoredProcDefFileNameIn { get; set; }
		public bool PasswordPrompt { get; set; }
		public string Password { get; set; }
		public string ModelDefFileNameIn { get; set; }
		public string ModelDefFileNameOut { get; set; }
		public string ConnectionString { get; set; }
		public string ModelsNamespace { get; set; }
		public string ModelsAssemblyFileName { get; set; }
		public string ModuleName { get; set; }
		public string ProjectNamespace { get; set; }
		public bool Verbose { get; set; }
		public bool ShouldShowHelp { get; set; }

		public TidalOptions() {
			this.optionSet = new OptionSet { 
				{ "o|out=", "path/filename of the generated code DataAccess class.", o => this.DataAccessFileNameOut = o}, 
				{ "t|tableout=", "path/filename to save table schema definitions.", t => this.TableDefFileNameOut = t }, 
				{ "T|tablein=", "path/filename to read table schema definitions.", T => this.TableDefFileNameIn = T }, 
				{ "b|tablecreatescript=", "path/filename to write out .sql table creation script ", b => this.TableCreateScriptFileName = b }, 
                { "B|tabledropscript=", "path/filename to write out .sql table drop script ", B => this.TableDropScriptFileName = B }, 
				{ "q|sqlout=", "path/filename to save optional copy of stored procedures.", q => this.SQLScriptFileNameOut = q }, 
				{ "Q|sqlin=", "path/filename from which to read stored procedure creation .sql script", Q => this.SQLScriptFileNameIn = Q},
				{ "m|makemodels=", "generate and save fresh models into this path.", m => this.ModelsPathOut = m}, 
				{ "r|removeproc", "remove existing Tidal stored procedures for this module in the database", r => this.RemoveProcedures = (r != null)},
				{ "c|createproc", "automatically execute stored procedure script", c => this.CreateProcedures = (c != null)},
				{ "s|storedprocout=", "path/filename to save stored procedure descriptions .json file", s => this.StoredProcDefFileNameOut = s},
				{ "S|storedprocin=", "path/filename from which to read stored procedure descriptions", S => this.StoredProcDefFileNameIn = S},
				{ "p|prompt", "enter password via prompt", p => this.PasswordPrompt = (p!=null) },
				{ "P|password=", "password to connect to database", P => this.Password = P },
				{ "d|modelout=", "path/filename to save model descriptions .json file", d => this.ModelDefFileNameIn = d},
				{ "D|modelin=", "path/filename from which to read model descriptions", D => this.ModelDefFileNameOut = D},
				{ "C|conn=", "connection string to database", C => this.ConnectionString = C},
				{ "N|modelsns=", "models namespace for generated and/or imported models", N => this.ModelsNamespace = N},
				{ "a|modelsdll=", "path/filename of external assembly containing models", a => this.ModelsAssemblyFileName = a},
				{ "u|modulename=", "module name for tagging stored procedures", u => this.ModuleName = u},
				{ "n|namespace=", "project namespace for output DataAccess class", n => this.ProjectNamespace = n},
				{ "v|verbose", "verbose messages", v => this.Verbose = (v != null) },
				{ "h|help", "show this message and exit", h => this.ShouldShowHelp = (h != null) },
			};
		}

		/* returns true if successfully processed options */
		public bool BuildFromArguments(string[] args) {
			List<string> extraCommandList;
			try {
				extraCommandList = this.optionSet.Parse (args);

			} catch (OptionException e) {
				Console.WriteLine (e.Message);
				Console.WriteLine ("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}

			if (extraCommandList.Count == 0) {
				Console.WriteLine ("Database type argument required but not found.");
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}
			else if (extraCommandList.Count == 1) {
				string databaseTypeInput = extraCommandList[0].ToLowerInvariant();
				DatabaseTypeEnum type;
				if (Enum.TryParse<DatabaseTypeEnum>(databaseTypeInput, out type) == true) {
					this.DatabaseType = type;
				}
				else {
					Console.WriteLine($"Database type {extraCommandList[0]} not recognized.");
					Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
					return false;
				}
			}
			else {
				Console.WriteLine("Unrecognized information: ");
				foreach (string extraLine in extraCommandList) {
					Console.WriteLine("\t" + extraLine);
				}
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}

			if (this.ShouldShowHelp == true) {
				Console.WriteLine ("Usage: tidal-csharp.exe database-type [OPTIONS]");
				Console.WriteLine ("Create a DataAccess.cs class in C#, and/or intermediate definition files.");
				Console.WriteLine ();


				Console.WriteLine ("database-type:");
				Console.WriteLine("\tmssql: Microsoft SQL Server");
				Console.WriteLine("\tmysql: MySQL");

				// output the options
				Console.WriteLine ("Options:");
				this.optionSet.WriteOptionDescriptions (Console.Out);

				Console.WriteLine("Examples:");
				Console.WriteLine("tidal-csharp.exe mssql [OPTIONS]");
				return false;
			}

			bool wasOkay = true;
			if (this.StoredProcDefFileNameOut != null && this.StoredProcDefFileNameIn != null) {
				wasOkay = false;
				Console.WriteLine ("Cannot use a stored procedure definition input file and a stored procedure definition output file simultaneously.  Please use either -s or -S exclusively.");
			}

			if (this.SQLScriptFileNameOut != null && this.SQLScriptFileNameIn != null) {
				wasOkay = false;
				Console.WriteLine ("Cannot use a .sql script input file and a .sql script output file simultaneously.  Please use either -q or -Q exclusively.");
			}


			if (this.TableDefFileNameOut != null && this.TableDefFileNameIn != null) {
				wasOkay = false;
				Console.WriteLine ("Cannot use a table schema definition input file and a table schema definition output file simultaneously.  Please use either -t or -T exclusively.");
			}

			if (this.ModelDefFileNameIn != null && this.ModelDefFileNameOut != null) {
				wasOkay = false;
				Console.WriteLine ("Cannot use a model descriptions input file and a model descriptions output file simultaneously.  Please use either -d or -D exclusively.");
			}


			if (this.ModelsPathOut != null) {
				/* TODO: validate it is a valid path */
			}

			if (this.RemoveProcedures == true && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine ("Option -r or --removeproc to remove previous Tidal stored procedures automatically requires a connection string (-C or --conn).");
			}

			if (this.CreateProcedures == true && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine ("Option -c or --createproc to create Tidal stored procedures automatically requires a connection string (-C or --conn).");
			}

			if (this.PasswordPrompt == true && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine ("Option -p or --prompt to prompt for a password requires a connection string (-C or --conn).");
			}

			if (this.Password != null && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine ("Option -P or --password to supply a password requires a connection string (-C or --conn).");
			}

			if (this.ModelsNamespace ==  null) {
				/* TODO: not all commands may need this? */
				wasOkay = false;
				Console.WriteLine ("Option -N or --modelsns to supply a models namespace is required.");
			}

			if (this.ModelsAssemblyFileName  ==  null) {
				/* TODO: not all commands may need this? */
				wasOkay = false;
				Console.WriteLine ("Option -a or --modelsdll to specify an assembly file containing models is required.");
			}

			if (this.ModuleName  ==  null) {
				/* TODO: not all commands may need this? */
				wasOkay = false;
				Console.WriteLine ("Option -u or --modulename to specify the module name to use when referring to stored procedures is required.");
			}

			if (this.ProjectNamespace  ==  null && this.DataAccessFileNameOut!=null) {
				/* TODO: not all commands may need this? */
				wasOkay = false;
				Console.WriteLine ("Option -n or --namespace to specify the namespace for the output DataAccess class is required.");
			}
			
			if (this.DataAccessFileNameOut == null) {
				wasOkay = false;
				Console.WriteLine ("Option -o or --out to specify the output file for DataAccess class is required.");
			}
			
			return wasOkay;

		}
	}
}

