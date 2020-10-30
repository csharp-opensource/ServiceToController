using System.Threading.Tasks;

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

