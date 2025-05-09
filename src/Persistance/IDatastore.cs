using System.Linq.Expressions;

namespace PeFi.Proxy.Persistance;

public interface IDataStore
{
    Task<T> Add<T>(string database, string collection, T item);

    Task<IEnumerable<T>> Get<T>(string database, string collection, Expression<Func<T, bool>> predicate);

    Task<IEnumerable<T>> Get<T>(string database, string collection);

}
