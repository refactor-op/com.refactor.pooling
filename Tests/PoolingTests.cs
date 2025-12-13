using System.Collections.Generic;
using NUnit.Framework;
using Refactor.Pooling;

namespace Refactor.Pooling.Tests
{
    public class PoolingTests
    {
        public class MyObject
        {
            public int Value;
            public bool IsReset;
        }

        public struct MyObjectPolicy : IPoolPolicy<MyObject>
        {
            public MyObject Create() => new MyObject();

            public void OnRent(MyObject obj)
            {
                obj.IsReset = false;
            }

            public bool OnReturn(MyObject obj)
            {
                obj.Value = 0;
                obj.IsReset = true;
                return true;
            }
        }

        [Test]
        public void Pool_BasicLifecycle_WorksCorrectly()
        {
            // Arrange
            var pool = Pools.Create<MyObject, MyObjectPolicy>(new MyObjectPolicy(), 10);

            // Act - Rent
            var obj1 = pool.Rent();
            obj1.Value = 42;

            // Assert
            Assert.IsNotNull(obj1);
            Assert.AreEqual(42, obj1.Value);

            // Act - Return
            pool.Return(obj1);

            // Act - Rent again
            var obj2 = pool.Rent();

            // Assert
            Assert.AreSame(obj1, obj2, "Should be reused");
            Assert.AreEqual(0, obj2.Value, "Should be reset");
            Assert.IsFalse(obj2.IsReset, "OnRent sets IsReset = false");
        }

        [Test]
        public void Pool_CapacityLimit_Works()
        {
            // Arrange
            var pool = Pools.Create<MyObject, MyObjectPolicy>(new MyObjectPolicy(), 1);

            // Act
            var obj1 = pool.Rent();
            var obj2 = pool.Rent();

            pool.Return(obj1);
            pool.Return(obj2);

            // Assert
#if DEVELOPMENT_BUILD
            Assert.AreEqual(1, pool.RejectedCount);
#endif
        }

        [Test]
        public void ListPool_SharedInstance_Works()
        {
            // Act
            var list = ListPool<int>.Rent();
            list.Add(1);
            list.Add(2);

            Assert.AreEqual(2, list.Count);

            ListPool<int>.Return(list);

            var list2 = ListPool<int>.Rent();
            Assert.AreEqual(0, list2.Count, "List should be cleared on return");
            Assert.AreSame(list, list2, "Should reuse list");
            ListPool<int>.Return(list2);
        }

        [Test]
        public void Scoped_Rent_AutoReturns()
        {
            // Arrange
            var pool = Pools.Create<MyObject, MyObjectPolicy>(new MyObjectPolicy(), 10);
            MyObject capturedObj = null;

            // Act
            using (var scope = pool.RentScoped())
            {
                capturedObj = scope.Value;
                Assert.IsNotNull(capturedObj);
            }

            // Assert
            // 在不访问内部结构的情况下很难检查它是否在池中,
            // 但我们可以检查下一次租借是否返回它.
            var nextObj = pool.Rent();
            Assert.AreSame(capturedObj, nextObj);
        }
        
        [Test]
        public void Prewarm_PopulatesPool()
        {
             // Arrange
            var pool = Pools.Create<MyObject, MyObjectPolicy>(new MyObjectPolicy(), 10);
            
            // Act
            pool.Prewarm(5);
            
            // Assert
            var objs = new List<MyObject>();
            for(int i=0; i<5; i++)
            {
                objs.Add(pool.Rent());
            }
            
            foreach(var obj in objs)
            {
                Assert.IsNotNull(obj);
            }
        }
    }
}
