using CSharpAuth.Db;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Shim
{
	public class PdoException : Exception
	{
		public PdoException(PostgresException innerException, PDO pdo)
			:base("See inner exception", innerException)
		{
			DatabaseName = pdo?.Connection?.Database ?? "null";
			ConnectionString = pdo?.Connection?.ConnectionString;
		}
		public string ConnectionString, DatabaseName;
	}
}
