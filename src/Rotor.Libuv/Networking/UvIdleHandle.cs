// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Rotor.Libuv.Infrastructure;

namespace Rotor.Libuv.Networking
{
    public class UvIdleHandle : UvHandle
    {
        private static readonly Binding.uv_close_cb _destroyMemory = (handle) => DestroyMemory(handle);

        private static readonly Binding.uv_idle_cb _uv_idle_cb = (handle) => IdleCb(handle);
        private Action _callback;
        private Action<Action<IntPtr>, IntPtr> _queueCloseHandle;

        public UvIdleHandle() : base()
        {
        }

        public void Init(UvLoopHandle loop, Action callback, Action<Action<IntPtr>, IntPtr> queueCloseHandle)
        {
            CreateMemory(
                loop.Binding,
                loop.ThreadId,
                loop.Binding.handle_size(Binding.HandleType.IDLE));

            _callback = callback;
            _queueCloseHandle = queueCloseHandle;
            _uv.idle_init(loop, this);
        }

        public void Start()
        {
            _uv.idle_start(this, _uv_idle_cb);
        }

        public void Stop()
        {
            ReleaseHandle();
        }

        unsafe private static void IdleCb(IntPtr handle)
        {
            FromIntPtr<UvIdleHandle>(handle)._callback.Invoke();
        }

        protected override bool ReleaseHandle()
        {
            var memory = handle;
            if (memory != IntPtr.Zero)
            {
                handle = IntPtr.Zero;

                if (Thread.CurrentThread.ManagedThreadId == ThreadId)
                {
                    _uv.close(memory, _destroyMemory);
                }
                else if (_queueCloseHandle != null)
                {
                    // This can be called from the finalizer.
                    // Ensure the closure doesn't reference "this".
                    var uv = _uv;
                    _queueCloseHandle(memory2 => uv.close(memory2, _destroyMemory), memory);
                    uv.unsafe_async_send(memory);
                }
                else
                {
                    Debug.Assert(false, "UvIdleHandle not initialized with queueCloseHandle action");
                    return false;
                }
            }
            return true;
        }
    }
}
