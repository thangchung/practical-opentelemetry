using System;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Shared.Redis
{
    public class RedisStore
    {
        private static Lazy<ConnectionMultiplexer> _lazyConnection = null;

        public RedisStore(IOptions<RedisOptions> redisOptions)
        {
            var configurationOptions = new ConfigurationOptions
            {
                EndPoints =
                {
                    redisOptions.Value.Connection
                },
                Password = redisOptions.Value.Password
            };

            _lazyConnection =
                new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(configurationOptions));
        }

        public ConnectionMultiplexer Connection => _lazyConnection?.Value ?? throw new Exception("Couldn't connect to Redis server.");

        public IDatabase RedisCache => Connection.GetDatabase();
    }
}
