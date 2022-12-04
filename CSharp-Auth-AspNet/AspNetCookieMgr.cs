using Microsoft.AspNetCore.Http;

namespace Delight.Shim.AspNetCore
{
	public class AspNetCookieMgr : Delight.Shim._COOKIE
	{
		private Microsoft.AspNetCore.Http.HttpRequest Request;
		private Microsoft.AspNetCore.Http.HttpResponse Response;
		private Shimmed_Full ShimmedFull;

		public AspNetCookieMgr(Shimmed_Full _ShimmedFull, Microsoft.AspNetCore.Http.HttpContext Ctx) : base() { Init(_ShimmedFull, Ctx.Request, Ctx.Response); }
		public AspNetCookieMgr(Shimmed_Full _ShimmedFull, Microsoft.AspNetCore.Http.HttpRequest _Req, Microsoft.AspNetCore.Http.HttpResponse _Resp) : base() { Init(_ShimmedFull, _Req, _Resp); }
		private void Init(Shimmed_Full _ShimmedFull, Microsoft.AspNetCore.Http.HttpRequest _Req, Microsoft.AspNetCore.Http.HttpResponse _Resp)
		{
			Request = _Req;
			Response = _Resp;

			ShimmedFull = _ShimmedFull;

			foreach (var CReq1 in Request.Headers["Cookie"])
			{
				foreach (var CReq2 in CReq1.Split(';'))
				{
					var CInternal = Delight.Cookie.Cookie.parse(ShimmedFull, CReq2.Trim());
					base[CInternal.getName()] = CInternal;
				}
			}
		}
		public override Cookie.Cookie this[string key]
		{
			get => base[key];
			set
			{
				base[key] = value;
				AddCookieToHttpResponse(value);
			}
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

		public override void Set(string key, Cookie.Cookie cookieEntry)
		{
			base.Set(key, cookieEntry);
			AddCookieToHttpResponse(cookieEntry);
		}
		public override void unset(string key)
		{
			base.unset(key);
			Response.Cookies.Delete(key);
		}

		private void AddCookieToHttpResponse(Cookie.Cookie cookieEntry)
		{
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
	}
}