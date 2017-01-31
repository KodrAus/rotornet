# `rotornet`

Messing around with the `libuv` bindings hidden away in [`kestrel`](https://github.com/aspnet/KestrelHttpServer).

This is a hobby project to build a state-machine based io abstraction for .NET based off the Rust library [`rotor`](https://github.com/tailhook/rotor). It's a learning project to explore event-based asynchrony, and stops short of manual memory management. The idea is to use F# discriminated unions to make it easy to write stateful io handlers without using the TPL.

Kestrel provides a couple of layers for working with libuv:

- A native binding layer, that maps libuv events to C# delegates
- A thread construct, that encapsulates its own loop and provides a mechanism for registering io and callbacks etc

The goal is to rework the thread layer to make it easy to build io abstractions with F#. This will be based off `rotor` and provide similar capabilities. The execution of FSMs within the loop will be synchronous, which makes it safe to mutate memory. How this memory is mutated is a question that needs to be explored. Where possible, we'll want to avoid heap allocations within the framework itself.

F# should be used down to the lowest layer possible. One possible point of contention could be executing many small managed callbacks from unmanaged code. For wakeups, we can get around this by handling all wakeup requests in a single callback, but socket
events are possibly a different story.

# The result

There were some challenges with the .NET Core tooling not working very well with F# on Linux and I couldn't get any of the major test runners working. The code is also not very idiomatic F#. Besides that, there are a few significant points in this library that would need to be designed properly before it could be fleshed out further:

- Managing concurrency
- Managing memory

The experiment was useful for investigating the way `Kestrel` manages asynchronous io efficiently. The next step is to look specifically at sharing memory with unmanaged code, which is being done in [this repo](https://github.com/KodrAus/csharp-rust).

## Managing concurrency

When the loop is started on its own thread the caller ends up polling a simple channel to see if it's ready or not. This is kind of lazy and leaky. There are plenty of projects out there for managing concurrent execution, like `hopac` and figuring out how they fit a public API would be essential.

The machine loop itself runs all handlers sequentially on the same thread, which is fine. The current implementation could be used for managing concurrency in a single-threaded environment. But primitives for interacting with that thread from the outside would be helpful.

## Managing memory

This is the sticking point where the current implementation stops. `Kestrel` has an internal memory pool it uses to share with `libuv`. This implementation has been abstracted into a first-class API in `CoreFX Lab`, that gives us the basic building blocks for describing memory independently of whether it's managed by the CLR or not.

How memory is represented in this library should utilise those building blocks.

# What's there?

## `Rotor.Libuv`

The native binding to `libuv`.

Pretty much lifted straight from `kestrel` with a few additions (like `UvTimerHandle`). I've also kep the memory pool for when I start getting some actual io working.

## `Rotor.Fsm`

The base API wrapping the `libuv` bindings.

It allows you to construct a state machine, add it to a loop and either wake it up with a message or let it time out
and perform some action asynchronously.

Right now I can't get `xUnit` working with F# on .NET Core, so I have a bunch of 'integration test'
samples you can run. Each targets a specific scenario anyway so they're not bad samples.
To run:

```sh
cd samples && ./test.sh
```
