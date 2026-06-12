### 5.7.0-prerelease0011
- [Sg] `Sg.heap : ISg -> ISg` — collapse a subtree through the heap with one combinator (Ag + ISimpleSg dispatch paths; non-heapable objects pass through). Pixel-identity golden-tested on both paths.
- [Sg] `HeapConfig.Enabled` removed — calling `Heap.ofRenderObjects`/`Sg.heap` IS the opt-in. Remaining knobs live in `module Heap`.

