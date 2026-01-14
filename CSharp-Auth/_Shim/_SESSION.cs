using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Shim
{
	public class _SESSION : BasicDictionaryWrapped<string, object>
	{
		/** @var string session field for whether the client is currently signed in */
		private const string K_SESSION_FIELD_LOGGED_IN = "auth_logged_in";
		/** @var string session field for the ID of the user who is currently signed in (if any) */
		private const string K_SESSION_FIELD_USER_ID = "auth_user_id";
		/** @var string session field for the email address of the user who is currently signed in (if any) */
		private const string K_SESSION_FIELD_EMAIL = "auth_email";
		/** @var string session field for the display name (if any) of the user who is currently signed in (if any) */
		private const string K_SESSION_FIELD_USERNAME = "auth_username";
		/** @var string session field for the status of the user who is currently signed in (if any) as one of the constants from the {@see Status} class */
		private const string K_SESSION_FIELD_STATUS = "auth_status";
		/** @var string session field for the roles of the user who is currently signed in (if any) as a bitmask using constants from the {@see Role} class */
		private const string K_SESSION_FIELD_ROLES = "auth_roles";
		/** @var string session field for whether the user who is currently signed in (if any) has been remembered (instead of them having authenticated actively) */
		private const string K_SESSION_FIELD_REMEMBERED = "auth_remembered";
		/** @var string session field for the UNIX timestamp in seconds of the session data's last resynchronization with its authoritative source in the database */
		private const string K_SESSION_FIELD_LAST_RESYNC = "auth_last_resync";
		/** @var string session field for the counter that keeps track of forced logouts that need to be performed in the current session */
		private const string K_SESSION_FIELD_FORCE_LOGOUT = "auth_force_logout";

		/** @var string session field for whether the client is currently signed in */
		public bool? SESSION_FIELD_LOGGED_IN { get => (bool?)this[K_SESSION_FIELD_LOGGED_IN]; set => this[K_SESSION_FIELD_LOGGED_IN] = value; }
		/** @var string session field for the ID of the user who is currently signed in (if any) */
		public int? SESSION_FIELD_USER_ID { get => (int?)this[K_SESSION_FIELD_USER_ID]; set => this[K_SESSION_FIELD_USER_ID] = value; }
		/** @var string session field for the email address of the user who is currently signed in (if any) */
		public string SESSION_FIELD_EMAIL { get => (string)this[K_SESSION_FIELD_EMAIL]; set => this[K_SESSION_FIELD_EMAIL] = value; }
		/** @var string session field for the display name (if any) of the user who is currently signed in (if any) */
		public string SESSION_FIELD_USERNAME { get => (string)this[K_SESSION_FIELD_USERNAME]; set => this[K_SESSION_FIELD_USERNAME] = value; }
		/** @var string session field for the status of the user who is currently signed in (if any) as one of the const stringants from the {@see Status} class */
		public CSharpAuth.Auth.Status? SESSION_FIELD_STATUS { get => (CSharpAuth.Auth.Status?)this[K_SESSION_FIELD_STATUS]; set => this[K_SESSION_FIELD_STATUS] = value; }
		/** @var string session field for the roles of the user who is currently signed in (if any) as a bitmask using const stringants from the {@see Role} class */
		public CSharpAuth.Auth.Roles? SESSION_FIELD_ROLES { get => (CSharpAuth.Auth.Roles?)this[K_SESSION_FIELD_ROLES]; set => this[K_SESSION_FIELD_ROLES] = value; }
		/** @var string session field for whether the user who is currently signed in (if any) has been remembered (instead of them having authenticated actively) */
		public bool? SESSION_FIELD_REMEMBERED { get => (bool?)this[K_SESSION_FIELD_REMEMBERED]; set => this[K_SESSION_FIELD_REMEMBERED] = value; }
		/** @var string session field for the UNIX timestamp in seconds of the session data"s last resynchronization with its authoritative source in the database */
		public int? SESSION_FIELD_LAST_RESYNC { get => (int?)this[K_SESSION_FIELD_LAST_RESYNC]; set => this[K_SESSION_FIELD_LAST_RESYNC] = value; }
		/** @var string session field for the counter that keeps track of forced logouts that need to be performed in the current session */
		public int? SESSION_FIELD_FORCE_LOGOUT { get => (int?)this[K_SESSION_FIELD_FORCE_LOGOUT]; set => this[K_SESSION_FIELD_FORCE_LOGOUT] = value; }

		public _SESSION() : base() { }

		private const string DefaultSessionName = "PHPSESSID";
		public string _SessionName = DefaultSessionName;

		public void Clear()
		{
			Dict.Clear();
		}

		public void session_destroy()
		{
			// Not sure what to do here
			Dict.Clear();
			_SessionName = DefaultSessionName;
		}

		public void session_regenerate_id(bool deleteOldSession)
		{
			// TODO
		}


		public class CookieParams
		{
			public int lifetime; // - The lifetime of the cookie in seconds.
			public string path; // - The path where information is stored.
			public string domain; // - The domain of the cookie.
			public bool secure; // - The cookie should only be sent over secure connections.
			public bool httponly; // - The cookie can only be accessed through the HTTP protocol.
			public string samesite; // - Controls the cross-domain sending of the cookie.
		}

		public CookieParams session_get_cookie_params()
		{
			return new CookieParams
			{
				lifetime = 0,
				path = "",
				domain = "",
				secure = false,
				httponly = false,
				samesite = ""
			};
		}
	}
}
