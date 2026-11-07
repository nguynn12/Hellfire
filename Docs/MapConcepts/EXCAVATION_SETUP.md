# Infected Excavation

This branch builds on feature/gameplay-showcase. Import the following packages through Unity Package Manager → My Assets. Keep their original file GUIDs and folder paths. The public repository carries the map scene and its project files. Asset Store packages are installed locally.

- [RPG Dungeon Kit DEMO](https://assetstore.unity.com/packages/3d/environments/dungeons/rpg-dungeon-kit-demo-348672)
- [Modular Industrial Catwalk Kit Free](https://assetstore.unity.com/packages/3d/props/industrial/modular-industrial-catwalk-kit-free-287064)
- [Modular Medieval Lanterns](https://assetstore.unity.com/packages/3d/environments/historic/modular-medieval-lanterns-85527)
- [Medieval props](https://assetstore.unity.com/packages/3d/props/medieval-props-41540)
- [FREE Shirtless Zombie](https://assetstore.unity.com/packages/3d/characters/humanoids/free-shirtless-zombie-276762)
- [Zombie Mutant Free](https://assetstore.unity.com/packages/3d/characters/humanoids/zombie-mutant-free-310842)

After importing Zombie Mutant, run these PowerShell commands from the project folder to create the local URP shader. Keep the checked-in destination .meta file so the Brute materials retain their shader reference. If Unity removes the .meta during import, restore it from Git after generating the shader.

```powershell
$source = 'Assets/Zombie_Mutant/shaders/Zombie_Mutant_ Shader Graph.shadergraph'
$target = 'Assets/_Project/Art/CodexShowcase/RealisticBrute_URP.shadergraph'
$graph = Get-Content -Raw -LiteralPath $source
$graph = $graph.Replace('BuiltIn.ShaderGraph.BuiltInLitSubTarget', 'Universal.ShaderGraph.UniversalLitSubTarget')
$graph = $graph.Replace('BuiltIn.ShaderGraph.BuiltInTarget', 'Universal.ShaderGraph.UniversalTarget')
[IO.File]::WriteAllText((Join-Path (Get-Location) $target), $graph)
git restore -- "$target.meta"
```

Open `Assets/_Project/Scenes/Hellfire_Infected_Excavation_Floor01.unity` and press Play. The map includes the Excavation and Pump Station sectors and the solid stone walls. The previous local Play Mode run checked all 16 enemy placements, nine navigation routes and both sectors' progression.
