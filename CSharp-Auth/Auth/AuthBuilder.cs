using CSharpAuth.Db;
using CSharpAuth.Shim;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static CSharpAuth.Auth.AuthBuilder;

namespace CSharpAuth.Auth
{
	public class AuthBuilder 
		: AuthBuilderSteps.Step1_Database, 
		  AuthBuilderSteps.Step2_CookieMgr,
		  AuthBuilderSteps.Step3_SessionMgr,
		  AuthBuilderSteps.Step4_ServerMgr,
		  AuthBuilderSteps.Step5_ClientIP,
		  AuthBuilderSteps.Final
	{
		public delegate _COOKIE DgtCookieFactory(PhpInstance Owner);
		public delegate _SESSION DgtSessionFactory(PhpInstance Owner);
		public delegate _SERVER DgtServerFactory(PhpInstance Owner, string ClientIpAddress);
		private DgtCookieFactory CookieFactory;
		private DgtSessionFactory SessionFactory;
		private DgtServerFactory ServerFactory;

		private AuthBuilder()
		{			
		}

		public static AuthBuilderSteps.Step1_Database Create() => new AuthBuilder();

		private PdoDatabase PdoDatabase = null;
		private PDO PdoInstance = null;
		private PdoDsn PdoDsn = null;
		private string DbUsername = null, DbPassword = null, DbHost = null, DbDsn = null, DbDatabase = null, DbTablePrefix = null;
		private int? DbPort = null;

		// Step 1: database
		public AuthBuilderSteps.Step2_CookieMgr SetDatabase(PdoDatabase pdoDatabase)
		{
			PdoDatabase = pdoDatabase;
			return this;
		}
		public AuthBuilderSteps.Step2_CookieMgr SetDatabase(PDO pdoInstance)
		{
			PdoInstance = pdoInstance;
			return this;
		}
		public AuthBuilderSteps.Step2_CookieMgr SetDatabaseParams(PdoDsn dsn)
		{
			PdoDsn = dsn;
			return this;
		}
		public AuthBuilderSteps.Step2_CookieMgr SetDatabaseParams(string Username, string Password, string DatabaseName, string Host="127.0.0.1", int Port=5432)
		{
			DbUsername = Username;
			DbPassword = Password;
			DbDatabase = DatabaseName;
			DbHost = Host;
			DbPort = Port;
			return this;
		}		
		public AuthBuilderSteps.Step1_Database SetDatabaseTablePrefix(string TablePrefix)
		{
			DbTablePrefix = TablePrefix;
			return this;
		}
		public AuthBuilderSteps.Step2_CookieMgr SetDatabaseParamsWithDsn(string Dsn, string Username, string Password)
		{
			DbDsn = Dsn;
			DbUsername = Username;
			DbPassword = Password;
			return this;
		}

		// Step 2: cookie mgr
		public AuthBuilderSteps.Step3_SessionMgr UseEmulatedCookieManager()
		{
			CookieFactory = (Owner) => new EmulatedCookieMgr();
			return this;
		}
		public AuthBuilderSteps.Step3_SessionMgr SetCookieManager(_COOKIE CookieMgr)
		{
			CookieFactory = (Owner) => CookieMgr;
			return this;
		}
		public AuthBuilderSteps.Step3_SessionMgr SetCookieManager(DgtCookieFactory _CookieFactory)
		{
			CookieFactory = _CookieFactory;
			return this;
		}
		
		// Step 3: session mgr
		public AuthBuilderSteps.Step4_ServerMgr UseDefaultSessionManager()
		{
			SessionFactory = (Owner) => new _SESSION();
			return this;
		}
		public AuthBuilderSteps.Step4_ServerMgr SetSessionManager(_SESSION SessionMgr)
		{
			SessionFactory = (Owner) => SessionMgr;
			return this;
		}
		public AuthBuilderSteps.Step4_ServerMgr SetSessionManager(DgtSessionFactory _SessionFactory)
		{
			SessionFactory = _SessionFactory;
			return this;
		}

		// Step 4: server mgr
		public AuthBuilderSteps.Step5_ClientIP UseDefaultServerManager()
		{
			ServerFactory = (Owner, ClientIpAddress) => new _SERVER(ClientIpAddress);
			return this;
		}
		public AuthBuilderSteps.Step5_ClientIP SetServerManager(_SERVER ServerMgr)
		{
			ServerFactory = (Owner, ClientIpAddress) => ServerMgr;
			return this;
		}
		public AuthBuilderSteps.Step5_ClientIP SetServerManager(DgtServerFactory _ServerFactory)
		{
			ServerFactory = _ServerFactory;
			return this;
		}

		// Step 5: client IP
		private string ClientIpAddress = "0.0.0.0";
		public AuthBuilderSteps.Final SetClientIp(string _ClientIpAddress)
		{
			ClientIpAddress = _ClientIpAddress;
			return this;
		}
		public AuthBuilderSteps.Final SetClientIp(IPAddress _ClientIpAddress)
		{
			ClientIpAddress = _ClientIpAddress.ToString();
			return this;
		}

		/// <summary>
		/// Call this once all options are set. Have you configured: (i) database connection, (ii) database table, (iii) remote IP?
		/// </summary>
		/// <returns></returns>
		public Auth Build()
		{
			bool AuthMustDisposePdoDatabase = false;
			// Guarantee that PdoDatabase exists
			if (!(PdoDatabase is object))
			{
				// Guarantee that PdoDsn exists (required to create PdoDatabase / PdoInstance)
				if (!(PdoDsn is object))
				{
					Npgsql.NpgsqlConnectionStringBuilder _Dsn;
					if (!string.IsNullOrEmpty(DbDsn))
						_Dsn = new Npgsql.NpgsqlConnectionStringBuilder(DbDsn);
					else
						_Dsn = new Npgsql.NpgsqlConnectionStringBuilder();
					if (!string.IsNullOrEmpty(DbHost)) _Dsn.Host = DbHost;
					if (DbPort is int iDbPort) _Dsn.Port = iDbPort;
					if (!string.IsNullOrEmpty(DbUsername)) _Dsn.Username = DbUsername;
					if (!string.IsNullOrEmpty(DbPassword)) _Dsn.Password = DbPassword;
					if (!string.IsNullOrEmpty(DbDatabase)) _Dsn.Database = DbDatabase;

					PdoDsn = new PdoDsn(_Dsn.ConnectionString, _Dsn.Username, _Dsn.Password);
				}

				bool PdoDatabaseMustDisposePdoInstance = false;
				// Guarantee that PdoInstance exists (required to create PdoDatabase)
				if (!(PdoInstance is object))
				{
					PdoInstance = new PdoFromDsn(PdoDsn.getDsn(), PdoDsn.getUsername(), PdoDsn.getPassword());
					PdoDatabaseMustDisposePdoInstance = true;
				}

				PdoDatabase = new PdoDatabase(PdoInstance, PdoDsn);
				PdoDatabase.MustDisposePdoInstance = PdoDatabaseMustDisposePdoInstance;
				AuthMustDisposePdoDatabase = true;
			}

			// Php instance
			var Inst = new PhpInstance(null, null, null);
			Inst._COOKIE = CookieFactory(Inst);
			Inst._SESSION = SessionFactory(Inst);
			Inst._SERVER = ServerFactory(Inst, ClientIpAddress);

			// Create Auth object
			var Ret = new Auth(PdoDatabase, Inst, ClientIpAddress, DbTablePrefix, null, null, null);
			Ret.MustDisposePdoDatabase = AuthMustDisposePdoDatabase;

			return Ret;
		}
	}

}

namespace CSharpAuth.Auth.AuthBuilderSteps
{
	public interface Step1_Database
	{
		AuthBuilderSteps.Step2_CookieMgr SetDatabase(PdoDatabase pdoDatabase);
		AuthBuilderSteps.Step2_CookieMgr SetDatabase(PDO pdoInstance);
		AuthBuilderSteps.Step2_CookieMgr SetDatabaseParams(PdoDsn dsn);
		AuthBuilderSteps.Step2_CookieMgr SetDatabaseParams(string Username, string Password, string DatabaseName, string Host = "127.0.0.1", int Port = 5432);
		AuthBuilderSteps.Step1_Database SetDatabaseTablePrefix(string TablePrefix);
		AuthBuilderSteps.Step2_CookieMgr SetDatabaseParamsWithDsn(string Dsn, string Username, string Password);
	}
	public interface Step2_CookieMgr
	{
		AuthBuilderSteps.Step3_SessionMgr UseEmulatedCookieManager();
		AuthBuilderSteps.Step3_SessionMgr SetCookieManager(_COOKIE CookieMgr);
		AuthBuilderSteps.Step3_SessionMgr SetCookieManager(DgtCookieFactory _CookieFactory);
	}
	public interface Step3_SessionMgr
	{
		AuthBuilderSteps.Step4_ServerMgr UseDefaultSessionManager();
		AuthBuilderSteps.Step4_ServerMgr SetSessionManager(_SESSION SessionMgr);
		AuthBuilderSteps.Step4_ServerMgr SetSessionManager(DgtSessionFactory _SessionFactory);
	}
	public interface Step4_ServerMgr
	{
		AuthBuilderSteps.Step5_ClientIP UseDefaultServerManager();
		AuthBuilderSteps.Step5_ClientIP SetServerManager(_SERVER ServerMgr);
		AuthBuilderSteps.Step5_ClientIP SetServerManager(DgtServerFactory _ServerFactory);
	}
	public interface Step5_ClientIP
	{
		AuthBuilderSteps.Final SetClientIp(string _ClientIpAddress);
		AuthBuilderSteps.Final SetClientIp(IPAddress _ClientIpAddress);
	}
	public interface Final
	{
		Auth Build();
	}
}