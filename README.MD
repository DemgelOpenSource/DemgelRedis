Mono Build (Travis): [![Build Status](https://travis-ci.org/DemgelOpenSource/DemgelRedis.svg)](https://travis-ci.org/DemgelOpenSource/DemgelRedis)  
Windows Build (AppVeyor): [![Build status](https://ci.appveyor.com/api/projects/status/6xl602hwsyu926aw/branch/master?svg=true)](https://ci.appveyor.com/project/tenowg/demgelredis/branch/master)  
Nuget Package Build: [![demgel MyGet Build Status](https://www.myget.org/BuildSource/Badge/demgel?identifier=caa525cf-17f7-4bf4-b2c6-d5f5abf6b122)](https://www.myget.org/)

This package is no where near ready for use... For Developers only (if interested)

## Demgel Redis

### Backing Source
DemgelRedis BackingSource will backup your Redis Cache to a Backing Datasource such as Azure Tables, Azure DocumentDB, SqlServer, MySql, and will retrieve/restore these objects to the Redis Cache as needed.

### Job Queue
A Reliable Job Queue on Redis Cache, enabling Cloud/Scalable apps to process jobs and recieve messages without worring about auto scaling projects and load balancing.

### OM
DemgelRedis OM is an `Object Manager` for Redis Cache servers.

DemgelRedis OM will perform `on the fly` Redis Cache updates as values change in
either the Local or Remote copy of the object. This will update any copy of the object
that is loaded in any application this is connected to the Redis Server.

DemgelRedis OM Lazy loads all `virtual` properties, loading them only when needed.

DemgelRedis OM will handle some `Relational Operations`, such as matching lists to keys, which will also be lazy loaded.

#### Example C# POCO Object

```c#

[RedisSuffix(Key = "suffix")]
[RedisPrefix(Key = "prefix")]
public class ExampleClass : IRedisObject
{
    public ExampleClass()
    {
        SomeList = new List<RedisValue>();
    }

    [RedisIdKey]
    public string Id { get; set; }

    [RedisPrefix(Key = "listprefix")]
    public virtual IList<RedisValue> SomeList { get; set; }

    public virtual DifferentRedisObject RedisObject { get; set; }
}
```

## Future
* Have backing data servers from SqlServer, MySql to NoSql servers like Azure Table, or DocumentDB and Mongo.
* Track changes to all Objects loaded, if you don't want `on the fly` behaviour. Letting you save the object on your terms.
