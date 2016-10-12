open System
open System.Threading
open Rotor.Fsm.Base

//A shared context (not accessed concurrently though)
type Context = {
    mutable listener: Option<Notifier>
}

type Machine (id) =
    inherit IMachine<Context>()

    override this.create c s =
        match c.listener with
        | Some l -> printfn "%i: ping" id
                    l.wakeup()
                    Done

        | None ->   c.listener <- Some(s.notifier())
                    Response.Deadline(1000UL)

    override this.wakeup c s =
        printfn "%i: pong" id
        Done

    override this.timeout c s =
        Response.Error("timed out before msg")

[<EntryPoint>]
let main argv =
    let l = loop { listener = None }

    l.addMachine (fun scope -> new Machine(1))
    l.addMachine (fun scope -> new Machine(2))
    
    l |> run