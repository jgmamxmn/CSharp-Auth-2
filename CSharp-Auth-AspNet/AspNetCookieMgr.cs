using Microsoft.AspNetCore.Http;

namespace Delight.Shim.AspNetCore
{
	public class AspNetCookieMgr : Delight.Shim._COOKIE
	{
		private Microsoft.AspNetCore.Http.HttpRequest Request;
		private Microsoft.AspNetCore.Http.HttpResponse Response;
		private PhpInstance MyPhpInstance;

		public AspNetCookieMgr(PhpInstance _PhpInstance, Microsoft.AspNetCore.Http.HttpContext Ctx) : base() { DoConstructor(_PhpInstance, Ctx.Request, Ctx.Response); }
		public AspNetCookieMgr(PhpInstance _PhpInstance, Microsoft.AspNetCore.Http.HttpRequest _Req, Microsoft.AspNetCore.Http.HttpResponse _Resp) : base() { DoConstructor(_PhpInstance, _Req, _Resp); }
		private void DoConstructor(PhpInstance _PhpInstance, Microsoft.AspNetCore.Http.HttpRequest _Req, Microsoft.AspNetCore.Http.HttpResponse _Resp)
		{
			Request = _Req;
			Response = _Resp;

			MyPhpInstance = _PhpInstance;
		}

		private bool _Initialized = false;
		private Dictionary<string, Cookie.Cookie> ShadowCopy;
		/// <summary>
		/// Ideally i'd just do this in the constructor, but there's a circularity to it, since the ShimmedFull object probably isn't fully initialized (_SESSION and _SERVER may be null, and _COOKIE will necessarily be null given that WE are the _COOKIE), so I'll just subsequently run this once before doing any read/write operation on the cookies.
		/// </summary>
		private void EnsureInitialized()
		{
			if(!_Initialized)
			{
				_Initialized = true;
				ShadowCopy = new Dictionary<string, Cookie.Cookie>();
				foreach (var CReq1 in Request.Headers["Cookie"])
				{
					foreach (var CReq2 in CReq1.Split(';'))
					{
						var CInternal = Delight.Cookie.Cookie.parse(MyPhpInstance, CReq2.Trim());
						//base[CInternal.getName()] = CInternal;
						ShadowCopy.Add(CInternal.getName(), CInternal);
					}
				}
			}
		}

		public Cookie.Cookie Get(string key)
		{
			EnsureInitialized();
			return ShadowCopy[key];
		}
		public void Set(string key, Cookie.Cookie value)
		{
			EnsureInitialized();
			if (ShadowCopy.ContainsKey(key))
				ShadowCopy.Remove(key);
			ShadowCopy.Add(key, value);
			AddCookieToHttpResponse(value);
		}

		private static SameSiteMode ParseSameSiteMode(in string phpAuthValue)
		{
			// `null`, `None`, `Lax` or `Strict`
			var Dict = new Dictionary<string, SameSiteMode>
			{
				{ "null", default(SameSiteMode) },
				{ "none", SameSiteMode.None },
				{ "lax", SameSiteMode.Lax },
				{ "strict", SameSiteMode.Strict }
			};
			if (Dict.TryGetValue(phpAuthValue.ToLower(), out SameSiteMode ret))
				return ret;
			else
				return default(SameSiteMode);
		}

		public void Unset(string key)
		{
			EnsureInitialized();
			ShadowCopy.Remove(key);
			Response.Cookies.Delete(key);
		}

		private void AddCookieToHttpResponse(Cookie.Cookie cookieEntry)
		{
			EnsureInitialized();
			Response.Cookies.Append(cookieEntry.getName(), cookieEntry.getValue(), new CookieOptions
			{
				Expires = cookieEntry.getExpiryTime(),
				Domain = cookieEntry.getDomain(),
				HttpOnly = cookieEntry.isHttpOnly(),
				MaxAge = cookieEntry.getExpiryTime() is DateTime DT ? DT - DateTime.Now : null,
				Path = cookieEntry.getPath(),
				SameSite = ParseSameSiteMode(cookieEntry.getSameSiteRestriction()),
				Secure = cookieEntry.isSecureOnly()
			});
		}

		public bool TryGetValue(string key, out Cookie.Cookie cookieEntry)
		{
			return ShadowCopy.TryGetValue(key, out cookieEntry);
		}

		public void Clear()
		{
			foreach (var toRemove in ShadowCopy)
				Response.Cookies.Delete(toRemove.Value.getName());
			ShadowCopy.Clear();
		}

		public Dictionary<string, Cookie.Cookie> GetLiveCollection()
		{
			return new Dictionary<string, Cookie.Cookie>(ShadowCopy);
		}

		public bool isset(string key)
		{
			return ShadowCopy.ContainsKey(key);
		}
	}
}