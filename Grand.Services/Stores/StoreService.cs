using Grand.Core.Caching;
using Grand.Core.Data;
using Grand.Core.Domain.Stores;
using Grand.Services.Events;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Grand.Services.Stores
{
    /// <summary>
    /// Store service
    /// </summary>
    public partial class StoreService : IStoreService
    {
        #region Constants

        /// <summary>
        /// Key for caching
        /// </summary>
        private const string STORES_ALL_KEY = "Grand.stores.all";
        /// <summary>
        /// Key for caching
        /// </summary>
        /// <remarks>
        /// {0} : store ID
        /// </remarks>
        private const string STORES_BY_ID_KEY = "Grand.stores.id-{0}";

        #endregion

        #region Fields

        private readonly IRepository<Store> _storeRepository;
        private readonly IMediator _mediator;
        private readonly ICacheManager _cacheManagerLevel1;
        private readonly ICacheManager _cacheManagerLevel2;

        private List<Store> _allStores;

        #endregion

        #region Ctor

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="cacheManager">Cache manager</param>
        /// <param name="storeRepository">Store repository</param>
        /// <param name="mediator">Mediator</param>
        public StoreService(IEnumerable<ICacheManager> cacheManager,
            IRepository<Store> storeRepository,
            IMediator mediator)
        {
            _cacheManagerLevel1 = cacheManager.First(o => o.GetType() == typeof(MemoryCacheManager));
            _cacheManagerLevel2 = cacheManager.FirstOrDefault(o => o.GetType() == typeof(DistributedRedisCache));

            _storeRepository = storeRepository;
            _mediator = mediator;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Deletes a store
        /// </summary>
        /// <param name="store">Store</param>
        public virtual async Task DeleteStore(Store store)
        {
            if (store == null)
                throw new ArgumentNullException("store");

            var allStores = await GetAllStores();
            if (allStores.Count == 1)
                throw new Exception("You cannot delete the only configured store");

            await _storeRepository.DeleteAsync(store);

            //clear cache
            await _cacheManagerLevel1.Clear();
            if (_cacheManagerLevel2 != null)
            {
                await _cacheManagerLevel2.Clear();
            }
            //event notification
            await _mediator.EntityDeleted(store);
        }

        /// <summary>
        /// Gets all stores
        /// </summary>
        /// <returns>Stores</returns>
        public virtual async Task<IList<Store>> GetAllStores()
        {
            if (_allStores == null)
            {
                string key = STORES_ALL_KEY;
                _allStores = await _cacheManagerLevel1.GetAsync(key, async () =>
                {
                    var res = await _storeRepository.Collection.FindAsync(new BsonDocument());
                    var aux = await res.ToListAsync();

                    return aux.OrderBy(x => x.DisplayOrder).ToList();
                });
            }
            return _allStores;
        }

        /// <summary>
        /// Gets a store 
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Store</returns>
        public virtual Task<Store> GetStoreById(string storeId)
        {
            string key = string.Format(STORES_BY_ID_KEY, storeId);
            return _cacheManagerLevel1.GetAsync(key, () => _storeRepository.GetByIdAsync(storeId));
        }

        /// <summary>
        /// Inserts a store
        /// </summary>
        /// <param name="store">Store</param>
        public virtual async Task InsertStore(Store store)
        {
            if (store == null)
                throw new ArgumentNullException("store");

            await _storeRepository.InsertAsync(store);

            //clear cache
            await _cacheManagerLevel1.Clear();
            if (_cacheManagerLevel2 != null)
            {
                await _cacheManagerLevel2.Clear();
            }
            //event notification
            await _mediator.EntityInserted(store);
        }

        /// <summary>
        /// Updates the store
        /// </summary>
        /// <param name="store">Store</param>
        public virtual async Task UpdateStore(Store store)
        {
            if (store == null)
                throw new ArgumentNullException("store");

            await _storeRepository.UpdateAsync(store);

            //clear cache
            await _cacheManagerLevel1.Clear();
            if (_cacheManagerLevel2 != null)
            {
                await _cacheManagerLevel2.Clear();
            }
            //event notification
            await _mediator.EntityUpdated(store);
        }
        #endregion
    }
}