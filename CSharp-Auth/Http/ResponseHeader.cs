using CSharpAuth.Shim;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuth.Http
{
	public class ResponseHeader 
	{
		PhpInstance PhpInstance;
		public ResponseHeader(PhpInstance phpInstance) 
		{
			PhpInstance = phpInstance;
		}

		/**
		 * Returns the header with the specified name (and optional value prefix)
		 *
		 * @param string $name the name of the header
		 * @param string $valuePrefix the optional string to match at the beginning of the header's value
		 * @return string|null the header (if found) or `null`
		 */
		public string get(string name, string valuePrefix = "")
		{
			if (Php.empty(name))
			{
				return null;
			}

			var nameLength = Php.strlen(name);
			var headers = PhpInstance.headers_list();

			foreach (var header in headers) 
			{
				if (Php.strcasecmp(Php.substr(header, 0, nameLength + 1), (name + ":")) == 0) 
				{
					var headerValue = Php.trim(Php.substr(header, nameLength + 1), "\t ");

					if (Php.empty(valuePrefix) 
						|| Php.substr(headerValue, 0, Php.strlen(valuePrefix)) == valuePrefix) 
					{
						return header;
					}
				}
			}

			return null;
		}

		/**
		 * Returns the value of the header with the specified name (and optional value prefix)
		 *
		 * @param string $name the name of the header
		 * @param string $valuePrefix the optional string to match at the beginning of the header's value
		 * @return string|null the value of the header (if found) or `null`
		 */
		public string getValue(string name, string valuePrefix = "")
		{
			var header = get(name, valuePrefix);

			if (!Php.empty(header))
			{
				var nameLength = Php.strlen(name);
				var headerValue = Php.substr(header, nameLength + 1);
				headerValue = Php.trim(headerValue, "\t ");

				return headerValue;
			}
			else
			{
				return null;
			}
		}

		/**
		 * Sets the header with the specified name and value
		 *
		 * If another header with the same name has already been set previously, that header will be overwritten
		 *
		 * @param string $name the name of the header
		 * @param string $value the corresponding value for the header
		 */
		public void set(string name, string value)
		{
			PhpInstance.header(name + ": " + value, true);
		}

		/**
		 * Adds the header with the specified name and value
		 *
		 * If another header with the same name has already been set previously, both headers (or header values) will be sent
		 *
		 * @param string $name the name of the header
		 * @param string $value the corresponding value for the header
		 */
		public void add(string name, string value)
		{
			PhpInstance.header(name + ": " + value, false);
		}

		/**
		 * Removes the header with the specified name (and optional value prefix)
		 *
		 * @param string $name the name of the header
		 * @param string $valuePrefix the optional string to match at the beginning of the header's value
		 * @return bool whether a header, as specified, has been found and removed
		 */
		public bool remove(string name, string valuePrefix = "")
		{
			return take(PhpInstance, name, valuePrefix) != null;
		}

		/**
		 * Returns and removes the header with the specified name (and optional value prefix)
		 *
		 * @param string $name the name of the header
		 * @param string $valuePrefix the optional string to match at the beginning of the header's value
		 * @return string|null the header (if found) or `null`
		 */
		public static string take(PhpInstance shim, string name, string valuePrefix = "")
		{
			if (Php.empty(name))
			{
				return null;
			}

			var nameLength = Php.strlen(name);
			var headers = shim.headers_list();

			string first = null;
			var homonyms = new List<string>();

			foreach (var header in headers) 
			{
				if (Php.strcasecmp(Php.substr(header, 0, nameLength + 1), (name + ":")) == 0) 
				{
					var headerValue = Php.trim(Php.substr(header, nameLength + 1), "\t ");

					if ((Php.empty(valuePrefix) || Php.substr(headerValue, 0, Php.strlen(valuePrefix)) == valuePrefix) && first == null) 
					{
						first = header;
					}
					else
					{
						homonyms.Add(header);
					}
				}
			}

			if (first != null) 
			{
				shim.header_remove(name);

				foreach (var homonym in homonyms)
				{
					shim.header(homonym, false);
				}
			}

			return first;
		}

		/**
		 * Returns the value of and removes the header with the specified name (and optional value prefix)
		 *
		 * @param string $name the name of the header
		 * @param string $valuePrefix the optional string to match at the beginning of the header's value
		 * @return string|null the value of the header (if found) or `null`
		 */
		public string takeValue(string name, string valuePrefix = "")
		{
			var header = take(PhpInstance, name, valuePrefix);

			if (!Php.empty(header))
			{
				var nameLength = Php.strlen(name);
				var headerValue = Php.substr(header, nameLength + 1);
				headerValue = Php.trim(headerValue, "\t ");

				return headerValue;
			}
			else
			{
				return null;
			}
		}

	}
}
