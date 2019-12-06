﻿using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Core.Caching
{
    public partial class DistributedRedisCache : ICacheManager
    {
        private readonly IDatabase _distributedCache;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public DistributedRedisCache(string redisConnectionString)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
            _distributedCache = _connectionMultiplexer.GetDatabase(0);
        }

        public virtual async Task<T> Get<T>(string key)
        {
            //get serialized item from cache
            var serializedItem = await _distributedCache.StringGetAsync(key);
            if (string.IsNullOrEmpty(serializedItem))
                return default(T);

            //deserialize item
            var item = JsonConvert.DeserializeObject<T>(serializedItem);
            if (item == null)
                return default(T);

            return item;
        }

        public virtual async Task<(T, bool)> TryGetValueAsync<T>(string key)
        {
            var res = await _distributedCache.StringGetAsync(key);
            if (string.IsNullOrEmpty(res.ToString()))
            {
                return (default, res.HasValue);
            }
            else
            {
                return (JsonConvert.DeserializeObject<T>(res), true);
            }
        }

        public virtual async Task Remove(string key)
        {
            await _distributedCache.KeyDeleteAsync(key, CommandFlags.PreferMaster);
        }

        public virtual async Task Set(string key, object data, int cacheTime)
        {
            if (data == null)
                return;

            //serialize item
            var serializedItem = JsonConvert.SerializeObject(data);

            //and set it to cache
            await _distributedCache.StringSetAsync(key, serializedItem, TimeSpan.FromMinutes(cacheTime), When.Always, CommandFlags.FireAndForget);
        }

        public bool IsSet(string key)
        {
            return _distributedCache.KeyExists(key);
        }

        public virtual async Task Clear()
        {
            foreach (var endPoint in _connectionMultiplexer.GetEndPoints())
            {
                var server = _connectionMultiplexer.GetServer(endPoint);
                await server.FlushDatabaseAsync(0);
            }
        }


        public virtual void Dispose()
        {
            //nothing special
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            var keys = new List<RedisKey>();
            foreach (var endPoint in _connectionMultiplexer.GetEndPoints())
            {
                var server = _connectionMultiplexer.GetServer(endPoint);
                keys.AddRange(server.Keys(_distributedCache.Database, $"*{pattern}*"));
            }
            await _distributedCache.KeyDeleteAsync(keys.Distinct().ToArray());
        }

        public Task RemoveByPattern(string pattern)
        {
            return RemoveByPatternAsync(pattern);
        }
    }
}
