using MongoDB.Driver;
using System.Linq.Expressions;

namespace PeFi.Proxy.Persistance;

public class MongoDatastore : IDataStore
{
    private readonly MongoClient client;

    public MongoDatastore()
    {
        client = new MongoClient("mongodb://192.168.0.5:27017");
    }

    private IMongoCollection<T> GetCollection<T>(string database, string collection)
    {
        return client.GetDatabase(database).GetCollection<T>(collection);
    }

    public async Task<IEnumerable<T>> Get<T>(string database, string collection, Expression<Func<T, bool>> predicate)
    {
        var a = await GetCollection<T>(database, collection)
        .FindAsync(predicate);

        return a.ToEnumerable();
    }

    public async Task<IEnumerable<T>> Get<T>(string database, string collection) => await Get<T>(database, collection, _ => true);

    public async Task<T> Add<T>(string database, string collection, T item)
    {
        await GetCollection<T>(database, collection)
        .InsertOneAsync(item);

        return item;
    }
}
