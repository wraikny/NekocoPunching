#r "paket:
source https://api.nuget.org/v3/index.json
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.IO.Zip
nuget Fake.Core.Target //"

#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"

open System
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let Altseed2Dir = @"lib/Altseed2"

let ProjectName = "NekocoPunching"

let getOutputPath = sprintf "publish/%s.%s" ProjectName

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

let dotnet cmd arg =
  let res = DotNet.exec id cmd arg
  if not res.OK then failwithf "Failed 'dotnet %s %s'" cmd arg

Target.create "Pack" (fun _ ->
  dotnet "fsi" "--exec pack.fsx"

  !! "Resources/**"
  |> Zip.zip "Resources" "Resources.zip"
)

let copyReadme outputPath =
  "PublishContents/README.md"
  |> Shell.copyFile outputPath

let makeLicense outputPath =
  [|
    ProjectName, "LICENSE"
    ".NET Core", "PublishContents/LICENSES/dotnetcore.txt"
    "FSharp.Data.LiteralProviders", "PublishContents/LICENSES/fsharp.data.literalproviders.txt"
    "Altseed2", "lib/Altseed2/LICENSE"
    "Altseed2.BoxUI", "lib/Altseed2.BoxUI/LICENSE"
  |]
  |> Array.map (fun (libname, path) -> sprintf "%s\n\n%s" libname (File.readAsString path))
  |> String.concat (sprintf "\n%s\n" <| String.replicate 50 "-")
  |> File.writeString false (sprintf "%s/LICENSE.txt" outputPath)

let publish (runtime: string) (outputBinDir) =
  sprintf "src/%s/%s.fsproj" ProjectName ProjectName
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
              :: ("CopyOutputSymbolsToPublishDirectory", "false")
              :: p.MSBuildParams.Properties
        }
        OutputPath = Some outputBinDir
    }
  )

  !! (sprintf "%s/**.pdb" outputBinDir)
  |> File.deleteAll

let copyResources outoutPath =
  "Resources.pack" |> Shell.copyFile outoutPath

Target.create "PublishWin" (fun _ ->
  let runtime = "win-x64"
  let outputPath = getOutputPath runtime

  Trace.tracefn "Clean %s" outputPath
  Shell.cleanDir outputPath
  
  publish runtime outputPath
  copyReadme outputPath
  makeLicense outputPath
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

  let shellFileName = sprintf "%s.command" ProjectName

  sprintf "PublishContents/%s" shellFileName
  |> Shell.copyFile outputPath

  Shell.Exec ("chmod", sprintf "+x %s/%s" outputPath shellFileName)
  |> ignore

  Shell.Exec ("chmod", sprintf "+x %s/%s" binOutputDir ProjectName)
  |> ignore
)

Target.create "Repack" (fun _ ->
  let artifactsDir = "artifacts"

  let zipDir outputPath =
    Trace.tracefn "Make %s.zip" outputPath
    !! (sprintf "%s/**" outputPath)
    |> Zip.zip artifactsDir (sprintf "%s.zip" outputPath)

  !! (sprintf "%s/**.zip" artifactsDir)
  |> Seq.iter (fun path ->
    let filename = IO.Path.GetFileNameWithoutExtension path

    let outputDir = sprintf "%s/%s" artifactsDir filename

    path |> Zip.unzip outputDir

    "Resources.pack" |> Shell.copyFile outputDir

    if filename.Contains "osx-x64" then
      Shell.Exec ("chmod", sprintf "+x %s/%s.command" outputDir ProjectName)
      |> ignore

      Shell.Exec ("chmod", sprintf "+x %s/Bin/%s" outputDir ProjectName)
      |> ignore
      ()

    !! (sprintf "%s/**" outputDir)
    |> Zip.zip artifactsDir (sprintf "publish/%s.zip" filename)

    Directory.delete outputDir
    
  )
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
