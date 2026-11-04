# Third-party notices

## Icons

The icon shape (a slice of cake) is the `cake-slice` icon from [Lucide](https://lucide.dev),
recolored and exported as `.ico` files in the `Assets` folder.
It is not among the Lucide icons derived from the Feather project, so the ISC license applies.

```
ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

The pink color (#FF6DC6) was taken from the "cupcake" emoji of
[Fluent Emoji](https://github.com/microsoft/fluentui-emoji) (MIT License); no artwork from it is included.

The tray icons (`tray-white.ico`, `tray-black.ico`) are redrawn on the pixel grid by `tools/MakeTrayIcons.ps1`
and only follow the same idea; the app icon (`app.ico`) is the Lucide shape.

## Build-time analyzers

[Roslynator.Analyzers](https://github.com/dotnet/roslynator) (Apache 2.0) and
[Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) (MIT) run only during the build and are not
shipped with the app.

## NuGet packages

None. The app uses only the .NET libraries. (NAudio was removed together with the volume slider, see `docs/REMOVED_FEATURES.md`.)
