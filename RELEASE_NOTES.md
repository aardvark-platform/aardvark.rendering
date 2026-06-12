### 5.7.0-prerelease0009
- [Sg] `Heap.ofRenderObjectsAuto` — per-draw heap fields are detected automatically: every effect-consumed (incl. derived-rule bases), packable uniform supplied by the RO's own provider becomes a field; scene-scope uniforms stay ordinary. Field sets are interned into the bucket key; shared avals dedup to one arena region. Explicit-names `ofRenderObjects` unchanged as the restricting variant. New `autofields` golden test: classic vs explicit vs auto pixel-identical.

