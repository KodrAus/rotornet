# `rotornet`

Messing around with the `libuv` bindings hidden away in [`kestrel`](https://github.com/aspnet/KestrelHttpServer).

This is a hobby project to build a state-machine based io abstraction for .NET based off the Rust library [`rotor`](https://github.com/tailhook/rotor).
The idea is to use F# discriminated unions to make it easy to write stateful io handlers without using the TPL.

Is it a good idea? Who knows, we'll find out...

Kestrel provides a couple of layers for working with libuv:

- A native binding layer, that maps libuv events to C# delegates
- A thread construct, that encapsulates its own loop and provides a mechanism for registering io and callbacks etc

Our goal is to rework the thread layer to make it easy to build io abstractions with F#.
This will be based off `rotor` and provide similar capabilities.
The execution of FSMs within the loop will be synchronous, which makes it safe to mutate memory.
How this memory is mutated is a question that needs to be explored.
Where possible, we'll want to avoid heap allocations within the framework itself.

F# should be used down to the lowest layer possible.
One possible point of contention could be executing many small managed callbacks from unmanaged code.
For wakeups, we can get around this by handling all wakeup requests in a single callback, but socket
events are possibly a different story.
