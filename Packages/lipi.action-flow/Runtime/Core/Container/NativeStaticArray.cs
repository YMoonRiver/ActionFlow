﻿using UnityEngine;
using System.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Entities;
using System.Collections.Generic;

namespace ActionFlow
{
    /// <summary>
    /// 存储NativeStaticArrayHead描述的数据数组。主要就是存储在runtime才能确定数据类型（struct)和长度的数据
    /// </summary>
    public unsafe struct NativeStaticArray : IDisposable
    {
        public static NativeStaticArray Create(ref NativeStaticArrayHead head, int size)
        {
            var array = new NativeStaticArray
            {
                head = head,
                Capacity = size,
                data = (byte*)UnsafeUtility.Malloc(head.Size * size, 4, Allocator.Persistent),
                _idle = new NativeQueue<int>(Allocator.Persistent)
            };
            return array;
        }

        private NativeStaticArrayHead head;
        private byte* data;
        public int Count { get; private set; }
        public int Capacity { get; private set; }

        private NativeQueue<int> _idle; //闲置的队列

        public NativeStaticArrayItem this[int i]
        {
            get
            {
                Debug.Assert(i < Count, "Index overflow");
                return new NativeStaticArrayItem()
                {
                    data = data + head.Size * i,
                    head = head
                };
            }
        }

        public int NewItem()
        {
            if (_idle.Count > 0)
            {
                var index = _idle.Dequeue();
                UnsafeUtility.MemClear(data + head.Size * index, head.Size);
                return index;
            }

            Count += 1;
            if (Capacity < Count)
            {
                var newData = (byte*)UnsafeUtility.Malloc(head.Size * (Count + Capacity), 4, Allocator.Persistent);
                UnsafeUtility.MemCpy(newData, data, Capacity * head.Size);
                Capacity = Count + Capacity;
                UnsafeUtility.Free(data, Allocator.Persistent);
                data = newData;
            }
            return Count - 1;
        }

        public void RemoveItem(int index)
        {
            Debug.Assert(index < Count, "Index overflow");
            _idle.Enqueue(index);
        }

        public void Dispose()
        {
            head.Dispose();
            _idle.Dispose();
            UnsafeUtility.Free(data, Allocator.Persistent);
        }

    }

    public unsafe struct NativeStaticArrayItem
    {
        internal byte* data;
        internal NativeStaticArrayHead head;

        public T Get<T>(int i) where T : struct
        {
            var pos = head[i];
            UnsafeUtility.CopyPtrToStructure<T>(data + pos.Offset, out var v);
            return v;
        }

        public void Set<T>(int i, T value) where T : struct
        {
            var pos = head[i];
            UnsafeUtility.CopyStructureToPtr<T>(ref value, data + pos.Offset);
        }
    }
}
