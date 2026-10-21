# GameControllerWrapper native source provenance

- Upstream: https://github.com/apple/unityplugins (Apache-2.0, see LICENSE.txt in this folder)
- Commit: 229700c0130c59308fd9915ed0383978a19e73c6 (2022-12-12), path `plug-ins/Apple.GameController/Native/`
- Contents: that tree, byte-identical except for the two files below. The Xcode project is kept intact
  (macOS, iOS and tvOS targets). Unity ignores this folder because of the trailing `~`.

## Modification

`GameControllerWrapper_GetSymbolForInputName` takes three further arguments after `renderingMode`:
`pointSize: Float`, `symbolWeight: Int`, `useFillStyle: Bool`. On macOS the symbol is built as
`NSImage.SymbolConfiguration(pointSize:weight:scale:)` (weight is `NSFont.Weight(rawValue: CGFloat(symbolWeight))`,
scale unchanged), ".fill" is appended to the symbol name when `useFillStyle` is set, and the resulting image
is tinted white by the new `NSImage.tint(color:)` extension. The iOS/tvOS branches are unchanged because the
evidence below covers only the macOS bundle.

- `GameControllerWrapper/Controller/Controller.swift`
- `GameControllerWrapper/Extensions.swift` (adds `NSImage.tint(color:)`)

## Evidence

Static inspection (symbols, strings, disassembly) of the arm64 slice of the shipped macOS
GameControllerWrapper.bundle; nothing was executed.

- Exported symbol signature: `GameControllerWrapper_GetSymbolForInputName(uniqueId, inputName, symbolSize,
  renderingMode, pointSize: Float, symbolWeight: Int, useFillStyle: Bool)`.
- Control flow of the macOS path: the ".fill" string (0x6c6c69662e) is appended when `useFillStyle` is true,
  before the configuration is created; `+[NSImageSymbolConfiguration configurationWithPointSize:weight:scale:]`
  receives the Float widened to Double, the Int converted to Double, and the unchanged scale; then
  `imageWithSystemSymbolName:accessibilityDescription:`, `imageWithSymbolConfiguration:`, and a tint through
  `imageWithSize:flipped:drawingHandler:`. The `renderingMode` argument is unused on macOS.
- The tint closure is `NSImage.tint(color:)`: `-[NSColor set]`, `fill(using:)` with the current graphics
  context's compositing operation (default sourceOver), `drawInRect:fromRect:operation:fraction:` with
  operation 7 (destinationIn), fraction 1.0, return true. The colour is `+[NSColor whiteColor]`.
- The managed declaration in this package (`GCControllerService`) has the matching parameters, with defaults
  60 / Black / true.
- The rest of the shipped native symbols match the upstream sources at the commit above.

Source-level spelling (variable names, optional chaining, formatting) is reconstructed, not recovered.
