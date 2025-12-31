using System;
using System.Runtime.CompilerServices;

namespace Refactor.Pooling
{
    public sealed class Pool<T, TPolicy>
        where T : class
        where TPolicy : struct, IPoolPolicy<T>
    {
        private readonly T[] _storage;
        private readonly TPolicy _policy;
        private int _ptr;
        
#if DEVELOPMENT_BUILD
        private int _rejectedCount;
        
        /// <summary>
        /// 被拒绝归还的对象数量.
        /// </summary>
        public int RejectedCount => _rejectedCount;
#endif
        
        /// <summary>
        /// 创建池
        /// </summary>
        /// <param name="policy">池策略</param>
        /// <param name="capacity">池容量（固定）</param>
        public Pool(TPolicy policy, int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            
            _storage = new T[capacity];
            _policy = policy;
            _ptr = 0;
        }
        
        /// <summary>
        /// 租借对象
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            if (_ptr > 0)
            {
                var obj = _storage[--_ptr];
                _storage[_ptr] = null;
                _policy.OnRent(obj);
                return obj;
            }
            
            var newObj = _policy.Create();
            _policy.OnRent(newObj);
            return newObj;
        }
        
        /// <summary>
        /// 归还对象
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T obj)
        {
            if (obj == null) return;
            var accepted = _policy.OnReturn(obj);
            if (_ptr < _storage.Length && accepted)
            {
                _storage[_ptr++] = obj;
            }
#if DEVELOPMENT_BUILD
            else
            {
                _rejectedCount++;
            }
#endif
        }
        
        /// <summary>
        /// 租借对象（RAII 模式）
        /// </summary>
        public PooledScope<T, TPolicy> RentScoped()
        {
            return new PooledScope<T, TPolicy>(this, Rent());
        }
        
        /// <summary>
        /// 预热池（预先创建对象）
        /// </summary>
        public void Prewarm(int count)
        {
            for (var i = 0; i < count && _ptr < _storage.Length; i++)
            {
                var obj = _policy.Create();
                if (_policy.OnReturn(obj))
                {
                    _storage[_ptr++] = obj;
                }
            }
        }
        
        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            for (var i = 0; i < _ptr; i++)
            {
                _storage[i] = null;
            }
            _ptr = 0;
        }
    }
}