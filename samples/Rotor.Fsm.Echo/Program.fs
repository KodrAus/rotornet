open System
open System.Threading
open Rotor.Fsm.Base
open Rotor.Fsm.Sock

//States of our machine:
// - Waiting for a connection
// - Accepting a connection
// - Reading from a connection
// - Writing to a connection
// - Closing a connection

type Server() =
    inherit ISocketMachine<unit>()

    override this.idle c s =
        raise (NotImplementedException("check for connection"))

    override this.read c s =
        raise (NotImplementedException("check for connection"))
    
    override this.write c s =
        raise (NotImplementedException("check for connection"))
    
    override this.wakeup c s =
        raise (NotImplementedException("check for connection"))
    
    override this.timeout c s =
        raise (NotImplementedException("check for connection"))

[<EntryPoint>]
let main argv = 
    let l = (loop ())
    l.addMachine (fun scope -> new Socket<_>(Server()))
    l |> run