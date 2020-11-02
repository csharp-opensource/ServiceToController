using NSwag.Annotations;
using System.Threading.Tasks;

public class MyService
{
    public Task<MyObject> GetAll(MyObject obj)
    {
        return Task.FromResult(obj);
    }
    public MyObject GetAll(string obj)
    {
        return (new MyObject());
    }
    public bool IsNew { get; set; } = true;
}

public class MyObject
{
    public string? Name { get; set; }
}

