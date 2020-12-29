#r "netstandard"
#r "./lib/Altseed2/Altseed2.dll"
#r "nuget: FSharp.Data.LiteralProviders"

open System
open FSharp.Data.LiteralProviders
open Altseed2

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let [<Literal>] Password = Env.RESOURCEPASSWORD.Value

let [<Literal>] Target = "Resources"

let coreModules = CoreModules.File

let config =
  Configuration(
    ConsoleLoggingEnabled = true,
    EnabledCoreModules = coreModules
  )

if not <| Engine.Initialize("Pack", 1, 1, config) then
  failwith "Failed to initialize the Engine"

printfn "Packing %s to %s.pack ..." Target Target
if Engine.File.PackWithPassword (Target, $"%s{Target}.pack", Password) then
  printfn "Succcess!"
else
  failwith "Failed to pack resources"

Engine.Terminate()
