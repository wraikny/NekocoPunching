open System
open Altseed2
open Altseed2.BoxUI

let shouldTrue msg cond =
  if not cond then failwith msg

[<EntryPoint>]
let main _ =

  Engine.Initialize("NekocoPunching", 800, 600)
  |> shouldTrue "Failed to initialize the Engine"

  while Engine.DoEvents() do
    BoxUISystem.Update()
    Engine.Update |> ignore

  BoxUISystem.Terminate()
  Engine.Terminate()

  0
