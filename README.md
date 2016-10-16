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

I'm not trying to build a competitor to `kestrel`, or even a viable framework, this is an exploration.

F# should be used down to the lowest layer possible.
One possible point of contention could be executing many small managed callbacks from unmanaged code.
For wakeups, we can get around this by handling all wakeup requests in a single callback, but socket
events are possibly a different story.

# What's there

## `Rotor.Libuv`

The native binding to `libuv`.

Pretty much lifted straight from `kestrel` with a few additions (like `UvTimerHandle`).
I've also kep the memory pool for when I start getting some actual io working.

## `Rotor.Fsm`

Our base API wrapping the `libuv` bindings.

It allows you to construct a state machine, add it to a loop and either wake it up with a message or let it time out
and perform some action asynchronously.

Right now I can't get `xUnit` working with F# on .NET Core, so I have a bunch of 'integration test'
samples you can run.
Each targets a specific scenario anyway so they're not bad samples.
To run:

```sh
cd samples && ./test.sh
```

# What else is out there?

If you did this idea, you should check out David Fowler's [Channels](https://github.com/davidfowl/Channels) library.
It's a standardisation of some of the good work in Kestrel.
