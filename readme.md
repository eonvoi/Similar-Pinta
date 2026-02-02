> [!CAUTION]
> # This is a MODIFIED version!<br>
> **This is NOT original Pinta.**<br>
> If you want to install the original, go to https://github.com/PintaProject/Pinta instead!

> [!NOTE]
> Use at your own risk.<br>
> A good general rule is not to run any software if you don't trust its author.<br>
> You must build from source, I won't provide binaries for this project.

### Here's how my modified version looks:
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/40637769-637b-4260-a34c-6634a5022c7a" />

### Changes made
Generally, I'm trying to make this fork's layout as similar to Paint.NET as possible.<br>
I know for a fact my code would never get accepted into the original project, but nevertheless:
- The `Tooblox` is now 2 columns wide, just like Paint.NET
- The Tools inside the `Toolbox` were re-ordered to match that of Paint.NET 5.1.11
- Reorganized menus and submenus
  - Moved "File", "Edit" and "Layers" from the `Menu Button`'s popup directly next to "View, Image, ...", in the header
  - Moved "File, Edit, View, Image, ..." to the left, and replaced their icons with labels instead, just like Paint.NET does
  - The "Help" section was separated from the `Menu Button`'s popup into its own button that sits next to the `Menu Button` (just like Paint.NET does)
  - Duplicated "Save All" from "`Menu Button` > Window > Save All" into "File > Save All" (because Paint.NET displays "Save All" under "Save As")
- Added missing "File > Open Recent" functionality (the list updates when you save or open a file successfuly)
- Added a "Layer Properties" button to the `LayersListViewItemWidget`, now you don't have to double-click the layer widget to open up its properties
- Fixed an annoyance where, when you clicked outside the canvas with the `Rectangle Select` tool, it would select a 0x0 area instead of de-selecting
- Fixed a bug where when dragging the `Color Pickers` really fast would crash the whole app
- Fixed a visual bug where, when using the `Paintbrush` tool, the blend mode of a layer wouldn't be visible to the user until the canvas was invalidated and redrawn
- Simplified `Shape Type` names to be more intuitive to the user
- *Reformatted some code / changed variable names for readibility*
- ***(From a WIP PR in the original repo, I'm not the one who implemented this):*** Upgraded the transform tool to be able to accurately transform the selected pixels
- ***(From a PR in the original repo, I'm not the one who implemented this):*** The text tool's font family and font size are now 2 separate UI elements

> Note that some localization is broken and I won't fix it.

## Icons are from:
> [!NOTE]
> (Editor's note)<br>
> (this is from the original repo, i did not add any new 3rd party icons)

- [Paint.Net 3.0](http://www.getpaint.net/)
Used under [MIT License](http://www.opensource.org/licenses/mit-license.php)

- [Silk icon set](https://github.com/markjames/famfamfam-silk-icons)
Used under [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/)

- [Fugue icon set](https://p.yusukekamiyamane.com)
Used under [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/)

- Pinta contributors, under the same license as the project itself
(see `Pinta.Resources/icons/pinta-icons.md` for the list of such icons)

## Building on Windows
> [!NOTE]
> (Editor's note)<br>
> (this is from the original repo, build instructions should be the same as for the original)

First, install the required GTK-related dependencies:
- Install [MSYS2](https://www.msys2.org)
- From the CLANG64 terminal, run `pacman -S mingw-w64-clang-x86_64-libadwaita mingw-w64-clang-x86_64-webp-pixbuf-loader`.
  - For ARM64 Windows, use the `CLANGARM64` terminal and replace `clang-x86_64` with `clang-aarch64`.

Pinta can then be built by opening `Pinta.sln` in [Visual Studio](https://visualstudio.microsoft.com/).
Ensure that .NET 8 is installed via the Visual Studio installer.

For building on the command line:
- [Install the .NET 8 SDK](https://dotnet.microsoft.com/).
- Build:
  - `dotnet build`
- Run:
  - `dotnet run --project Pinta`

## Building on macOS

- Install .NET 8 and GTK4
  - `brew install dotnet-sdk libadwaita adwaita-icon-theme gettext webp-pixbuf-loader`
  - For Apple Silicon, set `DYLD_LIBRARY_PATH=/opt/homebrew/lib` in the environment so that Pinta can load the GTK libraries
  - For Intel, you may need to set `DYLD_LIBRARY_PATH=/usr/local/lib` when using .NET 9 or higher
- Build:
  - `dotnet build`
- Run:
  - `dotnet run --project Pinta`

## Building on Linux

- Install [.NET 8](https://dotnet.microsoft.com/) following the instructions for your Linux distribution.
- Install other dependencies (instructions are for Ubuntu 22.10, but should be similar for other distros):
  - `sudo apt install autotools-dev autoconf-archive gettext intltool libadwaita-1-dev`
  - Minimum library versions: `gtk` >= 4.18 and `libadwaita` >= 1.7
  - Optional dependencies: `webp-pixbuf-loader`

> [!NOTE]
> (Editor's note)<br>
> On Fedora Linux or Nobara Linux, you can download dependencies like so:
> ```
> sudo dnf install autoconf automake libtool autoconf-archive gettext intltool libadwaita-devel
> ```
 
- Build (option 1, for development and testing):
  - `dotnet build`
  - `dotnet run --project Pinta`
- ~~- Build (option 2, for installation):~~ (`make install` is broken, probably because i had lazily changed the app id, probably won't fix) <br> 
  - ~~`./autogen.sh`~~ <br>
  - ~~If building from a tarball, run `./configure` instead.~~ <br>
  - ~~Add the `--prefix=<install directory>` argument to install to a directory other than `/usr/local`.~~ <br>
  - ~~`make install`~~

## Building and Debugging in Docker
> [!NOTE]
> (Editor's note)<br>
> I personally don't use Docker, but this section was included in the original repo, so I'm keeping it

Follow the instructions of the corresponding [pinta-virtual-dev-environment](https://github.com/janrothkegel/pinta-virtual-dev-environment) project

## Getting help / contributing:

This is a fork, I won't be accepting any bug or feature requests

## Code quality
This is a fork, code quality checks are lesser, and there are no official approvers for this fork
