<p align="center">
<img src="https://user-images.githubusercontent.com/65511890/155548059-d8aed5b1-b576-4c1b-8370-8d0eb6b9141a.png" width="50%"/>
</p>

# Auth

**Authentication for PHP, but actually for C#.**

Port of PHP-Auth for C#. Everyone says "don't roll your own authentication library", so here we are.

Currently only supports Postgresql, the Shim.PDO wrapper should be easy enough to replicate for other databases. Uses Npgsql for the Postgresql connection.

It isn't directly integrated with a webserver. Cookies, session, and headers are loosely emulated through `_COOKIE`, `_SESSION`, and `_SERVER` objects.
Importantly _*these are per Auth object*_ so session data etc. won't be carried over between Auth objects (unless you do it manually).

## Simple usage

```
// Establish database connection
var PDO = new CSharpAuth.Shim.PDO("Host=<server IP here>;Database=<database name here>",
          "<my_postgresql_username>",
          "<my_postgresql_password>");

// Create an Auth-friendly wrapper object
var DB = new CSharpAuth.Db.PdoDatabase(PDO);
// Create an Auth object
var Auth = new CSharpAuth.Auth.Auth(DB);
      
// If you want to set any cookies for the Auth session, use the Auth._COOKIE object.

try
{
  // Auth.login is the same as the Auth::login method in PHP-Auth
  // If it fails, it will throw an Exception
  Auth.login(username, password);
  
  Console.WriteLine("Logged in successfully!");
        
  // If you want to access any cookies that may have been set by Auth, use the Auth._COOKIE object.
}
catch (CSharpAuth.Auth.InvalidEmailException)
{
  Console.WriteLine("Bad username/password");
}
catch (CSharpAuth.Auth.InvalidPasswordException)
{
  Console.WriteLine("Bad username/password");
}
catch (Exception exc)
{
  Console.WriteLine("Error: " + exc.Message);
}

PDO.Disconnect();
```

In practice, you'll be required to explicitly pass around SESSION, SERVER, and COOKIE objects all the goshdarn time. These are available as fields in the Auth object: Auth.\_SESSION, Auth.\_SERVER, and Auth.\_COOKIE.

The best approach is to use Auth.AuthBuilder to establish a suitable environment. You can then daisychain its various methods to set up (as a minimum) the database connection details, the database table, and the client's remote IP address. For example:

```

using CSharpAuth.Auth;

Auth auth = Auth.Create()
	// Database info:
	.SetDatabaseParams("127.0.0.1", 5432, "PostgresUser", "MyPostgresPassword123")
	.SetDatabaseName("db")
	// Request-related info:
	.SetClientIp(request_defined_elsewhere.Context.IpAddress)
	// ... could customize the cookie manager etc. here ...
	// Done - build
	.Build();

```

If you are using an ASP.NET server, you can ease the process by also installing the CSharp-Auth-AspNet extension package and using it in the build query. This will automatically sync up Auth's cookies with the server request's cookies (which is important).

```

using CSharpAuth.Auth;
using CSharpAuth.Shim.AspNetCore;

Auth auth = Auth.Create()
	// Database info:
	.SetDatabaseParams("127.0.0.1", 5432, "PostgresUser", "MyPostgresPassword123")
	.SetDatabaseName("db")
	// Request-related info:
	.SetEnvironment(myAspNetCoreHttpContext)
	// Done - build
	.Build();

```


## License

This project is licensed under the terms of the [MIT License](https://opensource.org/licenses/MIT).
