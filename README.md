# NieR.Automata.Toolkit

A simple save-editting toolkit for NieR.Automata:
- Edit basic save items, including money, exp, and items
- Recommendations for selling/fusing chips
- [TBD] Edit outfits and hair colors

## Credit

This tool is a fork of [NieR.Automata.Editor by MysteryDash](https://github.com/MysteryDash/NieR.Automata.Editor)

# Getting Started

Go to the [Releases](https://github.com/RoboRyGuy/NieR.Automata.Toolkit/releases) page and download the latest version available.

Alternatively, clone the repository and build from source!

# Make Backups!

Be careful when using the save editor, and make sure that you always backup your save file before editing it.

# Features

From the original NieR.Automata.Editor, you can edit:
- Your header id (necessary if you use someone else's save)
    - To do this, copy the header ID from your own save and paste in the new save
- Your character name
- Your money
- Your experience
- Your inventory
- Your corpse's inventory
- Your chips
- Your weapons
 
This toolkit adds:
- Chip fusion recommendations
- Chip sale recommendations
- More TBD

## How Fusion and Sell Recommendations Work

Fusion and sell recommendations are generated based on the chips imported with your save
and a list of "desired" chips (the ability to edit this list is not yet implemented). 
When loaded, it creates a tree for each desired chip and fills it out as best it can with
the chips in the save file, with a preference for fulfilling lower-weight chips first.
The tree is adaptive, so if you have excessive low-weight chips they will be used for
higher-weight chips, which in turn will allow more high-weight chips to be used.

The result is: 
- Any chip that can reasonably be used to create a fusion is marked as 'keep'
- Any fusions that are ready immediately are marked 'fuse'
- Any remaining chips are marked 'sell'

This solution is not perfectly optimal - for example, a +3 [28] can theoretically be fused into a
+8 [21] (with enough diamond chips), but this system will discard it. That being said, this is about
the best prediction available, and it will help guarantee you don't waste diamond chips when you 
already have enough. It can also help keep peace-of-mind while cleaning inventory slots, knowing 
that the chips you're selling most likely will never help you reach your desired fusion goals.

# Prerequisite

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

# Contributing

I appreciate any and all contributions, so please feel free to create an issue or a pull request if you would like to contribute.

# License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

# Acknowledgments

This project would not be what it is without the previous works of:
- [MysteryDash](https://github.com/MysteryDash) | Created the original NieR.Automata.Editor
- [CensoredUsername](https://github.com/CensoredUsername)
- [JohnEdwa](https://github.com/JohnEdwa)
- [micktu](https://github.com/micktu)
- [Kerilk](https://github.com/Kerilk)
- [wmltogether](https://github.com/wmltogether)
- [LazyPlatypus9](https://github.com/LazyPlatypus9) | Big thanks to them for fixing an issue I had left open.
