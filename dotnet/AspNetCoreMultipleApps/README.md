# Multiple ASP.NET Core Applications, single process.

Assumptions

- You want to host multiple ASP.NET Core web applications in a single process but
  ensuring each web application is isolated from each other.
- You don't want to spawn and manage multiple processes.

This is a follow on from AspNetCoreNested apps that worked for ASP.NET Core 2.1.
In that version we leveraged `StartupLoader` and `ApplicationBuilder` to
construct our nested applications behind a convenient `IsolatedMap` extension
method and host multiple ASP.NET Core web applications with a single listener.

However, while `StartupLoader` was public class, it was in an
`Microsoft.AspNetCore.Hosting.Internal` namespace so using it had some long term
support risk. And so, in 3.1, the class was made internal.

But since it's all open source we can just grab a copy of that code and continue
as we were? Yes we can. However, in longer run, this adds a maintenance burden
as you need to port fixes. As it's clearly not a supported scenario so if you
have any problems, you may not get support.

In this solution we are going to take another approach where we are going to
host the each web application using standard mechanisms (i.e. using kestrel) in
a single process space with the addition of a reverse proxy in front of them.
This way we can still have the appearance of a "single application" from a
browser's perspective and a single deployable process from an operations
perspective.

However, various defaults in AspNetCore will assume that it is the host and
entry point. There are a number of things we need to do to make our ASP.NET Core
web applications more "library" like so they can be composed and hosted.

- Consider where how static content is discovered depending on hosting scenario
  (`dotnet run`/F5 vs Tests(ncrunch) vs `dotnet publish`)
- Ensure each ASP.NET Core web application doesn't discover controllers /
  services / etc from other web applications and register things it shouldn't
  know about.
- Use typed settings for configuration and only use `IConfiguration` in the `MainHost`.
- Any Security considerations.

## Implementation Notes

1. `MainHost` is the project that hosts is own web application as well as
   `WebApplication1` and `WebApplication2`.

1. `MainHost` proxies `/app1` and `/app2` to  `WebApplication1` and
   `WebApplication2` respectively.

1. Each WebApplication has it's own `Settings` class. This is used to inject
   settings into each WebApplication's `Startup`. There is no need to leverage
   `IConfiguration` at these surfaces.

1. Each WebApplication is hosted and run as a HostedService which manages their
   lifecycle with respect to `MainHost`.

1. The port numbers the WebApplication's are bind to are choosen by the OS. This
   removes any possibility of start up exceptions attempting to bind to a used
   port number. Since these aren't known until after the listener is running,
   `HostedServiceContext` is used to capture these values which is then
   subsequently used in the proxy.

1. Static content ("WebRoot") poses a particular challenge. When F5 debugging,
   or if you run `dotnet run` on the MainHost, the `ContentRootPath` is set to
   the MainHost directory. `wwwroot` folder is **not** copied as part of a build
   but by default, the WebApplication will look for
   `{MainHostDirecotry}/wwwroot` which will not contain the correct static content.

   The issue also applies after a `dotnet publish` after which the static
   content _is_ copied but put in a special folder -
   `wwwroot/_content/{WebApplication}` (At time of writing, I don't yet know the
   basis of this convention)

   To solve the above there is a `DetectWebRoot()` method that tests some paths
   to check which WebRoot path to use. It first tests for the WebApplication
   project directory which will exist with `dotnet run` / F5. Failing that,
   it'll test for `wwwroot/_content/{WebApplication}` which will exist after
   `dotnet publish`. There are some strings hand coded here and I feel, overall,
   that the approach is not bullet-proof (maybe I'm missing something...). If
   there are improvements that can be applied here, please reach out.

1. In tests, we detect if running in NCrunch and `SetContentRoot()` of the
   MainHost by checking an environment variable. It's not ideal that I have to
   add test runner specific code but that is the trade off as that is how
   NCrunch works. If there are improvements that can be applied here, please
   reach out.

1. Since this application opens multiple ports (and despite only on loopback),
   an eager Systems Administrator / Operator will still want assurance that
   these ports will not represent a potential issue (i.e. a 'backdoor' that
   another process can invoke).

   As an additional layer of security is added in that a special header,
   `SharedSettings.PreSharedKeyHeader` that will contain a Pre-Shared Key
   generated at startup, is sent to the hosted Web Applications. They then check
   every incoming request for this and if missing or incorrect, they will
   simply refused to service it by calling `Abort()`.

### Other notes

- There has been some "grapevine" talk of decoupling kestrel from the the
  underlying socket implementations which may allow this to be re-implemented as
  entirely in-memory.

- Microsoft's TestHost would be a candidate of a pure in-memory implementation
  however my experience with it over the years has had it's bugs. Currently it's
  not HTTP spec compliant with respect to `Content-Length` and
  `Transfer-Encoding` header.
