### 5.7.0-prerelease0015
- [Sg] Heap: geometry dedup looks through CONSTANT buffer avals to the value level — fresh per-leaf BufferView/aval wrappers around the SAME underlying array now share packed geometry (ArrayBuffer.Equals = array ReferenceEquals); key widened to (array-or-buffer source, byte offset, format typeId). Naturally-written per-node Primitives.Box scenes dedup with no authoring discipline. New geomvalue golden test.
- [Sg] Heap: NON-INDEXED draws are eligible — header index-cell sentinel (-1) makes the vertex fetch use gl_VertexIndex directly; indexed and non-indexed members ride the same bucket. Removes the old supply-Indices passthrough. New noindex golden test; makes Primitives.Box (and any non-indexed geometry) heap-eligible.

