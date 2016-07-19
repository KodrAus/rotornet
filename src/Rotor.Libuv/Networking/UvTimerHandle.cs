// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Rotor.Libuv.Infrastructure;

namespace Rotor.Libuv.Networking
{
    public class UvTimerHandle : UvHandle
    {
        private static readonly Binding.uv_close_cb _destroyMemory = (handle) => DestroyMemory(handle);

        private static readonly Binding.uv_timer_cb _uv_timer_cb = (handle) => TimerCb(handle);
        private Action _callback;
        private Action<Action<IntPtr>, IntPtr> _queueCloseHandle;

        public UvTimerHandle() : base()
        {
        }

        public void Init(UvLoopHandle loop, Action callback, Action<Action<IntPtr>, IntPtr> queueCloseHandle)
        {
            CreateMemory(
                loop.Binding,
                loop.ThreadId,
                loop.Binding.handle_size(Binding.HandleType.TIMER));

            _callback = callback;
            _queueCloseHandle = queueCloseHandle;
            _uv.timer_init(loop, this);
        }

        public void Start(ulong timeout, ulong repeat)
        {
            _uv.timer_start(this, _uv_timer_cb, timeout, repeat);
        }

        public void Stop()
        {
            _uv.timer_stop(this);
        }

        public void Again()
        {
            _uv.timer_again(this);
        }

        public void SetRepeat(ulong repeat)
        {
            _uv.timer_set_repeat(this, repeat);
        }

        //TODO: GetRepeat

        unsafe private static void TimerCb(IntPtr handle)
        {
            FromIntPtr<UvTimerHandle>(handle)._callback.Invoke();
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
                    Debug.Assert(false, "UvTimerHandle not initialized with queueCloseHandle action");
                    return false;
                }
            }
            return true;
        }
    }
}
