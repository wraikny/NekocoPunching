#r "paket:
source https://api.nuget.org/v3/index.json
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.Target
nuget FAKE.IO.Zip //"

#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"

open System
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

// let runtimes = [ "win-x64"; "osx-x64" ]
let Altseed2Dir = @"lib/Altseed2"

let dotnet cmd arg =
  let res = DotNet.exec id cmd arg
  if not res.OK then
    failwithf "Failed 'dotnet %s %s'" cmd arg

let getOutputPath = sprintf "publish/NekocoPunching.%s"

Target.initEnvironment()

Target.create "Clean" (fun _ ->
  !! "src/**/bin"
  ++ "src/**/obj"
  ++ "lib/**/bin"
  ++ "lib/**/obj"
  ++ "publish"
  |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
  !! "src/**/*.*proj"
  |> Seq.iter (DotNet.build id)
)

Target.create "Pack" (fun _ ->
  dotnet "fsi" "--exec pack.fsx"
)

let copyReadme outputPath =
  "PublishContents/README.md"
  |> Shell.copyFile outputPath

let makeLicense outputPath =
  [|
    "NekocoPunching", "LICENSE"
    ".NET Core", "PublishContents/LICENSES/dotnetcore.txt"
    "Altseed2", "lib/Altseed2/LICENSE"
    "Altseed2.BoxUI", "lib/Altseed2.BoxUI/LICENSE"
  |]
  |> Array.map (fun (libname, path) -> sprintf "%s\n\n%s" libname (File.readAsString path))
  |> String.concat (sprintf "\n%s\n" <| String.replicate 50 "-")
  |> File.writeString false (sprintf "%s/LICENSE.txt" outputPath)

let publish (runtime: string) (outputBinDir) =
  "src/NekocoPunching/NekocoPunching.fsproj"
  |> DotNet.publish (fun p ->
    { p with
        Runtime = Some runtime
        Configuration = DotNet.BuildConfiguration.Release
        SelfContained = Some true
        MSBuildParams = {
          p.MSBuildParams with
            Properties =
              ("PublishSingleFile", "true")
              :: ("PublishTrimmed", "true")
              :: p.MSBuildParams.Properties
        }
        OutputPath = Some outputBinDir
    }
  )

let copyResources outoutPath =
  "Resources.pack" |> Shell.copyFile outoutPath

let zipPublishedFiles outputPath =
  Trace.tracefn "Make %s.zip" outputPath
  !! (sprintf "%s/**" outputPath)
  |> Zip.zip "publish" (sprintf "%s.zip" outputPath)


Target.create "PublishWin" (fun _ ->
  let runtime = "win-x64"
  let outputPath = getOutputPath runtime

  Trace.tracefn "Clean %s" outputPath
  Shell.cleanDir outputPath
  
  publish runtime outputPath
  copyReadme outputPath
  makeLicense outputPath
  copyResources outputPath
  zipPublishedFiles outputPath
)

Target.create "PublishMac" (fun _ ->
  let runtime = "osx-x64"
  let outputPath = getOutputPath runtime
  let binOutputDir = outputPath + "/Bin"

  Trace.tracefn "Clean %s" outputPath
  Shell.cleanDir outputPath

  publish runtime binOutputDir
  copyReadme outputPath
  makeLicense outputPath
  copyResources outputPath

  let shellFileName = "NekocoPunching.command"

  sprintf "PublishContents/%s" shellFileName
  |> Shell.copyFile outputPath

  Shell.Exec ("chmod", sprintf "+x %s/%s" outputPath shellFileName)
  |> ignore

  Shell.Exec ("chmod", sprintf "+x %s/NekocoPunching" binOutputDir)
  |> ignore

  zipPublishedFiles outputPath
)

Target.create "CopyLib" (fun _ ->
  [|
    @"lib/Altseed2.BoxUI/lib/Altseed2"
  |]
  |> Seq.iter (fun dir ->
    Shell.copyDir dir Altseed2Dir (fun _ -> true)
  )
)

Target.runOrDefault "Build"
