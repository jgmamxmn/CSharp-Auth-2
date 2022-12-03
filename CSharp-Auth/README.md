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
var PDO = new Delight.Shim.PDO("Host=<server IP here>;Database=<database name here>",
          "<my_postgresql_username>",
          "<my_postgresql_password>");

// Create an Auth-friendly wrapper object
var DB = new Delight.Db.PdoDatabase(PDO);
// Create an Auth object
var Auth = new Delight.Auth.Auth(DB);
      
// If you want to set any cookies for the Auth session, use the Auth._COOKIE object.

try
{
  // Auth.login is the same as the Auth::login method in PHP-Auth
  // If it fails, it will throw an Exception
  Auth.login(username, password);
  
  Console.WriteLine("Logged in successfully!");
        
  // If you want to access any cookies that may have been set by Auth, use the Auth._COOKIE object.
}
catch (Delight.Auth.InvalidEmailException)
{
  Console.WriteLine("Bad username/password");
}
catch (Delight.Auth.InvalidPasswordException)
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

If you want to modify these values when the Auth object is created, you can use the CallbackToInitializeEnvironment parameter of Auth's constructor. For example:

```
// ... initialize PDO and DB as above example ...

var Auth = new Delight.Auth.Auth(DB,
  CallbackToInitializeEnvironment: (_auth, shimCookie, shimSession, shimServer) =>
  {
    // Copy cookies from some other source
    foreach(var ExistingCookie in CollectionOfExistingCookies)
    {
       shimCookie.set(ExistingCookie.name, ExistingCookie.value);
    }
  });
```

## License

This project is licensed under the terms of the [MIT License](https://opensource.org/licenses/MIT).
