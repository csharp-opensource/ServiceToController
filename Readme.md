# Install

```bash
Install-Package ServiceToController
# or
dotnet add package ServiceToController
```

# Use

```c#
using ServiceToController;
...
...
...

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        ...
        ...
        var builder = services.AddControllersWithViews().AddControllersAsServices();
        builder.AddCastedService<MyService>();
    }
    ...
...
...
}


public class MyService
{
    public Task<MyObject> GetAll(MyObject obj)
    {
        return Task.FromResult(obj);
    }
}

public class MyObject
{
    public string Name { get; set; }
}
```
