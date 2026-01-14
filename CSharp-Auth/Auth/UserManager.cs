using System;
using System.Collections.Generic;
using System.Text;
using CSharpAuth.Db;
using CSharpAuth.Shim;

/*
 * Based on PHP-Auth (https://github.com/delight-im/PHP-Auth)
 * Copyright (c) Delight.im (https://www.delight.im/)
 * Licensed under the MIT License (https://opensource.org/licenses/MIT)
 */

namespace CSharpAuth.Auth 
{


	/**
	 * Abstract base class for components implementing user management
	 *
	 * @internal
	 */
	abstract public class UserManager : IDisposable {

		public delegate void DgtConfirmationEmail(string selector, string token);

		public PhpInstance PhpInstance;
		public _COOKIE _COOKIE => PhpInstance._COOKIE;
		public _SESSION _SESSION => PhpInstance._SESSION;
		public _SERVER _SERVER => PhpInstance._SERVER;


		/** @var PdoDatabase the database connection to operate on */
		public PdoDatabase db { get; protected set; }
  		public bool MustDisposePdoDatabase = false;
		/** @var string|null the schema name for all database tables used by this component */
		protected string dbSchema;
		/** @var string the prefix for the names of all database tables used by this component */
		protected string dbTablePrefix;

		/**
		 * Creates a random string with the given maximum length
		 *
		 * With the default parameter, the output should contain at least as much randomness as a UUID
		 *
		 * @param int maxLength the maximum length of the output string (integer multiple of 4)
		 * @return string the new random string
		 */
		public static string createRandomString(int maxLength = 24) {
			// calculate how many bytes of randomness we need for the specified string length
			var bytes = Php.floor(maxLength / 4.0) * 3;

			// get random data
			var data = Php.openssl_random_pseudo_bytes(bytes);

			// return the Base64-encoded result
			return Base64.encodeUrlSafe(data);
		}

		/**
		 * @param PdoDatabase|PdoDsn|\PDO databaseConnection the database connection to operate on
		 * @param string|null dbTablePrefix (optional) the prefix for the names of all database tables used by this component
		 * @param string|null dbSchema (optional) the schema name for all database tables used by this component
		 */
		protected UserManager(PdoDatabase _databaseConnection, string _dbTablePrefix, string _dbSchema,
			PhpInstance _phpInstance)
			//Shim._COOKIE cookieShim, Shim._SESSION sessionShim, Shim._SERVER serverShim)
			//: base(cookieShim, sessionShim, serverShim)
		{
			PhpInstance = _phpInstance;

			/*if (databaseConnection instanceof PdoDatabase) {
				this.db = databaseConnection;
			}
			else if (databaseConnection instanceof PdoDsn) {
				this.db = PdoDatabase::fromDsn(databaseConnection);
			}
			else if (databaseConnection instanceof \PDO) {
				this.db = PdoDatabase::fromPdo(databaseConnection, true);
			}
			else {
				this.db = null;

				throw new \InvalidArgumentException("The database connection must be an instance of either `PdoDatabase`, `PdoDsn` or `PDO`");
			}*/
			db = _databaseConnection;

			this.dbSchema = _dbSchema;
			this.dbTablePrefix = _dbTablePrefix;
		}

		/**
		 * Creates a new user
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
		 * @param bool requireUniqueUsername whether it must be ensured that the username is unique
		 * @param string email the email address to register
		 * @param string password the password for the new account
		 * @param string|null username (optional) the username that will be displayed
		 * @param callable|null callback (optional) the void that sends the confirmation email to the user
		 * @return int the ID of the user that has been created (if any)
		 * @throws InvalidEmailException if the email address has been invalid
		 * @throws InvalidPasswordException if the password has been invalid
		 * @throws UserAlreadyExistsException if a user with the specified email address already exists
		 * @throws DuplicateUsernameException if it was specified that the username must be unique while it was *not*
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 *
		 * @see confirmEmail
		 * @see confirmEmailAndSignIn
		 */
		protected int createUserInternal(bool requireUniqueUsername, string email, string password, string username = null, 
			DgtConfirmationEmail callback = null)
		{
			Php.ignore_user_abort(true);

			email = validateEmailAddress(email);
			password = validatePassword(password);

			username = !string.IsNullOrEmpty(username) ? Php.trim(username) : null;

			// if the supplied username is the empty string or has consisted of whitespace only
			if (username == "") {
				// this actually means that there is no username
				username = null;
			}

			// if the uniqueness of the username is to be ensured
			if (requireUniqueUsername) {
				// if a username has actually been provided
				if (username != null) {
					// count the number of users who do already have that specified username
					var occurrencesOfUsername = Shim.MasterCaster.GetInt(this.db.selectValue(
						"SELECT COUNT(*) FROM " + this.makeTableName("users") + " WHERE username = @username",
						new BindValues { { "@username", username } }
					));

					// if any user with that username does already exist
					if (occurrencesOfUsername > 0) {
						// cancel the operation and report the violation of this requirement
						throw new DuplicateUsernameException();
					}
				}
			}

			password = Php.password_hash(password, Php.PASSWORD_ALGO.PASSWORD_DEFAULT);
			var verified = Php.is_callable(callback) ? 0 : 1;

			int newUserId = -1;
			try {
				this.db.insert(
					this.makeTableNameComponents_("users"),

					new Dictionary<string, object>
					{
						{ "email" , email },
						{ "password" , password },
						{ "username" , username },
						{ "verified" , verified },
						{ "registered" , Php.time() }
					}
					, "id", out object objNewUserId
				);
				newUserId = Shim.MasterCaster.GetInt(objNewUserId);
			}
			// if we have a duplicate entry
			catch (IntegrityConstraintViolationException) {
				throw new UserAlreadyExistsException();
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			//int newUserId = this.db.getLastInsertId();

			if (verified == 0) {
				this.createConfirmationRequest(newUserId, email, callback);
			}

			return newUserId;
		}

		/**
		 * Updates the given user"s password by setting it to the new specified password
		 *
		 * @param int userId the ID of the user whose password should be updated
		 * @param string newPassword the new password
		 * @throws UnknownIdException if no user with the specified ID has been found
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		protected void updatePasswordInternal(int userId, string newPassword) {
			newPassword = Php.password_hash(newPassword, Php.PASSWORD_ALGO.PASSWORD_DEFAULT);

			try {
				var affected = this.db.update(
					this.makeTableNameComponents_("users"),
					
					new Dictionary<string, object> { { "password" , newPassword } },

					new Dictionary<string, object> { { "id", userId } }
				);

				if (affected == 0) {
					throw new UnknownIdException();
				}
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}
		}

		/**
		 * Called when a user has successfully logged in
		 *
		 * This may happen via the standard login, via the "remember me" feature, or due to impersonation by administrators
		 *
		 * @param int userId the ID of the user
		 * @param string email the email address of the user
		 * @param string username the display name (if any) of the user
		 * @param int status the status of the user as one of the constants from the {@see Status} class
		 * @param Roles roles the roles of the user as a bitmask using constants from the {@see Role} class
		 * @param int forceLogout the counter that keeps track of forced logouts that need to be performed in the current session
		 * @param bool remembered whether the user has been remembered (instead of them having authenticated actively)
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		protected virtual void onLoginSuccessful(int userId, string email, string username, Status status, Roles roles, int forceLogout, bool remembered) {
			// re-generate the session ID to prevent session fixation attacks (requests a cookie to be written on the client)
			CSharpAuth.Cookie.Session.regenerate(PhpInstance, true);

			// save the user data in the session variables maintained by this library
			_SESSION.SESSION_FIELD_LOGGED_IN = true;
			_SESSION.SESSION_FIELD_USER_ID = userId;
			_SESSION.SESSION_FIELD_EMAIL = email;
			_SESSION.SESSION_FIELD_USERNAME = username;
			_SESSION.SESSION_FIELD_STATUS = status;
			_SESSION.SESSION_FIELD_ROLES = roles;
			_SESSION.SESSION_FIELD_FORCE_LOGOUT = forceLogout;
			_SESSION.SESSION_FIELD_REMEMBERED = remembered;
			_SESSION.SESSION_FIELD_LAST_RESYNC = Php.time();
		}

		/**
		 * Returns the requested user data for the account with the specified username (if any)
		 *
		 * You must never pass untrusted input to the parameter that takes the column list
		 *
		 * @param string username the username to look for
		 * @param array requestedColumns the columns to request from the user"s record
		 * @return array the user data (if an account was found unambiguously)
		 * @throws UnknownUsernameException if no user with the specified username has been found
		 * @throws AmbiguousUsernameException if multiple users with the specified username have been found
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		protected UserDataRow getUserDataByUsername(string username, string[] requestedColumns) {
			List<DatabaseResultRow> users=null;
			try {
				var projection = Php.implode(", ", requestedColumns);

				users = this.db.select(
					"SELECT " + projection + " FROM " + this.makeTableName("users") + " WHERE username = @username LIMIT 2 OFFSET 0",
					new BindValues
					{{"@username", username } }
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (Php.empty(users)) {
				throw new UnknownUsernameException();
			}
			else {
				if (Php.count(users) == 1) {
					return new UserDataRow(users[0]);
				}
			else {
					throw new AmbiguousUsernameException();
				}
			}
		}

		/**
		 * Validates an email address
		 *
		 * @param string email the email address to validate
		 * @return string the sanitized email address
		 * @throws InvalidEmailException if the email address has been invalid
		 */
		protected static string validateEmailAddress(string email) {
			if (string.IsNullOrEmpty(email))
				throw new InvalidEmailException();

			email = Php.trim(email);

			if (!Php.filter_var(email, Php.FILTER.FILTER_VALIDATE_EMAIL)) {
				throw new InvalidEmailException();
			}

			return email;
		}

		/**
		 * Validates a password
		 *
		 * @param string password the password to validate
		 * @return string the sanitized password
		 * @throws InvalidPasswordException if the password has been invalid
		 */
		protected static string validatePassword(string password) {
			if (string.IsNullOrEmpty(password)) {
				throw new InvalidPasswordException();
			}

			password = Php.trim(password);

			if (Php.strlen(password) < 1) {
				throw new InvalidPasswordException();
			}

			return password;
		}

		/**
		 * Creates a request for email confirmation
		 *
		 * The callback void must have the following signature:
		 *
		 * `function (selector, token)`
		 *
		 * Both pieces of information must be sent to the user, usually embedded in a link
		 *
		 * When the user wants to verify their email address as a next step, both pieces will be required again
		 *
		 * @param int userId the user"s ID
		 * @param string email the email address to verify
		 * @param callable callback the void that sends the confirmation email to the user
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		protected void createConfirmationRequest(int userId, string email, DgtConfirmationEmail callback) {
			var selector = createRandomString(16);
			var token = createRandomString(16);
			var tokenHashed = Php.password_hash(token, Php.PASSWORD_ALGO.PASSWORD_DEFAULT);
			var expires = Php.time() + 60 * 60 * 24;

			try {
				this.db.insert(
					this.makeTableNameComponents_("users_confirmations"),
					new Dictionary<string, object>
					{
						{ "user_id" , userId },
						{ "email" , email },
						{ "selector" , selector },
						{ "token" , tokenHashed },
						{ "expires" , expires }
					}
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}

			if (Php.is_callable(callback)) {
				callback(selector, token);
			}
		else {
				throw new MissingCallbackError();
			}
		}

		/**
		 * Clears an existing directive that keeps the user logged in ("remember me")
		 *
		 * @param int userId the ID of the user who shouldn"t be kept signed in anymore
		 * @param string selector (optional) the selector which the deletion should be restricted to
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		virtual protected void deleteRememberDirectiveForUserById(int userId, string selector = null) {
			var whereMappings = new Dictionary<string, object>();

			if (!string.IsNullOrEmpty(selector)) {
				whereMappings.Add("selector",selector);
			}

			whereMappings.Add("user",userId);

			try {
				this.db.delete(
					this.makeTableNameComponents_("users_remembered"),
					whereMappings
				);
			}
			catch (Exception e) {
				throw new DatabaseError(e.Message);
			}
		}

		/**
		 * Triggers a forced logout in all sessions that belong to the specified user
		 *
		 * @param int userId the ID of the user to sign out
		 * @throws AuthError if an internal problem occurred (do *not* catch)
		 */
		protected void forceLogoutForUserById(int userId) {
			this.deleteRememberDirectiveForUserById(userId);
			this.db.exec(Database.ExecType.NonQuery,
				"UPDATE " + this.makeTableName("users") + " SET force_logout = force_logout + 1 WHERE id = @id",
				new BindValues { { "@id", userId } }
			);
		}

		/**
		 * Builds a (qualified) full table name from an optional qualifier, an optional prefix, and the table name itself
		 *
		 * The optional qualifier may be a database name or a schema name, for example
		 *
		 * @param string name the name of the table
		 * @return string[] the components of the (qualified) full name of the table
		 */
		protected string[] makeTableNameComponents(string name) {
			var components = new List<string>();

			if (!Php.empty(this.dbSchema)) {
				components.Add(this.dbSchema);
			}

			if (!Php.empty(name)) {
				if (!Php.empty(this.dbTablePrefix)) {
					components.Add(this.dbTablePrefix + name);
				}
				else {
					components.Add(name);
				}
			}

			return components.ToArray();
		}
		protected string makeTableNameComponents_(string name)
			=> string.Join(".", makeTableNameComponents(name));

		/**
		 * Builds a (qualified) full table name from an optional qualifier, an optional prefix, and the table name itself
		 *
		 * The optional qualifier may be a database name or a schema name, for example
		 *
		 * @param string name the name of the table
		 * @return string the (qualified) full name of the table
		 */
		protected string makeTableName(string name) {
			var components = this.makeTableNameComponents(name);

			return Php.implode(".", components);
		}



  		protected bool disposedValue=false;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if(MustDisposePdoDatabase && (db is object))
					{
						db.Dispose();
						db = null;
					}
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

	}
}
