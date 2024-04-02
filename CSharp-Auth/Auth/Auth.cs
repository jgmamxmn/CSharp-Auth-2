using System;
using System.Collections.Generic;
using System.Text;
using Delight.Cookie;
using Delight.Db;
using System.Linq;
using Delight.Shim;
using System.Threading;
using System.Net;
using System.Runtime.CompilerServices;

/*
 * PHP-Auth (https://github.com/delight-im/PHP-Auth)
 * Copyright (c) delight.im (https://www.delight.im/)
 * Licensed under the MIT License (https://opensource.org/licenses/MIT)
 */

namespace Delight.Auth
{

	/** Component that provides all features and utilities for secure authentication of individual users */
	sealed public class Auth : UserManager {

		public static readonly string[] COOKIE_PREFIXES = new string[] { Delight.Cookie.Cookie.PREFIX_SECURE, Delight.Cookie.Cookie.PREFIX_HOST };
		internal const string COOKIE_CONTENT_SEPARATOR = "~";

		/** @var string the user"s current IP address */
		private string ipAddress;
		/** @var bool whether throttling should be enabled (e.g. in production) or disabled (e.g. during development) */
		private bool throttling;
		/** @var int the interval in seconds after which to resynchronize the session data with its authoritative source in the database */
		private int sessionResyncInterval;
		/** @var string the name of the cookie used for the "remember me" feature */
		private string rememberCookieName;

		public delegate bool DgtOnBeforeSuccess(int user_id);

		/**
		 * @param PdoDatabase|PdoDsn|\
		 * @param string|null ipAddress 
		 * @param string|null dbTablePrefix 
		 * @param bool|null throttling 
		 * @param int|null sessionResyncInterval 
		 * @param string|null dbSchema 
		 */

		/*
		/// <summary>
		/// 
		/// </summary>
		/// <param name="databaseConnection">PDO databaseConnection the database connection to operate on</param>
		/// <param name="ipAddress">(optional) the IP address that should be used instead of the default setting (if any), e.g. when behind a proxy</param>
		/// <param name="dbTablePrefix">(optional) the prefix for the names of all database tables used by this component</param>
		/// <param name="throttling">(optional) whether throttling should be enabled (e.g. in production) or disabled (e.g. during development)</param>
		/// <param name="sessionResyncInterval">(optional) the interval in seconds after which to resynchronize the session data with its authoritative source in the database</param>
		/// <param name="dbSchema">(optional) the schema name for all database tables used by this component</param>
		/// <param name="CallbackToInitializeEnvironment">(optional) a user-specified callback function that can do things to the environmental emulation objects (e.g. add cookies to the environment)</param>
		/// <param name="shimCookie">(optional) an existing cookie emulator object. you will only have one of these if you are sharing an environment across multiple Auth objects, and you already have an Auth object initialized</param>
		/// <param name="shimSession">(optional) an existing session emulator object. you will only have one of these if you are sharing an environment across multiple Auth objects, and you already have an Auth object initialized</param>
		/// <param name="shimServer">(optional) an existing server environment emulator object. you will only have one of these if you are sharing an environment across multiple Auth objects, and you already have an Auth object initialized</param>
		[Obsolete("It's recommended to use the Create method, with extensions, to create an Auth object")]
		public Auth(PdoDatabase _databaseConnection, string _clientIpAddress, string _dbTablePrefix = null, bool? _throttling = null,
			int? _sessionResyncInterval = null, string _dbSchema = null,
			Action<Auth, Shim._COOKIE, Shim._SESSION, Shim._SERVER> CallbackToInitializeEnvironment = null,
			Shim._COOKIE shimCookie = null, Shim._SESSION shimSession = null, Shim._SERVER shimServer = null)
			: base(_databaseConnection, _dbTablePrefix, _dbSchema,
				  shimCookie ?? new Shim._COOKIE(),
				  shimSession ?? new Shim._SESSION(),
				  shimServer ?? new Shim._SERVER(_clientIpAddress))
		{
			DoConstructor(_databaseConnection, _clientIpAddress, _dbTablePrefix, _throttling, _sessionResyncInterval, _dbSchema, CallbackToInitializeEnvironment, shimCookie, shimSession, shimServer);
		}*/

		public Auth(PdoDatabase _databaseConnection, PhpInstance _phpInstance, string _clientIpAddress,
			string _dbTablePrefix = null, string _dbSchema = null, 
			bool? _throttling = null, int? _sessionResyncInterval = null)
			: base(_databaseConnection, _dbTablePrefix, _dbSchema, _phpInstance)
		{
			this.ipAddress = !Php.empty(_clientIpAddress) ? _clientIpAddress : (!string.IsNullOrEmpty(_SERVER.REMOTE_ADDR) ? _SERVER.REMOTE_ADDR : null);
			this.throttling = _throttling ?? true;
			this.sessionResyncInterval = _sessionResyncInterval ?? (60 * 5);
			this.rememberCookieName = createRememberCookieName();

			this.initSessionIfNecessary();
			this.enhanceHttpSecurity();

			this.processRememberDirective();
			this.resyncSessionIfNecessary();
		}


		/// <summary>
		/// Returns an AuthBuilder object that can be used to create a suitable Auth object. Once created, daisychain its methods to set various options,
		/// then, finally, call Build() to build the Auth object
		/// </summary>
		/// <returns></returns>
		public static AuthBuilder Create()
		{
			return new AuthBuilder();
		}
		public class AuthBuilder
		{
			public delegate Shim._COOKIE DgtCookieFactory(PhpInstance Owner);
			public delegate Shim._SESSION DgtSessionFactory(PhpInstance Owner);
			public delegate Shim._SERVER DgtServerFactory(PhpInstance Owner, string ClientIpAddress);
			private DgtCookieFactory CookieFactory;
			private DgtSessionFactory SessionFactory;
			private DgtServerFactory ServerFactory;

			public AuthBuilder()
			{
				CookieFactory = (Owner) => new EmulatedCookieMgr();
				SessionFactory = (Owner) => new _SESSION();
				ServerFactory = (Owner, ClientIpAddress) => new _SERVER(ClientIpAddress);
			}

			private PdoDatabase PdoDatabase=null;
			private PDO PdoInstance = null;
			private PdoDsn PdoDsn = null;
			private string DbUsername=null, DbPassword=null, DbHost=null, DbDsn=null, DbDatabase = null, DbTablePrefix = null;
			private int? DbPort = null;
			public AuthBuilder SetDatabase(PdoDatabase pdoDatabase)
			{
				PdoDatabase = pdoDatabase;
				return this;
			}
			public AuthBuilder SetDatabase(PDO pdoInstance)
			{
				PdoInstance = pdoInstance;
				return this;
			}
			public AuthBuilder SetDatabaseParams(PdoDsn dsn)
			{
				PdoDsn = dsn;
				return this;
			}
			public AuthBuilder SetDatabaseParams(string Username, string Password)
			{
				DbUsername = Username;
				DbPassword = Password;
				return this;
			}
			public AuthBuilder SetDatabaseParams(string Host, int Port, string Username, string Password)
			{
				DbHost = Host;
				DbPort = Port;
				DbUsername = Username;
				DbPassword = Password;
				return this;
			}
			public AuthBuilder SetDatabaseName(string Database)
			{
				DbDatabase = Database;
				return this;
			}
			public AuthBuilder SetDatabaseTablePrefix(string TablePrefix)
			{
				DbTablePrefix = TablePrefix;
				return this;
			}
			public AuthBuilder SetDatabaseParams(string Dsn, string Username, string Password)
			{
				DbDsn = Dsn;
				DbUsername = Username;
				DbPassword = Password;
				return this;
			}

			public AuthBuilder SetCookieManager(_COOKIE CookieMgr)
			{
				CookieFactory = (Owner) => CookieMgr;
				return this;
			}
			public AuthBuilder SetCookieManager(DgtCookieFactory _CookieFactory)
			{
				CookieFactory = _CookieFactory;
				return this;
			}
			public AuthBuilder SetSessionManager(_SESSION SessionMgr)
			{
				SessionFactory = (Owner) => SessionMgr;
				return this;
			}
			public AuthBuilder SetSessionManager(DgtSessionFactory _SessionFactory)
			{
				SessionFactory = _SessionFactory;
				return this;
			}
			public AuthBuilder SetServerManager(_SERVER ServerMgr)
			{
				ServerFactory = (Owner, ClientIpAddress) => ServerMgr;
				return this;
			}
			public AuthBuilder SetServerManager(DgtServerFactory _ServerFactory)
			{
				ServerFactory = _ServerFactory;
				return this;
			}

			private string ClientIpAddress = "0.0.0.0";
			public AuthBuilder SetClientIp(string _ClientIpAddress)
			{
				ClientIpAddress = _ClientIpAddress;
				return this;
			}
			public AuthBuilder SetClientIp(IPAddress _ClientIpAddress)
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

					bool PdoDatabaseMustDisposePdoInstance=false;
					// Guarantee that PdoInstance exists (required to create PdoDatabase)
					if (!(PdoInstance is object))
					{
						PdoInstance = new PDO(PdoDsn.getDsn(), PdoDsn.getUsername(), PdoDsn.getPassword());
      						PdoDatabaseMustDisposePdoInstance=true;
					}

					PdoDatabase = new PdoDatabase(PdoInstance, PdoDsn);
     					PdoDatabase.MustDisposePdoInstance = PdoDatabaseMustDisposePdoInstance;
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

	
		/*private void DoConstructor(PdoDatabase _databaseConnection, string _clientIpAddress, string _dbTablePrefix = null, bool? _throttling = null,
			int? _sessionResyncInterval = null, string _dbSchema = null,
			Action<Auth, Shim._COOKIE, Shim._SESSION, Shim._SERVER> CallbackToInitializeEnvironment = null,
			Shim._COOKIE shimCookie = null, Shim._SESSION shimSession = null, Shim._SERVER shimServer = null)
		{ 
			if (CallbackToInitializeEnvironment != null)
				CallbackToInitializeEnvironment(this, this._COOKIE, this._SESSION, this._SERVER);

			this.ipAddress = !Php.empty(_clientIpAddress) ? _clientIpAddress : (Php.isset(_SERVER.REMOTE_ADDR) ? _SERVER.REMOTE_ADDR : null);
			this.throttling = _throttling ?? true;
			this.sessionResyncInterval = _sessionResyncInterval ?? (60 * 5);
			this.rememberCookieName = createRememberCookieName();

			this.initSessionIfNecessary();
			this.enhanceHttpSecurity();

			this.processRememberDirective();
			this.resyncSessionIfNecessary();
		}*/

		/** Initializes the session and sets the correct configuration */
		private void initSessionIfNecessary() {
			/*if (\session_status() == \PHP_SESSION_NONE) {
			// use cookies to store session IDs
			\ini_set("session.use_cookies", 1);
			// use cookies only (do not send session IDs in URLs)
			\ini_set("session.use_only_cookies", 1);
			// do not send session IDs in URLs
			\ini_set("session.use_trans_sid", 0);

				// start the session (requests a cookie to be written on the client)
				@Session::start();
			}*/
		}

		/** Improves the application"s security over HTTP(S) by setting specific headers */
		private void enhanceHttpSecurity() {
			// remove exposure of PHP version (at least where possible)
			PhpInstance.header_remove("X-Powered-By");

			// if the user is signed in
			if (this.isLoggedIn()) {
				// prevent clickjacking
				PhpInstance.header("X-Frame-Options: sameorigin");
				// prevent content sniffing (MIME sniffing)
				PhpInstance.header("X-Content-Type-Options: nosniff");

				// disable caching of potentially sensitive data
				PhpInstance.header("Cache-Control: no-store, no-cache, must-revalidate", true);
				PhpInstance.header("Expires: Thu, 19 Nov 1981 00:00:00 GMT", true);
				PhpInstance.header("Pragma: no-cache", true);
			}
		}

		/** Checks if there is a "remember me" directive set and handles the automatic login (if appropriate) */
		private void processRememberDirective() {
			// if the user is not signed in yet
			if (!this.isLoggedIn()) 
			{
				// if there is currently no cookie for the "remember me" feature
				if (!Php.isset(_COOKIE, this.rememberCookieName)) 
				{
					// if an old cookie for that feature from versions v1.x.x to v6.x.x has been found
					//if (Php.isset(_COOKIE, "auth_remember")) 
					if(_COOKIE.TryGetValue("auth_remember", out Cookie.Cookie authRememberCookie))
					{
						// use the value from that old cookie instead
						_COOKIE.Set(this.rememberCookieName, authRememberCookie);
					}
				}

				// if a remember cookie is set
				if (Php.isset(_COOKIE, this.rememberCookieName)) 
				{
					// assume the cookie and its contents to be invalid until proven otherwise
					var valid = false;

					// split the cookie"s content into selector and token
					var parts = Php.explode(COOKIE_CONTENT_SEPARATOR, _COOKIE.Get(this.rememberCookieName).getValue(), 2);

					DatabaseResultRow rememberData;

					// if both selector and token were found
					if (!Php.empty(parts[0]) && !Php.empty(parts[1])) {
						try {
							rememberData = this.db.selectRow(
								"SELECT a.user, a.token, a.expires, b.email, b.username, b.status, b.roles_mask, b.force_logout FROM " + this.makeTableName("users_remembered") + " AS a JOIN " + this.makeTableName("users") + " AS b ON a.user = b.id WHERE a.selector = @selector",
								new BindValues { { "@selector", parts[0] } }
							);
						}
						catch (Exception e) {
							throw new DatabaseError(e.Message);
						}

						if (!Php.empty(rememberData)) {
							if ((Delight.Shim.MasterCaster.GetInt(rememberData["expires"])) >= Php.time()) {
								if (Php.password_verify(parts[1], Delight.Shim.MasterCaster.GetString(rememberData["token"]))) {
									// the cookie and its contents have now been proven to be valid
									valid = true;

									this.onLoginSuccessful
									(
										Delight.Shim.MasterCaster.GetInt(rememberData["user"]),
										Delight.Shim.MasterCaster.GetString(rememberData["email"]),
										Delight.Shim.MasterCaster.GetString(rememberData["username"]), 
										(Status)Delight.Shim.MasterCaster.GetInt(rememberData["status"]), 
										(Roles)Delight.Shim.MasterCaster.GetInt(rememberData["roles_mask"]),
										Delight.Shim.MasterCaster.GetInt(rememberData["force_logout"]),
										true
									);
								}
							}
						}
					}

					// if the cookie or its contents have been invalid
					if (!valid)
					{
						// mark the cookie as such to prevent any further futile attempts
						this.setRememberCookie("", "", DateTime.Now.AddDays(365));
							//(int)(time() + 60 * 60 * 24 * 365.25));
					}
				}
			}
		}

		private void resyncSessionIfNecessary() {
			// if the user is signed in
			if (this.isLoggedIn()) {
				// the following session field may not have been initialized for sessions that had already existed before the introduction of this feature
				if (_SESSION.SESSION_FIELD_LAST_RESYNC==null) {
					_SESSION.SESSION_FIELD_LAST_RESYNC = 0;
				}

				DatabaseResultRow authoritativeData;

				// if it"s time for resynchronization
				if ((_SESSION.SESSION_FIELD_LAST_RESYNC! + this.sessionResyncInterval) <= Php.time()) {
					// fetch the authoritative data from the database again
					try {
						authoritativeData = this.db.selectRow(
							"SELECT email, username, status, roles_mask, force_logout FROM " + this.makeTableName("users") + " WHERE id = @id",
							new BindValues { { "@id", this.getUserId() } }
						);
					}
					catch (Exception e) {
						throw new DatabaseError(e.Message);
					}

					// if the user"s data has been found
					if (!Php.empty(authoritativeData)) {
						// the following session field may not have been initialized for sessions that had already existed before the introduction of this feature
						if (_SESSION.SESSION_FIELD_FORCE_LOGOUT == null) {
							_SESSION.SESSION_FIELD_FORCE_LOGOUT = 0;
						}

						// if the counter that keeps track of forced logouts has been incremented
						if ((Delight.Shim.MasterCaster.GetInt(authoritativeData["force_logout"])) > (_SESSION.SESSION_FIELD_FORCE_LOGOUT!)) {
							// the user must be signed out
							this.logOut();
						}
						// if the counter that keeps track of forced logouts has remained unchanged
						else {
							// the session data needs to be updated
							_SESSION.SESSION_FIELD_EMAIL = Delight.Shim.MasterCaster.GetString(authoritativeData["email"]);
							_SESSION.SESSION_FIELD_USERNAME = Delight.Shim.MasterCaster.GetString(authoritativeData["username"]);
							_SESSION.SESSION_FIELD_STATUS = (Status)Delight.Shim.MasterCaster.GetInt(authoritativeData["status"]);
							_SESSION.SESSION_FIELD_ROLES = (Roles)Delight.Shim.MasterCaster.GetInt(authoritativeData["roles_mask"]);

							// remember that we"ve just performed the required resynchronization
							_SESSION.SESSION_FIELD_LAST_RESYNC = Php.time();
						}
					}
					// if no data has been found for the user
					else {
						// their account may have been deleted so they should be signed out
						this.logOut();
					}
				}
			}
		}

		/**
		 * Attempts to sign up a user
		 *
		 * If you want the user"s account to be activated by default, pass `null` as the callback
		 *
		 * If you want to make the user verify their email address first, pass an anonymous void as the callback
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param string email the email address to register
		 * @param string password the password for the new account
		 * @param string|null username (optional) the username that will be displayed
		 * @param callable|null callback (optional) the void that sends the confirmation email to the user
		 * @return int the ID of the user that has been created (if any)
		 * @throws InvalidEmailException if the email address was invalid
		 * @throws InvalidPasswordException if the password was invalid
		 * @throws UserAlreadyExistsException if a user with the specified email address already exists
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see confirmEmail
		 * @see confirmEmailAndSignIn
		 */
		public int register(string email, string password, string username = null, DgtConfirmationEmail callback = null) {
			this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);
			this.throttle(new[] { "createNewAccount", this.getIpAddress() }, 1, (60 * 60 * 12), 5, true);

			var newUserId = this.createUserInternal(false, email, password, username, callback);

			this.throttle(new[] { "createNewAccount", this.getIpAddress() }, 1, (60 * 60 * 12), 5, false);

			return newUserId;
		}

		/**
		 * Attempts to sign up a user while ensuring that the username is unique
		 *
		 * If you want the user"s account to be activated by default, pass `null` as the callback
		 *
		 * If you want to make the user verify their email address first, pass an anonymous void as the callback
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param string email the email address to register
		 * @param string password the password for the new account
		 * @param string|null username (optional) the username that will be displayed
		 * @param callable|null callback (optional) the void that sends the confirmation email to the user
		 * @return int the ID of the user that has been created (if any)
		 * @throws InvalidEmailException if the email address was invalid
		 * @throws InvalidPasswordException if the password was invalid
		 * @throws UserAlreadyExistsException if a user with the specified email address already exists
		 * @throws DuplicateUsernameException if the specified username wasn"t unique
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see confirmEmail
		 * @see confirmEmailAndSignIn
		 */
		public int registerWithUniqueUsername(string email, string password, string username = null, DgtConfirmationEmail callback = null) {
			this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);
			this.throttle(new[] { "createNewAccount", this.getIpAddress() }, 1, (60 * 60 * 12), 5, true);

			var newUserId = this.createUserInternal(true, email, password, username, callback);

			this.throttle(new[] { "createNewAccount", this.getIpAddress() }, 1, (60 * 60 * 12), 5, false);

			return newUserId;
		}

		/**
		 * Attempts to sign in a user with their email address and password
		 *
		 * @param string email the user"s email address
		 * @param string password the user"s password
		 * @param int|null rememberDuration (optional) the duration in seconds to keep the user logged in ("remember me"), e.g. `60 * 60 * 24 * 365.25` for one year
		 * @param callable|null onBeforeSuccess (optional) a void that receives the user"s ID as its single parameter and is executed before successful authentication; must return `true` to proceed or `false` to cancel
		 * @throws InvalidEmailException if the email address was invalid or could not be found
		 * @throws InvalidPasswordException if the password was invalid
		 * @throws EmailNotVerifiedException if the email address has not been verified yet via confirmation email
		 * @throws AttemptCancelledException if the attempt has been cancelled by the supplied callback that is executed before success
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void login(string email, string password, int? rememberDuration = null, DgtOnBeforeSuccess onBeforeSuccess = null) {
			this.throttle(new[] { "attemptToLogin", "email", email }, 500, (60 * 60 * 24), simulated: true);

			this.authenticateUserInternal(password, email, null, rememberDuration, onBeforeSuccess);
		}

		/**
		 * Attempts to sign in a user with their username and password
		 *
		 * When using this method to authenticate users, you should ensure that usernames are unique
		 *
		 * Consistently using {@see registerWithUniqueUsername} instead of {@see register} can be helpful
		 *
		 * @param string username the user"s username
		 * @param string password the user"s password
		 * @param int|null rememberDuration (optional) the duration in seconds to keep the user logged in ("remember me"), e.g. `60 * 60 * 24 * 365.25` for one year
		 * @param callable|null onBeforeSuccess (optional) a void that receives the user"s ID as its single parameter and is executed before successful authentication; must return `true` to proceed or `false` to cancel
		 * @throws UnknownUsernameException if the specified username does not exist
		 * @throws AmbiguousUsernameException if the specified username is ambiguous, i.e. there are multiple users with that name
		 * @throws InvalidPasswordException if the password was invalid
		 * @throws EmailNotVerifiedException if the email address has not been verified yet via confirmation email
		 * @throws AttemptCancelledException if the attempt has been cancelled by the supplied callback that is executed before success
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void loginWithUsername(string username, string password, int? rememberDuration = null, DgtOnBeforeSuccess onBeforeSuccess = null) {
			this.throttle(new[] { "attemptToLogin", "username", username }, 500, (60 * 60 * 24), 1, true);
			this.authenticateUserInternal(password, null, username, rememberDuration, onBeforeSuccess);
		}

		/**
		 * Attempts to confirm the currently signed-in user"s password again
		 *
		 * Whenever you want to confirm the user"s identity again, e.g. before
		 * the user is allowed to perform some "dangerous" action, you should
		 * use this method to confirm that the user is who they claim to be.
		 *
		 * For example, when a user has been remembered by a long-lived cookie
		 * and thus {@see isRemembered} returns `true`, this means that the
		 * user has not entered their password for quite some time anymore.
		 *
		 * @param string password the user"s password
		 * @return bool whether the supplied password has been correct
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public bool reconfirmPassword(string password) {
			if (this.isLoggedIn()) {
				try {
					password = validatePassword(password);
				}
				catch (InvalidPasswordException) {
					return false;
				}

				this.throttle(new[] { "reconfirmPassword", this.getIpAddress() }, 3, (60 * 60), 4, true);

				string expectedHash;
				try {
					expectedHash = Shim.MasterCaster.GetString(this.db.selectValue(
						"SELECT password FROM " + this.makeTableName("users") + " WHERE id = @id",
						new BindValues { { "@id", this.getUserId() } }
					));
				}
				catch (Exception e) {
					throw new DatabaseError(e.Message);
				}

				if (!Php.empty(expectedHash)) {
					bool validated = Php.password_verify(password, expectedHash);

					if (!validated) {
						this.throttle(new[] { "reconfirmPassword", this.getIpAddress() }, 3, (60 * 60), 4, false);
					}

					return validated;
				}
				else {
					throw new NotLoggedInException();
				}
			}
			else {
				throw new NotLoggedInException();
			}
		}

		/**
		 * Logs the user out
		 *
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void logOut() {
			// if the user has been signed in
			if (this.isLoggedIn()) {
				// retrieve any locally existing remember directive
				var rememberDirectiveSelector = this.getRememberDirectiveSelector();

				// if such a remember directive exists
				if (!string.IsNullOrEmpty(rememberDirectiveSelector)) {
					// delete the local remember directive
					this.deleteRememberDirectiveForUserById(
						this.getUserId(),
						rememberDirectiveSelector
					);
				}

				// remove all session variables maintained by this library
				_SESSION.SESSION_FIELD_LOGGED_IN=null;
				_SESSION.SESSION_FIELD_USER_ID=null;
				_SESSION.SESSION_FIELD_EMAIL=null;
				_SESSION.SESSION_FIELD_USERNAME=null;
				_SESSION.SESSION_FIELD_STATUS=null;
				_SESSION.SESSION_FIELD_ROLES=null;
				_SESSION.SESSION_FIELD_REMEMBERED=null;
				_SESSION.SESSION_FIELD_LAST_RESYNC=null;
				_SESSION.SESSION_FIELD_FORCE_LOGOUT=null;
			}
		}

		/**
		 * Logs the user out in all other sessions (except for the current one)
		 *
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void logOutEverywhereElse() {
			if (!this.isLoggedIn()) {
				throw new NotLoggedInException();
			}

			// determine the expiry date of any locally existing remember directive
			var previousRememberDirectiveExpiry = this.getRememberDirectiveExpiry();

			// schedule a forced logout in all sessions
			this.forceLogoutForUserById(this.getUserId());

			// the following session field may not have been initialized for sessions that had already existed before the introduction of this feature
			if (_SESSION.SESSION_FIELD_FORCE_LOGOUT == null) {
				_SESSION.SESSION_FIELD_FORCE_LOGOUT = 0;
			}

			// ensure that we will simply skip or ignore the next forced logout (which we have just caused) in the current session
			_SESSION.SESSION_FIELD_FORCE_LOGOUT++;

			// re-generate the session ID to prevent session fixation attacks (requests a cookie to be written on the client)
			Cookie.Session.regenerate(PhpInstance, true);

			// if there had been an existing remember directive previously
			if (previousRememberDirectiveExpiry!=-1) {
				// restore the directive with the old expiry date but new credentials
				this.createRememberDirective(
					this.getUserId(),
					previousRememberDirectiveExpiry - Php.time()
				);
			}
		}

		/**
		 * Logs the user out in all sessions
		 *
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void logOutEverywhere() {
			if (!this.isLoggedIn()) {
				throw new NotLoggedInException();
			}

			// schedule a forced logout in all sessions
			this.forceLogoutForUserById(this.getUserId());
			// and immediately apply the logout locally
			this.logOut();
		}

		/**
		 * Destroys all session data
		 *
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void destroySession() {
			// remove all session variables without exception
			_SESSION.Clear();
			// delete the session cookie
			this.deleteSessionCookie();
			// let PHP destroy the session
			PhpInstance.session_destroy();
		}

		/**
		 * Creates a new directive keeping the user logged in ("remember me")
		 *
		 * @param int userId the user ID to keep signed in
		 * @param int duration the duration in seconds
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void createRememberDirective(int userId, int? duration) {
			var selector = createRandomString(24);
			var token = createRandomString(32);
			var tokenHashed = Php.password_hash(token, Php.PASSWORD_ALGO.PASSWORD_DEFAULT);
			DateTime? expires = 
				(duration is int iDuration ? DateTime.Now.AddSeconds(iDuration) : null);

			try {
				this.db.insert(
					this.makeTableNameComponents_("users_remembered"),
					new Dictionary<string, object>
					{
						{"user" , userId },
						{"selector" , selector},
						{"token" , tokenHashed},
						{"expires" , (expires is DateTime dtExpires ? (dtExpires-new DateTime(1970,1,1)).TotalSeconds : 0) }
					}
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			this.setRememberCookie(selector, token, expires);
		}

		override protected void deleteRememberDirectiveForUserById(int userId, string selector = null) {
			base.deleteRememberDirectiveForUserById(userId, selector);

			this.setRememberCookie(null, null, DateTime.Now.AddSeconds(-3600));
		}

		/**
		 * Sets or updates the cookie that manages the "remember me" token
		 *
		 * @param string|null selector the selector from the selector/token pair
		 * @param string|null token the token from the selector/token pair
		 * @param int expires the UNIX time in seconds which the token should expire at
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void setRememberCookie(string selector, string token, DateTime? expires)
		{
			/*bool cookieBool(in string _cookie)
			{
				if (_cookie == "1") return true;
				var _cookielc = _cookie.ToLower();
				if (_cookielc == "true" || _cookielc == "yes" || _cookielc == "on") return true;
				return false;
			}*/

			var myParams = PhpInstance.session_get_cookie_params();

			string content;

			if (!string.IsNullOrEmpty(selector) && !string.IsNullOrEmpty(token)) {
				content = selector + COOKIE_CONTENT_SEPARATOR + token;
			}
			else {
				content = "";
			}

			// save the cookie with the selector and token (requests a cookie to be written on the client)
			{
				var cookie = new Delight.Cookie.Cookie(this.rememberCookieName, PhpInstance);
				cookie.setValue(content);
				cookie.setExpiryTime(expires);
				cookie.setPath(myParams.path);
				cookie.setDomain(myParams.domain);
				cookie.setHttpOnly(myParams.httponly);
				cookie.setSecureOnly(myParams.secure);
				var result = cookie.save();

				if (result == false) {
					throw new HeadersAlreadySentError();
				}
			}

			// if we"ve been deleting the cookie above
			if (string.IsNullOrEmpty(selector) || string.IsNullOrEmpty(token)) {
				// attempt to delete a potential old cookie from versions v1.x.x to v6.x.x as well (requests a cookie to be written on the client)
				var cookie = new Delight.Cookie.Cookie("auth_remember", PhpInstance);
				cookie.setPath((!Php.empty(myParams.path)) ? myParams.path : "/");
				cookie.setDomain(myParams.domain);
				cookie.setHttpOnly(myParams.httponly);
				cookie.setSecureOnly(myParams.secure);
				cookie.delete();
			}
		}

		override protected void onLoginSuccessful(int userId, string email, string username, Status status, Roles roles, int forceLogout, bool remembered) {
			// update the timestamp of the user"s last login
			try {
				this.db.update(
					this.makeTableNameComponents_("users"),

					new Dictionary<string, object>
					{
						{ "last_login" , Php.time() }
					},
					new Dictionary<string, object>
					{
						{ "id" , userId }
					}
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			base.onLoginSuccessful(userId, email, username, status, roles, forceLogout, remembered);
		}

		/**
		 * Deletes the session cookie on the client
		 *
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void deleteSessionCookie() {
			var myParams = PhpInstance.session_get_cookie_params();

			// ask for the session cookie to be deleted (requests a cookie to be written on the client)
			var cookie = new Delight.Cookie.Cookie(PhpInstance.session_name(), PhpInstance);
			cookie.setPath(myParams.path);
			cookie.setDomain(myParams.domain);
			cookie.setHttpOnly(myParams.httponly);
			cookie.setSecureOnly(myParams.secure);
			var result = cookie.delete();

			if (result == false) {
				throw new HeadersAlreadySentError();
			}
		}

		/**
		 * Confirms an email address (and activates the account) by supplying the correct selector/token pair
		 *
		 * The selector/token pair must have been generated previously by registering a new account
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @return string[] an array with the old email address (if any) at index zero and the new email address (which has just been verified) at index one
		 * @throws InvalidSelectorTokenPairException if either the selector or the token was not correct
		 * @throws TokenExpiredException if the token has already expired
		 * @throws UserAlreadyExistsException if an attempt has been made to change the email address to a (now) occupied address
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public string[] confirmEmail(string selector, string token) {
			this.throttle(new[] { "confirmEmail", this.getIpAddress() }, 5, (60 * 60), 10);
			this.throttle(new[] { "confirmEmail", "selector", selector }, 3, (60 * 60), 10);
			this.throttle(new[] { "confirmEmail", "token", token }, 3, (60 * 60), 10);

			DatabaseResultRow confirmationData = null;

			try
			{
				confirmationData = this.db.selectRow(
					"SELECT a.id, a.user_id, a.email AS new_email, a.token, a.expires, b.email AS old_email FROM " + this.makeTableName("users_confirmations") +
						" AS a JOIN " + this.makeTableName("users") + " AS b ON b.id = a.user_id WHERE a.selector = @selector",
					new BindValues {{ "@selector", selector }}
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (!Php.empty(confirmationData)) {
				if (Php.password_verify(token, Delight.Shim.MasterCaster.GetString(confirmationData["token"]))) {
					if (Delight.Shim.MasterCaster.GetInt(confirmationData["expires"]) >= Php.time()) {
						// invalidate any potential outstanding password reset requests
						try {
							this.db.delete(
								this.makeTableNameComponents_("users_resets"),
								new Dictionary<string, object> { { "user", confirmationData["user_id"] } }
							);
						}
						catch (Exception e) {
							throw new DatabaseError(e.Message);
						}

						// mark the email address as verified (and possibly update it to the new address given)
						try {
							this.db.update(
								this.makeTableNameComponents_("users"),
								new Dictionary<string, object>{
									{ "email" , confirmationData["new_email"] },
									{"verified" , 1 }
								},

								new Dictionary<string, object> { { "id", confirmationData["user_id"] } }
							);
						}
						catch (IntegrityConstraintViolationException) {
							throw new UserAlreadyExistsException();
						}
						catch (Exception e) {
							throw new DatabaseError(e.Message);
						}

						// if the user is currently signed in
						if (this.isLoggedIn()) {
							// if the user has just confirmed an email address for their own account
							if (this.getUserId() == Delight.Shim.MasterCaster.GetInt(confirmationData["user_id"])) {
								// immediately update the email address in the current session as well
								_SESSION.SESSION_FIELD_EMAIL = Delight.Shim.MasterCaster.GetString(confirmationData["new_email"]);
							}
						}

						// consume the token just being used for confirmation
						try {
							this.db.delete(
								this.makeTableNameComponents_("users_confirmations"),
								new Dictionary<string, object>
								{
									{ "id" , confirmationData["id"] }
								}
							);
						}
						catch (Exception e) {
							throw new DatabaseError(e.Message);
						}

						// if the email address has not been changed but simply been verified
						if (confirmationData["old_email"] == confirmationData["new_email"]) {
							// the output should not contain any previous email address
							confirmationData["old_email"] = null;
						}

						return new string[] {
							Shim.MasterCaster.GetString(confirmationData["old_email"]),
							Shim.MasterCaster.GetString(confirmationData["new_email"])
						};
					}
					else {
						throw new TokenExpiredException();
					}
				}
				else {
					throw new InvalidSelectorTokenPairException();
				}
			}
			else {
				throw new InvalidSelectorTokenPairException();
			}
		}

		/**
		 * Confirms an email address and activates the account by supplying the correct selector/token pair
		 *
		 * The selector/token pair must have been generated previously by registering a new account
		 *
		 * The user will be automatically signed in if this operation is successful
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @param int|null rememberDuration (optional) the duration in seconds to keep the user logged in ("remember me"), e.g. `60 * 60 * 24 * 365.25` for one year
		 * @return string[] an array with the old email address (if any) at index zero and the new email address (which has just been verified) at index one
		 * @throws InvalidSelectorTokenPairException if either the selector or the token was not correct
		 * @throws TokenExpiredException if the token has already expired
		 * @throws UserAlreadyExistsException if an attempt has been made to change the email address to a (now) occupied address
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public string[] confirmEmailAndSignIn(string selector, string token, int? rememberDuration = null) {
			var emailBeforeAndAfter = this.confirmEmail(selector, token);

			if (!this.isLoggedIn()) {
				if ((emailBeforeAndAfter?.Length ?? 0)>1) {
					emailBeforeAndAfter[1] = validateEmailAddress(emailBeforeAndAfter[1]);

					var userData = this.getUserDataByEmailAddress(
						emailBeforeAndAfter[1],
						new string[] { "id", "email", "username", "status", "roles_mask", "force_logout" }
					);

					this.onLoginSuccessful(userData.id, userData.email, userData.username, userData.status, userData.roles_mask, userData.force_logout, true);

					if (rememberDuration != null) {
						this.createRememberDirective(userData.id, rememberDuration);
					}
				}
			}

			return emailBeforeAndAfter;
		}

		/**
		 * Changes the currently signed-in user"s password while requiring the old password for verification
		 *
		 * @param string oldPassword the old password to verify account ownership
		 * @param string newPassword the new password that should be set
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws InvalidPasswordException if either the old password has been wrong or the desired new one has been invalid
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void changePassword(string oldPassword, string newPassword) {
			if (this.reconfirmPassword(oldPassword)) {
				this.changePasswordWithoutOldPassword(newPassword);
			}
			else {
				throw new InvalidPasswordException();
			}
		}

		/**
		 * Changes the currently signed-in user"s password without requiring the old password for verification
		 *
		 * @param string newPassword the new password that should be set
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws InvalidPasswordException if the desired new password has been invalid
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void changePasswordWithoutOldPassword(string newPassword) {
			if (this.isLoggedIn()) {
				newPassword = validatePassword(newPassword);
				this.updatePasswordInternal(this.getUserId(), newPassword);

				try {
					this.logOutEverywhereElse();
				}
				catch (NotLoggedInException) { }
			}
			else {
				throw new NotLoggedInException();
			}
		}

		/**
		 * Attempts to change the email address of the currently signed-in user (which requires confirmation)
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param string newEmail the desired new email address
		 * @param callable callback the void that sends the confirmation email to the user
		 * @throws InvalidEmailException if the desired new email address is invalid
		 * @throws UserAlreadyExistsException if a user with the desired new email address already exists
		 * @throws EmailNotVerifiedException if the current (old) email address has not been verified yet
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see confirmEmail
		 * @see confirmEmailAndSignIn
		 */
		public void changeEmail(string newEmail, DgtConfirmationEmail callback) {
			if (this.isLoggedIn()) {
				newEmail = validateEmailAddress(newEmail);

				this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);

				object existingUsersWithNewEmail;

				try
				{
					existingUsersWithNewEmail = this.db.selectValue(
						"SELECT COUNT(*) FROM " + this.makeTableName("users") + " WHERE email = @email",
						new BindValues {{ "@email", newEmail }}
					);
				}
				catch (Exception e) {
					throw new DatabaseError(e.Message);
				}

				if (Delight.Shim.MasterCaster.GetInt(existingUsersWithNewEmail) != 0) {
					throw new UserAlreadyExistsException();
				}

				object verified;

				try {
					verified = this.db.selectValue(
						"SELECT verified FROM "+ this.makeTableName("users")+ " WHERE id = @id",
						new BindValues { { "@id", this.getUserId() } }
					);
				}
				catch (Exception e) {
					throw new DatabaseError(e.Message);
				}

				// ensure that at least the current (old) email address has been verified before proceeding
				if (Delight.Shim.MasterCaster.GetInt(verified) != 1) {
					throw new EmailNotVerifiedException();
				}

				this.throttle(new[] { "requestEmailChange", "userId", this.getUserId().ToString() }, 1, (60 * 60 * 24));
				this.throttle(new[] { "requestEmailChange", this.getIpAddress() }, 1, (60 * 60 * 24), 3);

				this.createConfirmationRequest(this.getUserId(), newEmail, callback);
			}
			else {
				throw new NotLoggedInException();
			}
		}

		/**
		 * Attempts to re-send an earlier confirmation request for the user with the specified email address
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param string email the email address of the user to re-send the confirmation request for
		 * @param callable callback the void that sends the confirmation request to the user
		 * @throws ConfirmationRequestNotFound if no previous request has been found that could be re-sent
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 */
		public void resendConfirmationForEmail(string email, DgtConfirmationEmail callback) {
			this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);

			this.resendConfirmationForColumnValue("email", email, callback);
		}

		/**
		 * Attempts to re-send an earlier confirmation request for the user with the specified ID
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param int userId the ID of the user to re-send the confirmation request for
		 * @param callable callback the void that sends the confirmation request to the user
		 * @throws ConfirmationRequestNotFound if no previous request has been found that could be re-sent
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 */
		public void resendConfirmationForUserId(int userId, DgtConfirmationEmail callback) {
			this.resendConfirmationForColumnValue("user_id", userId, callback);
		}

		/**
		 * Attempts to re-send an earlier confirmation request
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * You must never pass untrusted input to the parameter that takes the column name
		 *
		 * @param string columnName the name of the column to filter by
		 * @param mixed columnValue the value to look for in the selected column
		 * @param callable callback the void that sends the confirmation request to the user
		 * @throws ConfirmationRequestNotFound if no previous request has been found that could be re-sent
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void resendConfirmationForColumnValue(string columnName, object columnValue, DgtConfirmationEmail callback) {
			
			DatabaseResultRow latestAttempt;

			try {
				latestAttempt = this.db.selectRow(
					"SELECT user_id, email FROM "+ this.makeTableName("users_confirmations")+ " WHERE \""+columnName+ "\" = @colval ORDER BY id DESC LIMIT 1 OFFSET 0",
					new BindValues { { "@colval", columnValue } }
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (latestAttempt == null) {
				throw new ConfirmationRequestNotFound();
			}

			this.throttle(new[] { "resendConfirmation", "userId", Delight.Shim.MasterCaster.GetString(latestAttempt["user_id"]) }, 1, (60 * 60 * 6));
			this.throttle(new[] { "resendConfirmation", this.getIpAddress() }, 4, (60 * 60 * 24 * 7), 2);

			this.createConfirmationRequest(
				Delight.Shim.MasterCaster.GetInt(latestAttempt["user_id"]),
				Delight.Shim.MasterCaster.GetString(latestAttempt["email"]),
				callback
			);
		}

		/**
		 * Initiates a password reset request for the user with the specified email address
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to proceed to the second step of the password reset, both pieces will be required again
		 *
		 * @param string email the email address of the user who wants to request the password reset
		 * @param callable callback the void that sends the password reset information to the user
		 * @param int|null requestExpiresAfter (optional) the interval in seconds after which the request should expire
		 * @param int|null maxOpenRequests (optional) the maximum number of unexpired and unused requests per user
		 * @throws InvalidEmailException if the email address was invalid or could not be found
		 * @throws EmailNotVerifiedException if the email address has not been verified yet via confirmation email
		 * @throws ResetDisabledException if the user has explicitly disabled password resets for their account
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see canResetPasswordOrThrow
		 * @see canResetPassword
		 * @see resetPassword
		 * @see resetPasswordAndSignIn
		 */
		public void forgotPassword(string email, DgtSendPasswordResetInfoToUser callback, int requestExpiresAfter = 60*60*6, int maxOpenRequests = 2) {
			email = validateEmailAddress(email);

			this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);

			var userData = this.getUserDataByEmailAddress(
				email,
				new[] { "id", "verified", "resettable" }
			);

			// ensure that the account has been verified before initiating a password reset
			if (!userData.verified) {
				throw new EmailNotVerifiedException();
			}

			// do not allow a password reset if the user has explicitly disabled this feature
			if (!userData.resettable) {
				throw new ResetDisabledException();
			}

			var openRequests = this.throttling ? this.getOpenPasswordResetRequests(userData.id) : 0;

			if (openRequests < maxOpenRequests) {
				this.throttle(new[] { "requestPasswordReset", this.getIpAddress() }, 4, (60 * 60 * 24 * 7), 2);
				this.throttle(new[] { "requestPasswordReset", "user", userData.id.ToString() }, 4, (60 * 60 * 24 * 7), 2);

				this.createPasswordResetRequest(userData.id, requestExpiresAfter, callback);
			}
			else {
				throw new TooManyRequestsException("", requestExpiresAfter, userData.id, openRequests, maxOpenRequests);
			}
		}

		// Old paramater format
		private void authenticateUserInternal(string password, string email, string username, bool rememberDuration, DgtOnBeforeSuccess onBeforeSuccess = null)
		{
			authenticateUserInternal(password, email, username,
				rememberDuration ? 60 * 60 * 24 * 28 : null,
				onBeforeSuccess);
		}

		/**
		 * Authenticates an existing user
		 *
		 * @param string password the user"s password
		 * @param string|null email (optional) the user"s email address
		 * @param string|null username (optional) the user"s username
		 * @param int|null rememberDuration (optional) the duration in seconds to keep the user logged in ("remember me"), e.g. `60 * 60 * 24 * 365.25` for one year
		 * @param callable|null onBeforeSuccess (optional) a void that receives the user"s ID as its single parameter and is executed before successful authentication; must return `true` to proceed or `false` to cancel
		 * @throws InvalidEmailException if the email address was invalid or could not be found
		 * @throws UnknownUsernameException if an attempt has been made to authenticate with a non-existing username
		 * @throws AmbiguousUsernameException if an attempt has been made to authenticate with an ambiguous username
		 * @throws InvalidPasswordException if the password was invalid
		 * @throws EmailNotVerifiedException if the email address has not been verified yet via confirmation email
		 * @throws AttemptCancelledException if the attempt has been cancelled by the supplied callback that is executed before success
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void authenticateUserInternal(string password, string email = null, string username = null, int? rememberDuration = null, DgtOnBeforeSuccess onBeforeSuccess = null) {
			this.throttle(new[] { "enumerateUsers", this.getIpAddress() }, 1, (60 * 60), 75);
			this.throttle(new[] { "attemptToLogin", this.getIpAddress() }, 4, (60 * 60), 5, true);

			var columnsToFetch = new[] { "id", "email", "password", "verified", "username", "status", "roles_mask", "force_logout" };

			UserDataRow userData;

			if (email != null) {
				email = validateEmailAddress(email);

				// attempt to look up the account information using the specified email address
				userData = this.getUserDataByEmailAddress(
					email,
					columnsToFetch
				);
			}
			else if (username != null) {
				username = Php.trim(username);

				// attempt to look up the account information using the specified username
				userData = this.getUserDataByUsername(
					username,
					columnsToFetch
				);
			}
			// if neither an email address nor a username has been provided
			else {
				// we can"t do anything here because the method call has been invalid
				throw new EmailOrUsernameRequiredError();
			}

			password = validatePassword(password);

			if (Php.password_verify(password, userData.password)) {
				// if the password needs to be re-hashed to keep up with improving password cracking techniques
				if (Php.password_needs_rehash(userData.password)) {
					// create a new hash from the password and update it in the database
					this.updatePasswordInternal(userData.id, password);
				}

				if (userData.verified ) {
					if ((!(onBeforeSuccess is object)) || onBeforeSuccess(userData.id)) {
						this.onLoginSuccessful(userData.id, userData.email, userData.username, userData.status, userData.roles_mask, userData.force_logout, false);

						if (rememberDuration != null) {
							this.createRememberDirective(userData.id, rememberDuration);
						}

						return;
					}
					else {
						this.throttle(new[] { "attemptToLogin", this.getIpAddress() }, 4, (60 * 60), 5, false);

						if (!string.IsNullOrEmpty(email)) {
							this.throttle(new[] { "attemptToLogin", "email", email }, 500, (60 * 60 * 24), simulated:false);
						}
						else if(!string.IsNullOrEmpty(username)) {
							this.throttle(new[] { "attemptToLogin", "username", username }, 500, (60 * 60 * 24), simulated:false);
						}

						throw new AttemptCancelledException();
					}
				}
				else {
					throw new EmailNotVerifiedException();
				}
			}
			else {
				this.throttle(new[] { "attemptToLogin", this.getIpAddress() }, 4, (60 * 60), 5, false);

				if (!string.IsNullOrEmpty(email)) {
					this.throttle(new[] { "attemptToLogin", "email", email }, 500, (60 * 60 * 24), simulated:false);
				}
				else if(!string.IsNullOrEmpty(username)) {
					this.throttle(new[] { "attemptToLogin", "username", username }, 500, (60 * 60 * 24), simulated:false);
				}

				// we cannot authenticate the user due to the password being wrong
				throw new InvalidPasswordException();
			}
		}

		/**
		 * Returns the requested user data for the account with the specified email address (if any)
		 *
		 * You must never pass untrusted input to the parameter that takes the column list
		 *
		 * @param string email the email address to look for
		 * @param array requestedColumns the columns to request from the user"s record
		 * @return array the user data (if an account was found)
		 * @throws InvalidEmailException if the email address could not be found
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private UserDataRow getUserDataByEmailAddress(string email, string[] requestedColumns) {
			DatabaseResultRow userData;
			try {
				var projection = Php.implode(", ", requestedColumns);
				userData = this.db.selectRow(
					"SELECT "+projection+ " FROM "+ this.makeTableName("users")+ " WHERE email = @email",
					new BindValues { { "@email", email } }
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (!Php.empty(userData)) {
				return new UserDataRow(userData);
			}
			else {
				throw new InvalidEmailException();
			}
		}

		/**
		 * Returns the number of open requests for a password reset by the specified user
		 *
		 * @param int userId the ID of the user to check the requests for
		 * @return int the number of open requests for a password reset
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private int getOpenPasswordResetRequests(int userId) {
			try {
				var requests = this.db.selectValue(
					"SELECT COUNT(*) FROM "+ this.makeTableName("users_resets")+ " WHERE \"user\" = @user AND \"expires\" > @expires",
					new BindValues
					{
						{"@user", userId },
						{"@expires", Php.time() }
					}
				);

				if (requests is int iRequests) {
					return iRequests;
				}
				else {
					return 0;
				}
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}
		}

		public delegate void DgtSendPasswordResetInfoToUser(string selector, string token);

		/**
		 * Creates a new password reset request
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to proceed to the second step of the password reset, both pieces will be required again
		 *
		 * @param int userId the ID of the user who requested the reset
		 * @param int expiresAfter the interval in seconds after which the request should expire
		 * @param callable callback the void that sends the password reset information to the user
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		private void createPasswordResetRequest(int userId, int expiresAfter, DgtSendPasswordResetInfoToUser callback) {
			string selector = createRandomString(20);
			string token = createRandomString(20);
			string tokenHashed = Php.password_hash(token, Php.PASSWORD_ALGO.PASSWORD_DEFAULT);
			int expiresAt = Php.time() + expiresAfter;

			try {
				this.db.insert(
					this.makeTableNameComponents_("users_resets"),
					new Dictionary<string, object>
					{
						{"user" , userId},
						{"selector" , selector},
						{"token" , tokenHashed},
						{"expires" , expiresAt }
                    }
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (Php.is_callable(callback)) {
				callback(selector, token);
			}
			else 
			{
				throw new MissingCallbackError();
			}
		}

		/**
		 * Resets the password for a particular account by supplying the correct selector/token pair
		 *
		 * The selector/token pair must have been generated previously by calling {@see forgotPassword}
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @param string newPassword the new password to set for the account
		 * @return string[] an array with the user"s ID at index `id` and the user"s email address at index `email`
		 * @throws InvalidSelectorTokenPairException if either the selector or the token was not correct
		 * @throws TokenExpiredException if the token has already expired
		 * @throws ResetDisabledException if the user has explicitly disabled password resets for their account
		 * @throws InvalidPasswordException if the new password was invalid
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see forgotPassword
		 * @see canResetPasswordOrThrow
		 * @see canResetPassword
		 * @see resetPasswordAndSignIn
		 */
		public IdAndEmail resetPassword(string selector, string token, string newPassword)
		{
			this.throttle(new[] { "resetPassword", this.getIpAddress() }, 5, (60 * 60), 10);
			this.throttle(new[] { "resetPassword", "selector", selector }, 3, (60 * 60), 10);
			this.throttle(new[] { "resetPassword", "token", token }, 3, (60 * 60), 10);

			DatabaseResultRow resetData;

			try
			{
				resetData = this.db.selectRow(
					"SELECT a.id, a.user, a.token, a.expires, b.email, b.resettable FROM " + this.makeTableName("users_resets") +
					" AS a JOIN " + this.makeTableName("users") + " AS b ON b.id = a.user WHERE a.selector = @selector",
					new BindValues { { "@selector", selector } }
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (!Php.empty(resetData)) {
				if (Delight.Shim.MasterCaster.GetInt(resetData["resettable"]) == 1) {
					if (Php.password_verify(token, Shim.MasterCaster.GetString(resetData["token"]))) {
						if ((Delight.Shim.MasterCaster.GetInt(resetData["expires"])) >= Php.time()) {
							newPassword = validatePassword(newPassword);
							this.updatePasswordInternal(Delight.Shim.MasterCaster.GetInt(resetData["user"]), newPassword);
							this.forceLogoutForUserById(Delight.Shim.MasterCaster.GetInt(resetData["user"]));

							try {
								this.db.delete(
									this.makeTableNameComponents_("users_resets"),
									new Dictionary<string, object>
									{
										{ "id" , resetData["id"] }
									}
								);
							}
							catch (Exception e) {
								throw new DatabaseError(e.Message);
							}

							return new IdAndEmail
							{
								id = Delight.Shim.MasterCaster.GetInt(resetData["user"]),
								email = Delight.Shim.MasterCaster.GetString(resetData["email"])
							};
						}
					else {
							throw new TokenExpiredException();
						}
					}
				else {
						throw new InvalidSelectorTokenPairException();
					}
				}
				else {
					throw new ResetDisabledException();
				}
			}
			else {
				throw new InvalidSelectorTokenPairException();
			}
		}

		/**
		 * Resets the password for a particular account by supplying the correct selector/token pair
		 *
		 * The selector/token pair must have been generated previously by calling {@see forgotPassword}
		 *
		 * The user will be automatically signed in if this operation is successful
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @param string newPassword the new password to set for the account
		 * @param int|null rememberDuration (optional) the duration in seconds to keep the user logged in ("remember me"), e.g. `60 * 60 * 24 * 365.25` for one year
		 * @return string[] an array with the user"s ID at index `id` and the user"s email address at index `email`
		 * @throws InvalidSelectorTokenPairException if either the selector or the token was not correct
		 * @throws TokenExpiredException if the token has already expired
		 * @throws ResetDisabledException if the user has explicitly disabled password resets for their account
		 * @throws InvalidPasswordException if the new password was invalid
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see forgotPassword
		 * @see canResetPasswordOrThrow
		 * @see canResetPassword
		 * @see resetPassword
		 */
		public class IdAndEmail
		{
			public int id;
			public string email;
		}

		public IdAndEmail resetPasswordAndSignIn(string selector, string token, string newPassword, int? rememberDuration = null)
		{
			var idAndEmail = this.resetPassword(selector, token, newPassword);
			UserDataRow userData;

			if (!this.isLoggedIn()) {
				idAndEmail.email = validateEmailAddress(idAndEmail.email);

				userData = this.getUserDataByEmailAddress(
					idAndEmail.email,
					new string[] { "username", "status", "roles_mask", "force_logout" }
				);

				this.onLoginSuccessful(idAndEmail.id, idAndEmail.email, userData.username, userData.status, userData.roles_mask, userData.force_logout, true);

				if (rememberDuration != null) {
					this.createRememberDirective(idAndEmail.id, rememberDuration);
				}
			}

			return idAndEmail;
		}

		/**
		 * Check if the supplied selector/token pair can be used to reset a password
		 *
		 * The password can be reset using the supplied information if this method does *not* throw any exception
		 *
		 * The selector/token pair must have been generated previously by calling {@see forgotPassword}
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @throws InvalidSelectorTokenPairException if either the selector or the token was not correct
		 * @throws TokenExpiredException if the token has already expired
		 * @throws ResetDisabledException if the user has explicitly disabled password resets for their account
		 * @throws TooManyRequestsException if the number of allowed attempts/requests has been exceeded
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see forgotPassword
		 * @see canResetPassword
		 * @see resetPassword
		 * @see resetPasswordAndSignIn
		 */
		public void canResetPasswordOrThrow(string selector, string token) {
			try {
				// pass an invalid password intentionally to force an expected error
				this.resetPassword(selector, token, null);

				// we should already be in one of the `catch` blocks now so this is not expected
				throw new AuthError();
			}
			// if the password is the only thing that"s invalid
			catch (InvalidPasswordException) {
				// the password can be reset
			}
			// if some other things failed (as well)
			catch (AuthException e) {
				// re-throw the exception
				throw e;
			}
		}

		/**
		 * Check if the supplied selector/token pair can be used to reset a password
		 *
		 * The selector/token pair must have been generated previously by calling {@see forgotPassword}
		 *
		 * @param string selector the selector from the selector/token pair
		 * @param string token the token from the selector/token pair
		 * @return bool whether the password can be reset using the supplied information
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see forgotPassword
		 * @see canResetPasswordOrThrow
		 * @see resetPassword
		 * @see resetPasswordAndSignIn
		 */
		public bool canResetPassword(string selector, string token) {
			try {
				this.canResetPasswordOrThrow(selector, token);

				return true;
			}
			catch (AuthException) {
				return false;
			}
		}

		/**
		 * Sets whether password resets should be permitted for the account of the currently signed-in user
		 *
		 * @param bool enabled whether password resets should be enabled for the user"s account
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public void setPasswordResetEnabled(bool enabled) {

			if (this.isLoggedIn()) {
				try {
					this.db.update(
						this.makeTableNameComponents_("users"),
						new Dictionary<string, object>
						{
							{ "resettable" , enabled ? 1 : 0 }
						},
						new Dictionary<string, object>
						{
							{ "id" , this.getUserId() }
						}
					);
				}
				catch (Exception e) {
					throw new DatabaseError(e.Message);
				}
			}
			else {
				throw new NotLoggedInException();
			}
		}

		/**
		 * Returns whether password resets are permitted for the account of the currently signed-in user
		 *
		 * @return bool
		 * @throws NotLoggedInException if the user is not currently signed in
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public bool isPasswordResetEnabled()
		{

			object enabled;

			if (this.isLoggedIn()) {
				try {
					enabled = this.db.selectValue(
						"SELECT resettable FROM "+ this.makeTableName("users")+ " WHERE id = @id",
						new BindValues
						{
							{"@id", this.getUserId() }
						}
					);

					return Delight.Shim.MasterCaster.GetInt(enabled) == 1;
				}
				catch (Exception e) {
					throw new DatabaseError(e.Message);
				}
			}
			else {
				throw new NotLoggedInException();
			}
		}

		/**
		 * Returns whether the user is currently logged in by reading from the session
		 *
		 * @return boolean whether the user is logged in or not
		 */
		[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
		public bool isLoggedIn() {

			var b = _SESSION?.SESSION_FIELD_LOGGED_IN;
			if (b == true)
				return true;

			return false;
		}

		/**
		 * Shorthand/alias for ´isLoggedIn()´
		 *
		 * @return boolean
		 */
		public bool check() {
			return this.isLoggedIn();
		}

		/**
		 * Returns the currently signed-in user"s ID by reading from the session
		 *
		 * @return int the user ID
		 */
		public int getUserId() 
			=> _SESSION.SESSION_FIELD_USER_ID ?? -1;

		/**
		 * Shorthand/alias for {@see getUserId}
		 *
		 * @return int
		 */
		public int id() {
			return this.getUserId();
		}

		/**
		 * Returns the currently signed-in user"s email address by reading from the session
		 *
		 * @return string the email address
		 */
		public string getEmail() {
			return _SESSION?.SESSION_FIELD_EMAIL;
		}

		/**
		 * Returns the currently signed-in user"s display name by reading from the session
		 *
		 * @return string the display name
		 */
		public string getUsername()
			=> _SESSION.SESSION_FIELD_USERNAME;

		/**
		 * Returns the currently signed-in user"s status by reading from the session
		 *
		 * @return int the status as one of the constants from the {@see Status} class
		 */
		public Status getStatus() 
			=> _SESSION.SESSION_FIELD_STATUS?? Status.Undefined;

		/**
		 * Returns whether the currently signed-in user is in "normal" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isNormal() {
			return this.getStatus() == Status.NORMAL;
		}

		/**
		 * Returns whether the currently signed-in user is in "archived" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isArchived() {
			return this.getStatus() == Status.ARCHIVED;
		}

		/**
		 * Returns whether the currently signed-in user is in "banned" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isBanned() {
			return this.getStatus() == Status.BANNED;
		}

		/**
		 * Returns whether the currently signed-in user is in "locked" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isLocked() {
			return this.getStatus() == Status.LOCKED;
		}

		/**
		 * Returns whether the currently signed-in user is in "pending review" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isPendingReview() {
			return this.getStatus() == Status.PENDING_REVIEW;
		}

		/**
		 * Returns whether the currently signed-in user is in "suspended" state
		 *
		 * @return bool
		 *
		 * @see Status
		 * @see Auth::getStatus
		 */
		public bool isSuspended() {
			return this.getStatus() == Status.SUSPENDED;
		}

		/**
		 * Returns whether the currently signed-in user has the specified role
		 *
		 * @param Roles role the role as one of the constants from the {@see Role} class
		 * @return bool
		 *
		 * @see Role
		 */
		public bool hasRole(Roles role) {

			if (_SESSION?.SESSION_FIELD_ROLES is Roles R)
			{ 
				return
				(
					(
						R & role
					)
					== role
				);
			}
			else {
				return false;
			}
		}

		/**
		 * Returns whether the currently signed-in user has *any* of the specified roles
		 *
		 * @param int[] ...roles the roles as constants from the {@see Role} class
		 * @return bool
		 *
		 * @see Role
		 */
		public bool hasAnyRole(Roles[] roles) {
			foreach (var role in roles) {
				if (this.hasRole(role)) {
					return true;
				}
			}

			return false;
		}

		/**
		 * Returns whether the currently signed-in user has *all* of the specified roles
		 *
		 * @param int[] ...roles the roles as constants from the {@see Role} class
		 * @return bool
		 *
		 * @see Role
		 */
		public bool hasAllRoles(Roles[] roles) {
			foreach (var role in roles) {
				if (!this.hasRole(role)) {
					return false;
				}
			}

			return true;
		}

		/**
		 * Returns an array of the user"s roles, mapping the numerical values to their descriptive names
		 *
		 * @return array
		 */
		public Dictionary<Roles, string> getRoles() {
			return Php.array_filter(
				Role.getMap(),
				(r) => this.hasRole(r), // [this, "hasRole"],
				Php.ARRAY_FILTER_USE_KEY.x
			);
		}

		/**
		 * Returns whether the currently signed-in user has been remembered by a long-lived cookie
		 *
		 * @return bool whether they have been remembered
		 */
		public bool isRemembered()
			=> _SESSION?.SESSION_FIELD_REMEMBERED ?? false;

		/**
		 * Returns the user"s current IP address
		 *
		 * @return string the IP address (IPv4 or IPv6)
		 */
		public string getIpAddress() {
			return this.ipAddress;
		}

		/**
		 * Performs throttling or rate limiting using the token bucket algorithm (inverse leaky bucket algorithm)
		 *
		 * @param array criteria the individual criteria that together describe the resource that is being throttled
		 * @param int supply the number of units to provide per interval (>= 1)
		 * @param int interval the interval (in seconds) for which the supply is provided (>= 5)
		 * @param int|null burstiness (optional) the permitted degree of variation or unevenness during peaks (>= 1)
		 * @param bool|null simulated (optional) whether to simulate a dry run instead of actually consuming the requested units
		 * @param int|null cost (optional) the number of units to request (>= 1)
		 * @param bool|null force (optional) whether to apply throttling locally (with this call) even when throttling has been disabled globally (on the instance, via the constructor option)
		 * @return float the number of units remaining from the supply
		 * @throws TooManyRequestsException if the actual demand has exceeded the designated supply
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		public float throttle(string[] criteria, int supply, int interval, int burstiness = 1, bool simulated = false, int cost = 1, bool force = false) {

			if (!this.throttling && !force) {
				return supply;
			}

			// generate a unique key for the bucket (consisting of 44 or fewer ASCII characters)
			var key = Base64.encodeUrlSafeWithoutPadding(
				Php.hash(Php.HASH_ALGO.sha256,
					Php.implode("\n", criteria),
					true
				)
			);

			var now = Php.time();

			// determine the volume of the bucket
			var capacity = burstiness * supply;

			// calculate the rate at which the bucket is refilled (per second)
			double bandwidthPerSecond = ((double)supply) / ((double)interval);

			DatabaseResultRow bucket=null;

			string query_str = "SELECT tokens, replenished_at FROM " + this.makeTableName("users_throttling") + " WHERE bucket = @bucket";
			var query_params = new BindValues { { "@bucket", key } };

			try
			{
				bucket = this.db.selectRow(
					query_str,
					query_params
				);
				//} catch (Exception e) { throw new DatabaseError(e.Message); }
			} catch(Exception e)
			{
				Console.WriteLine(e.ToString());
				Console.WriteLine("Query: " + query_str);
				Console.WriteLine("Params:" + query_params.Serialize());
			}

			if (bucket == null) {
				bucket = new DatabaseResultRow();
			}

			// initialize the number of tokens in the bucket
			bucket["tokens"] = (bucket.TryGetValue("tokens", out object o_token) 
				? Shim.MasterCaster.GetFloat(o_token) 
				: (float)capacity);
			// initialize the last time that the bucket has been refilled (as a Unix timestamp in seconds)
			bucket["replenished_at"] = (bucket.TryGetValue("replenished_at", out object o_repl_at) 
				? Delight.Shim.MasterCaster.GetInt(o_repl_at)
				: now);

			// replenish the bucket as appropriate
			var secondsSinceLastReplenishment = Php.max(0, now - Shim.MasterCaster.GetInt(bucket["replenished_at"]));
			var tokensToAdd = secondsSinceLastReplenishment * bandwidthPerSecond;
			bucket["tokens"] = Php.fmin((double)capacity, Shim.MasterCaster.GetDbl(bucket["tokens"]) + tokensToAdd);
			bucket["replenished_at"] = now;

			var accepted = (Shim.MasterCaster.GetFloat(bucket["tokens"])) >= cost;

			if (!simulated) {
				if (accepted) {
					// remove the requested number of tokens from the bucket
					bucket["tokens"] = Php.max(0, (Shim.MasterCaster.GetFloat(bucket["tokens"]) - (float)cost));
				}

				// set the earliest time after which the bucket *may* be deleted (as a Unix timestamp in seconds)

				bucket["expires_at"] = now + Php.floor(((double)capacity) / bandwidthPerSecond * 2.0);

				int affected;

				// merge the updated bucket into the database
				//try {
					affected = this.db.update(
						this.makeTableNameComponents_("users_throttling"),
						bucket,
						new Dictionary<string, object>
						{
							{ "bucket" , key }
						}
					);
				//} catch (Exception e) { throw new DatabaseError(e.Message);	}

				if (affected == 0) {
					bucket["bucket"] = key;

					try {
						this.db.insert(
							this.makeTableNameComponents_("users_throttling"),
							bucket
						);
					}
					catch (IntegrityConstraintViolationException) { }
					catch (Exception e) {
						throw new DatabaseError(e.Message);
					}
				}
			}

			if (accepted) {
				return Shim.MasterCaster.GetFloat(bucket["tokens"]);
			}
			else {
				var tokensMissing = ((float)cost - Shim.MasterCaster.GetFloat(bucket["tokens"]));
				var estimatedWaitingTimeSeconds = Php.ceil(((double)tokensMissing) / bandwidthPerSecond);

				throw new TooManyRequestsException("", estimatedWaitingTimeSeconds, null, null, null, key, string.Join("; ", criteria));
			}
		}

		/**
		 * Returns the component that can be used for administrative tasks
		 *
		 * You must offer access to this interface to authorized users only (restricted via your own access control)
		 *
		 * @return Administration
		 */
		public Administration admin()
		{
			return new Administration(this.db, this.dbTablePrefix, this.dbSchema, 
				PhpInstance);
		}

		/**
		 * Creates a UUID v4 as per RFC 4122
		 *
		 * The UUID contains 128 bits of data (where 122 are random), i.e. 36 characters
		 *
		 * @return string the UUID
		 * @author Jack @ Stack Overflow
		 */
		public static void createUuid() => System.Guid.NewGuid(); /*{
		var data = openssl_random_pseudo_bytes(16);

		// set the version to 0100
		data[6] = \chr(\ord(data[6]) & 0x0f | 0x40);
		// set bits 6-7 to 10
		data[8] = \chr(\ord(data[8]) & 0x3f | 0x80);

		return \vsprintf("%s%s-%s-%s-%s-%s%s%s", \str_split(\bin2hex(data), 4));
	}*/

		/**
		 * Generates a unique cookie name for the given descriptor based on the supplied seed
		 *
		 * @param string descriptor a short label describing the purpose of the cookie, e.g. "session"
		 * @param string|null seed (optional) the data to deterministically generate the name from
		 * @return string
		 */
		public static string createCookieName(string descriptor, string seed = null) {
			// use the supplied seed or the current UNIX time in seconds
			seed = (!string.IsNullOrEmpty(seed)) ? seed : Php.time().ToString();

			foreach (var cookiePrefix in COOKIE_PREFIXES) {
				// if the seed contains a certain cookie prefix
				if (Php.strpos(seed, cookiePrefix) == 0) {
					// prepend the same prefix to the descriptor
					descriptor = cookiePrefix + descriptor;
				}
			}

			// generate a unique token based on the name(space) of this library and on the seed
			var token = Base64.encodeUrlSafeWithoutPadding(
			Php.md5(
					Php.__NAMESPACE__ + "\n" + seed,
					true
				)
			);

			return descriptor + "_" + token;
		}

		/**
		 * Generates a unique cookie name for the "remember me" feature
		 *
		 * @param string|null sessionName (optional) the session name that the output should be based on
		 * @return string
		 */
		public string createRememberCookieName(string sessionName = null) {
			return createCookieName(
				"remember",
				(sessionName != null) ? sessionName : PhpInstance.session_name()
			);
		}

		/**
		 * Returns the selector of a potential locally existing remember directive
		 *
		 * @return string|null
		 */
		private string getRememberDirectiveSelector() {
			if (Php.isset(_COOKIE, this.rememberCookieName)) {
				var selectorAndToken = Php.explode(COOKIE_CONTENT_SEPARATOR, _COOKIE.Get(this.rememberCookieName).getValue(), 2);
				return selectorAndToken[0];
			}
			else {
				return null;
			}
		}

		/**
		 * Returns the expiry date of a potential locally existing remember directive
		 *
		 * @return int|null
		 */
		private int getRememberDirectiveExpiry() {
			// if the user is currently signed in
			if (this.isLoggedIn()) {
				// determine the selector of any currently existing remember directive
				var existingSelector = this.getRememberDirectiveSelector();

				// if there is currently a remember directive whose selector we have just retrieved
				if (!string.IsNullOrEmpty(existingSelector)) {
					// fetch the expiry date for the given selector
					var existingExpiry = this.db.selectValue(
						"SELECT expires FROM " + this.makeTableName("users_remembered") + " WHERE selector = @selector AND user = @user",
						new BindValues
						{
							{ "@selector", existingSelector },
							{ "@user", this.getUserId() }
						}
					);

					// if an expiration date has been found
					if (existingExpiry is object) { // If no results, db.selectValue will return null
						// return the date
						return Shim.MasterCaster.GetInt(existingExpiry);
					}
				}
			}

			return -1;
		}

	}
}
