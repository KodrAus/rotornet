open System
open System.Threading
open Rotor.Fsm.Base
open Rotor.Fsm.Sock

//A machine with a mutable internal state
type Machine() =
    inherit ISocketMachine<unit>()

    override this.ready c s =
        Intent.Sleep

    override this.read c s =
        Intent.Sleep

    override this.write c s =
        Intent.Sleep

    override this.wakeup c s =
        Intent.Sleep

    override this.timeout c s =
        Intent.Sleep

[<EntryPoint>]
let main argv =
    let l = (loop ())
    l.addMachine (fun scope -> new Socket<_>(new Machine()))
    l |> run |> ignore
    0