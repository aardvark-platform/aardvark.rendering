### 5.7.0-prerelease0013
- [Sg] Heap: typeId-branching attribute decoder — the shader converts each allocation's source type (f32/i32/f64 x1-4, normalized C4b incl. BGRA layout fix, f64 bit-decoded without shaderFloat64) to the effect's input type at fetch (widen with (0,0,0,1), narrow, normalize, cast). Element types leave the host bucket key: mixed-format objects (C4b singletons, C4f buffers, V3d vs V4f positions) share ONE bucket. Unsupported pairs -> precise Heap.Diagnostics. GPU cost +2-3% on the gather, CPU slightly improved.

